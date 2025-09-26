using ePisarnica.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using ePisarnica.Helpers;
using ePisarnica.Services;
using static ePisarnica.Services.PDFSigningService;
using iTextSharp.text.pdf.security;
using Microsoft.AspNetCore.Authorization;

namespace ePisarnica.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DigitalSignatureController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        private readonly IPDFSigningService _pdfSigningService;
        private readonly IConfiguration _configuration;
        private readonly IPDFSignatureDetectionService _pdfSignatureDetectionService;
        private readonly EmailService _emailService;
        private readonly IWordToPdfService _wordToPdfService;

        public DigitalSignatureController(AppDbContext context, IWebHostEnvironment env, IPDFSigningService pdfSigningService, IConfiguration configuration, IPDFSignatureDetectionService pdfSignatureDetectionService, EmailService emailService, IWordToPdfService wordToPdfService)
        {
            _context = context;
            _env = env;
            _pdfSigningService = pdfSigningService;
            _configuration = configuration;
            _pdfSignatureDetectionService = pdfSignatureDetectionService;
            _emailService = emailService;
            _wordToPdfService = wordToPdfService;
        }


        // GET: api/DigitalSignature/ForDocument/5
        [HttpGet("ForDocument/{documentId}")]
        public async Task<IActionResult> GetSignaturesForDocument(int documentId)
        {
            try
            {
                var signatures = await _context.DigitalSignatures
                    .Include(s => s.User)
                    .Where(s => s.DocumentId == documentId)
                    .OrderByDescending(s => s.SignedAt)
                    .Select(s => new DigitalSignatureDTO
                    {
                        Id = s.Id,
                        DocumentId = s.DocumentId,
                        UserId = s.UserId,
                        SignatureData = s.SignatureData,
                        SignatureHash = s.SignatureHash,
                        SignedAt = s.SignedAt,
                        Reason = s.Reason,
                        Location = s.Location,
                        IsValid = s.IsValid,
                        ValidatedAt = s.ValidatedAt,
                        Username = s.User.Username,
                        Email = s.User.Email
                    })
                    .ToListAsync();

                return Ok(signatures);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Greška: {ex.Message}" });
            }
        }

        // POST: api/DigitalSignature
        [HttpPost]
        public async Task<IActionResult> CreateSignature([FromBody] CreateSignatureRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                var document = await _context.Documents
                    .FirstOrDefaultAsync(d => d.Id == request.DocumentId);

                if (document == null)
                {
                    return BadRequest(new { message = "Dokument nije pronađen" });
                }

                var signatureHash = GenerateSignatureHash(request.SignatureData, userId, request.DocumentId);

                var existingSignature = await _context.DigitalSignatures
                    .FirstOrDefaultAsync(s => s.DocumentId == request.DocumentId && s.UserId == userId);

                if (existingSignature != null)
                {
                    return BadRequest(new { message = "Već ste potpisali ovaj dokument" });
                }

                var signature = new DigitalSignature
                {
                    DocumentId = request.DocumentId,
                    UserId = userId,
                    SignatureData = request.SignatureData,
                    SignatureHash = signatureHash,
                    Reason = request.Reason,
                    Location = request.Location,
                    SignedAt = DateTime.Now
                };

                _context.DigitalSignatures.Add(signature);
                await _context.SaveChangesAsync();

                document.Status = DocumentStatus.Potpisan;
                document.ModifiedAt = DateTime.Now;
                _context.Documents.Update(document);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Dokument uspješno potpisan",
                    signatureId = signature.Id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Greška: {ex.Message}" });
            }
        }


        [HttpPost("SignPdf")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SignPdfDocument([FromBody] SignPdfRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                var document = await _context.Documents
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.Id == request.DocumentId);

                if (document == null)
                    return BadRequest(new { message = "Dokument nije pronađen" });

                var originalFilePath = Path.Combine(_env.WebRootPath, document.FilePath.TrimStart('/'));
                if (!System.IO.File.Exists(originalFilePath))
                    return BadRequest(new { message = "Fajl nije pronađen" });

                byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(originalFilePath);
                byte[] pdfBytes;
                string newFileName;
                string newFilePath;

                // Check if it's a Word document and convert to PDF if needed
                if (_wordToPdfService.IsWordDocument(document.FileName))
                {
                    Console.WriteLine($"Converting Word document to PDF: {document.FileName}");

                    // Convert Word to PDF
                    pdfBytes = await _wordToPdfService.ConvertWordToPdfAsync(fileBytes);

                    // Generate new PDF filename
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(document.FileName);
                    newFileName = $"{fileNameWithoutExt}.pdf";
                    newFilePath = Path.Combine(Path.GetDirectoryName(originalFilePath), newFileName);

                    // Save the converted PDF
                    await System.IO.File.WriteAllBytesAsync(newFilePath, pdfBytes);

                    // Delete the original Word file
                    System.IO.File.Delete(originalFilePath);

                    // Update document record with new PDF info
                    document.FileName = newFileName;
                    document.FilePath = document.FilePath.Replace(Path.GetFileName(document.FilePath), newFileName);
                    document.ModifiedAt = DateTime.Now;

                    Console.WriteLine($"Word document converted to PDF: {newFileName}");
                }
                else if (Path.GetExtension(document.FileName).ToLower() == ".pdf")
                {
                    // It's already a PDF
                    pdfBytes = fileBytes;
                    newFilePath = originalFilePath;
                    newFileName = document.FileName;
                }
                else
                {
                    return BadRequest(new { message = "Podržani su samo PDF i Word dokumenti (.pdf, .doc, .docx)" });
                }

                // Get certificate configuration
                var certPath = _configuration["DigitalSigning:CertificatePath"];
                var certPassword = _configuration["DigitalSigning:CertificatePassword"];

                if (string.IsNullOrEmpty(certPath) || string.IsNullOrEmpty(certPassword))
                    return StatusCode(500, new { message = "Certifikat nije konfigurisan" });

                // Sign the PDF
                byte[] signedPdf = _pdfSigningService.SignPdf(
                    pdfBytes,
                    request.SignatureData,
                    request.Reason,
                    request.Location,
                    certPath,
                    certPassword
                );

                // Create signed filename
                var signedFileName = $"{Path.GetFileNameWithoutExtension(newFileName)}_signed{Path.GetExtension(newFileName)}";
                var signedFilePath = Path.Combine(Path.GetDirectoryName(newFilePath), signedFileName);

                // Save signed PDF
                await System.IO.File.WriteAllBytesAsync(signedFilePath, signedPdf);

                // Delete the unsigned PDF (if it was converted from Word)
                if (_wordToPdfService.IsWordDocument(Path.GetExtension(document.FileName).Replace(".pdf", "")))
                {
                    if (System.IO.File.Exists(newFilePath))
                    {
                        System.IO.File.Delete(newFilePath);
                    }
                }

                // Generate signature hash
                var signatureHash = GenerateSignatureHash(request.SignatureData, userId, request.DocumentId);

                // Create digital signature record
                var signature = new DigitalSignature
                {
                    DocumentId = request.DocumentId,
                    UserId = userId,
                    SignatureData = request.SignatureData,
                    SignatureHash = signatureHash,
                    Reason = request.Reason,
                    Location = request.Location,
                    SignedAt = DateTime.Now,
                    IsValid = true,
                    ValidatedAt = DateTime.Now
                };

                _context.DigitalSignatures.Add(signature);

                // Update document record
                document.FilePath = document.FilePath.Replace(Path.GetFileName(document.FilePath), signedFileName);
                document.FileName = signedFileName;
                document.ModifiedAt = DateTime.Now;
                document.Status = DocumentStatus.Potpisan;
                _context.Documents.Update(document);

                // Update protocol entry
                var protocol = await _context.ProtocolEntries
                    .FirstOrDefaultAsync(p => p.DocumentId == document.Id);

                if (protocol != null)
                {
                    protocol.IsSigned = true;
                    protocol.SignedDate = DateTime.Now;
                    protocol.SignedByUserId = userId;
                    protocol.SignatureNotes = request.Reason;
                    protocol.ModifiedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // Send email notification
                Console.WriteLine("-------------------------------------------------------");
                Console.WriteLine($"Document processing complete: {document.FileName}");
                if (protocol != null)
                {
                    Console.WriteLine($"Protocol Entry Email: {protocol.Email}");
                    Console.WriteLine($"Protocol Stranka: {protocol.Stranka}");
                }
                Console.WriteLine("-------------------------------------------------------");

                if (protocol != null && !string.IsNullOrEmpty(protocol.Email))
                {
                    try
                    {
                        var subject = $"Vaš dokument '{document.FileName}' je digitalno potpisan";
                        var body = $@"
        <p>Poštovani {protocol.Stranka},</p>
        <p>Vaš dokument <strong>{document.FileName}</strong> je digitalno potpisan 
        dana {DateTime.Now:dd.MM.yyyy HH:mm} od strane <strong>{User.Identity?.Name}</strong>.</p>
        <p><strong>Razlog:</strong> {request.Reason}</p>
        <br/>
        <p>Lijep pozdrav,<br/>ePisarnica</p>";

                        await _emailService.SendEmailAsync(protocol.Email, subject, body);
                    }
                    catch (Exception mailEx)
                    {
                        Console.WriteLine($"[EMAIL-ERROR] Greška pri slanju emaila: {mailEx.Message}");
                    }
                }

                var responseMessage = _wordToPdfService.IsWordDocument(Path.GetExtension(request.DocumentId.ToString()))
                    ? "Word dokument je konvertovan u PDF i digitalno potpisan"
                    : "PDF uspješno digitalno potpisan";

                return Ok(new
                {
                    message = responseMessage,
                    signatureId = signature.Id,
                    filePath = document.FilePath,
                    convertedToPdf = _wordToPdfService.IsWordDocument(document.FileName.Replace(".pdf", ""))
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SIGN-ERROR] Greška pri potpisivanju: {ex.Message}");
                return StatusCode(500, new { message = $"Greška: {ex.Message}" });
            }
        }



        // POST: api/DigitalSignature/Validate/5
        [HttpPost("Validate/{signatureId}")]
        public async Task<IActionResult> ValidateSignature(int signatureId)
        {
            try
            {
                var signature = await _context.DigitalSignatures
                    .FirstOrDefaultAsync(s => s.Id == signatureId);

                if (signature == null)
                {
                    return NotFound(new { message = "Potpis nije pronađen" });
                }

                var expectedHash = GenerateSignatureHash(signature.SignatureData, signature.UserId, signature.DocumentId);
                var isValid = signature.SignatureHash == expectedHash;

                signature.IsValid = isValid;
                signature.ValidatedAt = DateTime.Now;

                _context.DigitalSignatures.Update(signature);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = isValid ? "Potpis je valjan" : "Potpis nije valjan",
                    isValid = isValid
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Greška: {ex.Message}" });
            }
        }


        // GET: api/DigitalSignature/Certificate/5
        [HttpGet("Certificate/{userId}")]
        public async Task<IActionResult> GetUserCertificate(int userId)
        {
            var certificate = await _context.SignatureCertificates
                .FirstOrDefaultAsync(c => c.UserId == userId && !c.IsRevoked && c.ExpiresAt > DateTime.Now);

            if (certificate == null)
            {
                return NotFound(new { message = "Certifikat nije pronađen ili je istekao" });
            }

            return Ok(certificate);
        }

        private string GenerateSignatureHash(string signatureData, int userId, int documentId)
        {
            using (var sha256 = SHA256.Create())
            {
                var rawData = $"{signatureData}{userId}{documentId}{DateTime.Now:yyyyMMddHHmm}";
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                return Convert.ToBase64String(bytes);
            }
        }

        // Add this endpoint
        [HttpGet("CheckPdfSignatures/{documentId}")]
        public async Task<IActionResult> CheckPdfSignatures(int documentId)
        {
            try
            {
                var document = await _context.Documents
                    .FirstOrDefaultAsync(d => d.Id == documentId);

                if (document == null)
                {
                    return NotFound(new { message = "Dokument nije pronađen" });
                }

                var filePath = Path.Combine(_env.WebRootPath, document.FilePath.TrimStart('/'));

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound(new { message = "PDF fajl nije pronađen" });
                }

                var hasSignature = _pdfSignatureDetectionService.HasDigitalSignature(filePath);
                var signatureInfo = _pdfSignatureDetectionService.GetSignatureInfo(filePath);

                return Ok(new
                {
                    hasDigitalSignature = hasSignature,
                    signatures = signatureInfo,
                    documentInfo = new
                    {
                        id = document.Id,
                        title = document.Title,
                        fileName = document.FileName
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Greška: {ex.Message}" });
            }
        }

        // DELETE: api/DigitalSignature/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSignature(int id)
        {
            try
            {
                var signature = await _context.DigitalSignatures
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (signature == null)
                {
                    return NotFound(new { message = "Potpis nije pronađen" });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var isAdmin = User.IsInRole("Admin");

                if (signature.UserId != userId && !isAdmin)
                {
                    return Forbid();
                }

                _context.DigitalSignatures.Remove(signature);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Potpis uspješno obrisan" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Greška: {ex.Message}" });
            }
        }
    }

    public class CreateSignatureRequest
    {
        public int DocumentId { get; set; }
        public string SignatureData { get; set; }
        public string Reason { get; set; }
        public string Location { get; set; }
    }
}