using ePisarnica.Helpers;
using ePisarnica.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using iTextSharpText = iTextSharp.text;
using iTextSharpTextPdf = iTextSharp.text.pdf;
using System.Security.Claims;
using ePisarnica.Services;
using ePisarnica.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using System.Text;
using DocumentFormat.OpenXml.Spreadsheet;


namespace ePisarnica.Controllers
{
    public class ProtocolController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly QRHelper _qrHelper; 
        private readonly IPDFSignatureDetectionService _pdfSignatureDetectionService; 
        private readonly IConfiguration _configuration; 
        private readonly EmailService _emailService;

        public ProtocolController(AppDbContext context, IWebHostEnvironment env, QRHelper qRHelper, IPDFSignatureDetectionService pdfSignatureDetectionService, IConfiguration configuration, EmailService emailService)
        {
            _qrHelper = qRHelper;
            _context = context;
            _env = env;
            _pdfSignatureDetectionService = pdfSignatureDetectionService; 
            _configuration = configuration;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index(string search, string status, string dateFrom, string dateTo, int page = 1, int pageSize = 10)
        {
            var query = _context.ProtocolEntries
                .Include(p => p.Document)
                .Include(p => p.SignedByUser)
                .AsQueryable();

            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && currentUserId.HasValue)
            {
                query = query.Where(p => p.Document != null && p.Document.UserId == currentUserId.Value);
            }

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.BrojProtokola.ToString().Contains(search) ||
                                         p.Stranka.Contains(search) ||
                                         (p.Document != null && p.Document.FileName.Contains(search)));

            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.Document.Status.ToString() == status);

            if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var from))
                query = query.Where(p => p.Datum >= from);

            if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var to))
                query = query.Where(p => p.Datum <= to);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.Datum)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();


            var model = new PagedResult<ProtocolEntry>
            {
                Items = items,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            ViewBag.SearchTerm = search;
            ViewBag.StatusFilter = status;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.IsAdmin = isAdmin;

            return View(model);
        }


        [HttpPost]
        public async Task<IActionResult> UpdateDocumentStatus([FromBody] UpdateStatusRequest request)
        {
            try
            {
                Console.WriteLine("📥 UpdateDocumentStatus called");
                Console.WriteLine($"👉 ProtocolId received: {request.ProtocolId}");
                Console.WriteLine($"👉 NewStatus received: {request.NewStatus}");

                var entry = await _context.ProtocolEntries
                    .Include(p => p.Document)
                    .FirstOrDefaultAsync(p => p.Id == request.ProtocolId);

                if (entry == null)
                {
                    Console.WriteLine("❌ No ProtocolEntry found for this ProtocolId");
                    return Json(new { success = false, message = "Protokol nije pronađen" });
                }

                Console.WriteLine($"✅ ProtocolEntry found: Id={entry.Id}, BrojProtokola={entry.BrojProtokola}, DocumentId={entry.DocumentId}");

                if (entry.Document == null)
                {
                    Console.WriteLine("❌ ProtocolEntry found, but Document is NULL");
                    return Json(new { success = false, message = "Protokol ili dokument nije pronađen" });
                }

                Console.WriteLine($"✅ Document found: Id={entry.Document.Id}, CurrentStatus={entry.Document.Status}, ModifiedAt={entry.Document.ModifiedAt}");

                if (!Enum.TryParse<DocumentStatus>(request.NewStatus, out var newStatus))
                {
                    Console.WriteLine("❌ Invalid status value received");
                    return Json(new { success = false, message = "Nevalidan status" });
                }

                Console.WriteLine($"👉 Parsed new status: {newStatus}");

                entry.Document.Status = newStatus;
                entry.Document.ModifiedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                Console.WriteLine("💾 Changes saved successfully");

                return Json(new
                {
                    success = true,
                    message = "Status je uspešno ažuriran",
                    newStatus = newStatus.ToString(),
                    displayStatus = GetStatusDisplayName(newStatus)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔥 Exception: {ex}");
                return Json(new { success = false, message = $"Greška: {ex.Message}" });
            }
        }


        [HttpPost]
        public async Task<IActionResult> AddNote([FromBody] AddNoteRequest request)
        {
            var entry = await _context.ProtocolEntries.FirstOrDefaultAsync(p => p.Id == request.ProtocolId);

            if (entry == null)
                return Json(new { success = false, message = "Protokol nije pronađen" });

            if (!string.IsNullOrEmpty(entry.Napomena))
                entry.Napomena += "\n\n" + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + ": " + request.Note;
            else
                entry.Napomena = DateTime.Now.ToString("dd.MM.yyyy HH:mm") + ": " + request.Note;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Napomena je dodana" });
        }

        public async Task<IActionResult> Details(int id)
        {
            var entry = await _context.ProtocolEntries
                .Include(p => p.Document)
                .ThenInclude(d => d.Folder)
                .Include(p => p.Document)
                    .ThenInclude(d => d.DigitalSignatures)
                        .ThenInclude(ds => ds.User)
                .Include(p => p.SignedByUser) 
                .FirstOrDefaultAsync(p => p.Id == id);



            if (entry == null)
                return NotFound();

            var isAdmin = User.IsInRole("Admin");


            if (isAdmin && entry.IsNew)
            {
                entry.IsNew = false;
                await _context.SaveChangesAsync();
            }

            if (!isAdmin)
            {
                return View("UserDetails", entry);
            }

            if (entry.Document != null)
            {
                var filePath = Path.Combine(_env.WebRootPath, entry.Document.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    ViewBag.HasPdfDigitalSignature = _pdfSignatureDetectionService.HasDigitalSignature(filePath);
                    ViewBag.PdfSignatureInfo = _pdfSignatureDetectionService.GetSignatureInfo(filePath);
                }
                else
                {
                    ViewBag.HasPdfDigitalSignature = false;
                    ViewBag.PdfSignatureInfo = new List<PDFSignatureInfo>();
                }
            }

            return View(entry);
        }

        private IActionResult GeneratePdfReport(List<ProtocolEntry> entries, DateTime? dateFrom, DateTime? dateTo, string status = null, string vrstaPostupka = null)
        {
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    var document = new iTextSharpText.Document(iTextSharpText.PageSize.A4.Rotate(), 30, 30, 40, 40);
                    var writer = iTextSharpTextPdf.PdfWriter.GetInstance(document, memoryStream);

                    document.Open();

                    // Add company header/logo section
                    var headerTable = new iTextSharpTextPdf.PdfPTable(2);
                    headerTable.WidthPercentage = 100;
                    headerTable.SetWidths(new float[] { 3, 1 });

                    // Left side - Company info
                    var companyFont = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA_BOLD, 12, new iTextSharpText.BaseColor(51, 51, 51));
                    var companyCell = new iTextSharpTextPdf.PdfPCell(new iTextSharpText.Phrase("ePisarnica - Sistem Upravljanja Dokumentima", companyFont));
                    companyCell.Border = iTextSharpText.Rectangle.NO_BORDER;
                    companyCell.PaddingBottom = 5;
                    headerTable.AddCell(companyCell);

                    // Right side - Date
                    var dateFont = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA, 10, iTextSharpText.BaseColor.GRAY);
                    var dateCell = new iTextSharpTextPdf.PdfPCell(new iTextSharpText.Phrase($"Generisan: {DateTime.Now:dd.MM.yyyy HH:mm}", dateFont));
                    dateCell.Border = iTextSharpText.Rectangle.NO_BORDER;
                    dateCell.HorizontalAlignment = iTextSharpText.Element.ALIGN_RIGHT;
                    dateCell.PaddingBottom = 5;
                    headerTable.AddCell(dateCell);

                    document.Add(headerTable);

                    // Add separator line
                    var line = new iTextSharpText.Paragraph(new iTextSharpText.Chunk(new iTextSharpTextPdf.draw.LineSeparator(1f, 100f, new iTextSharpText.BaseColor(200, 200, 200), iTextSharpText.Element.ALIGN_CENTER, -2)));
                    document.Add(line);
                    document.Add(new iTextSharpText.Paragraph(" "));

                    // Enhanced title with gradient-like effect
                    var titleFont = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA_BOLD, 22, new iTextSharpText.BaseColor(41, 128, 185));
                    var title = new iTextSharpText.Paragraph("IZVJEŠTAJ PROTOKOLA", titleFont);
                    title.Alignment = iTextSharpText.Element.ALIGN_CENTER;
                    title.SpacingAfter = 20;
                    document.Add(title);

                    // Enhanced report summary in a styled box
                    var summaryTable = new iTextSharpTextPdf.PdfPTable(1);
                    summaryTable.WidthPercentage = 100;
                    summaryTable.SpacingBefore = 10;
                    summaryTable.SpacingAfter = 20;

                    var summaryContent = new StringBuilder();
                    summaryContent.AppendLine($"📊 PREGLED IZVJEŠTAJA");
                    summaryContent.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    summaryContent.AppendLine($"Ukupno zapisa: {entries.Count}");

                    if (dateFrom.HasValue || dateTo.HasValue)
                    {
                        summaryContent.AppendLine($"Period: {(dateFrom.HasValue ? dateFrom.Value.ToString("dd.MM.yyyy") : "Početak")} - {(dateTo.HasValue ? dateTo.Value.ToString("dd.MM.yyyy") : "Kraj")}");
                    }

                    if (!string.IsNullOrEmpty(status))
                        summaryContent.AppendLine($"Filtrirano po statusu: {status}");

                    if (!string.IsNullOrEmpty(vrstaPostupka))
                        summaryContent.AppendLine($"Filtrirano po vrsti postupka: {vrstaPostupka}");

                    // Add statistics
                    if (entries.Any())
                    {
                        var totalByStatus = entries.Where(e => e.Document != null).GroupBy(e => e.Document.Status).ToDictionary(g => g.Key, g => g.Count());
                        var urgentCount = entries.Count(e => !string.IsNullOrEmpty(e.Hitno) && e.Hitno.ToLower() == "da");

                        summaryContent.AppendLine($"Hitni predmeti: {urgentCount}");
                        summaryContent.AppendLine("Raspodjela po statusima:");
                        foreach (var statusGroup in totalByStatus)
                        {
                            summaryContent.AppendLine($"  • {statusGroup.Key}: {statusGroup.Value}");
                        }
                    }

                    var summaryFont = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA, 11, new iTextSharpText.BaseColor(51, 51, 51));
                    var summaryCell = new iTextSharpTextPdf.PdfPCell(new iTextSharpText.Phrase(summaryContent.ToString(), summaryFont));
                    summaryCell.BackgroundColor = new iTextSharpText.BaseColor(248, 249, 250);
                    summaryCell.Border = iTextSharpText.Rectangle.BOX;
                    summaryCell.BorderColor = new iTextSharpText.BaseColor(222, 226, 230);
                    summaryCell.BorderWidth = 1f;
                    summaryCell.Padding = 15;
                    summaryTable.AddCell(summaryCell);
                    document.Add(summaryTable);

                    if (!entries.Any())
                    {
                        var noDataFont = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA, 14, iTextSharpText.BaseColor.GRAY);
                        var noData = new iTextSharpText.Paragraph("Nema podataka za prikaz na osnovu zadatih kriterija.", noDataFont);
                        noData.Alignment = iTextSharpText.Element.ALIGN_CENTER;
                        noData.SpacingBefore = 30;
                        document.Add(noData);
                    }
                    else
                    {
                        // Enhanced main table with better styling
                        var table = new iTextSharpTextPdf.PdfPTable(9);
                        table.WidthPercentage = 100;
                        table.SetWidths(new float[] { 1f, 1.2f, 2.5f, 2f, 2f, 1.2f, 1.5f, 1.5f, 1.8f });
                        table.SpacingBefore = 10;

                        // Enhanced table headers with gradient-like effect
                        var headerFont = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA_BOLD, 10, iTextSharpText.BaseColor.WHITE);
                        var headerBgColor = new iTextSharpText.BaseColor(52, 73, 94); // Dark blue-gray

                        string[] headers = { "Br. Prot.", "Datum", "Stranka", "Primalac", "Vrsta Postupka", "Hitno", "Status", "Dostavio", "Telefon" };

                        foreach (string header in headers)
                        {
                            AddEnhancedTableHeaderCell(table, header, headerFont, headerBgColor);
                        }

                        // Enhanced table data with better formatting
                        var cellFont = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA, 9, new iTextSharpText.BaseColor(51, 51, 51));
                        var alternateBgColor = new iTextSharpText.BaseColor(249, 249, 249);
                        var urgentColor = new iTextSharpText.BaseColor(255, 243, 224);
                        var isAlternate = false;

                        foreach (var entry in entries.OrderBy(e => e.BrojProtokola))
                        {
                            var isUrgent = !string.IsNullOrEmpty(entry.Hitno) && entry.Hitno.ToLower() == "da";
                            var rowBgColor = isUrgent ? urgentColor : (isAlternate ? alternateBgColor : null);
                            var statusText = entry.Document?.Status.ToString() ?? "Bez dokumenta";

                            AddEnhancedTableCell(table, entry.BrojProtokola.ToString(), cellFont, rowBgColor, isUrgent);
                            AddEnhancedTableCell(table, entry.Datum.ToString("dd.MM.yyyy"), cellFont, rowBgColor);
                            AddEnhancedTableCell(table, TruncateText(entry.Stranka, 35), cellFont, rowBgColor);
                            AddEnhancedTableCell(table, TruncateText(entry.Primalac ?? "—", 25), cellFont, rowBgColor);
                            AddEnhancedTableCell(table, TruncateText(entry.VrstaPostupka ?? "—", 25), cellFont, rowBgColor);

                            var urgentText = entry.Hitno ?? "—";
                            var urgentFont = isUrgent ?
                                iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA_BOLD, 9, new iTextSharpText.BaseColor(220, 53, 69)) :
                                cellFont;
                            AddEnhancedTableCell(table, urgentText, urgentFont, rowBgColor);

                            AddEnhancedTableCell(table, statusText, cellFont, rowBgColor);
                            AddEnhancedTableCell(table, TruncateText(entry.Dostavio ?? "—", 20), cellFont, rowBgColor);
                            AddEnhancedTableCell(table, entry.Telefon ?? "—", cellFont, rowBgColor);

                            isAlternate = !isAlternate;
                        }

                        document.Add(table);

                        document.Add(new iTextSharpText.Paragraph(" "));
                        var legendFont = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA, 8, iTextSharpText.BaseColor.GRAY);
                        var legend = new iTextSharpText.Paragraph("🔸 Legenda: \n• Označene su hitne stavke žutom bojom \n• Oznaka — označava prazno polje", legendFont);
                        legend.Alignment = iTextSharpText.Element.ALIGN_LEFT;
                        document.Add(legend);
                    }

                    var footerFont = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA, 8, iTextSharpText.BaseColor.GRAY);
                    var footer = new iTextSharpText.Paragraph($"Izvještaj kreiran {DateTime.Now:dd.MM.yyyy u HH:mm} • ePisarnica v2.0", footerFont);
                    footer.Alignment = iTextSharpText.Element.ALIGN_CENTER;
                    footer.SpacingBefore = 20;
                    document.Add(footer);

                    document.Close();

                    var fileName = $"Protokol_Izvjestaj_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    return File(memoryStream.ToArray(), "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Greška pri generisanju PDF izvještaja: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private void AddEnhancedTableHeaderCell(iTextSharpTextPdf.PdfPTable table, string text, iTextSharpText.Font font, iTextSharpText.BaseColor backgroundColor)
        {
            var cell = new iTextSharpTextPdf.PdfPCell(new iTextSharpText.Phrase(text, font));
            cell.BackgroundColor = backgroundColor;
            cell.HorizontalAlignment = iTextSharpText.Element.ALIGN_CENTER;
            cell.VerticalAlignment = iTextSharpText.Element.ALIGN_MIDDLE;
            cell.Padding = 8;
            cell.PaddingTop = 10;
            cell.PaddingBottom = 10;
            cell.BorderColor = new iTextSharpText.BaseColor(44, 62, 80);
            cell.BorderWidth = 1f;
            table.AddCell(cell);
        }

        private void AddEnhancedTableCell(iTextSharpTextPdf.PdfPTable table, string text, iTextSharpText.Font font, iTextSharpText.BaseColor backgroundColor = null, bool isUrgent = false)
        {
            var cell = new iTextSharpTextPdf.PdfPCell(new iTextSharpText.Phrase(text ?? "—", font));

            if (backgroundColor != null)
            {
                cell.BackgroundColor = backgroundColor;
            }

            if (isUrgent && backgroundColor == null)
            {
                cell.BackgroundColor = new iTextSharpText.BaseColor(255, 243, 224);
            }

            cell.Padding = 6;
            cell.PaddingTop = 8;
            cell.PaddingBottom = 8;
            cell.BorderColor = new iTextSharpText.BaseColor(222, 226, 230);
            cell.BorderWidth = 0.5f;
            cell.VerticalAlignment = iTextSharpText.Element.ALIGN_MIDDLE;

            table.AddCell(cell);
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text ?? "—";

            return text.Substring(0, maxLength - 3) + "...";
        }

        // ProtocolEntryController.cs - ažuriraj Create metode
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            Console.WriteLine("=== CREATE GET METHOD CALLED ===");
            Console.WriteLine($"Timestamp: {DateTime.Now}");
            Console.WriteLine($"User authenticated: {User.Identity?.IsAuthenticated}");
            Console.WriteLine($"User name: {User.Identity?.Name}");
            Console.WriteLine($"User ID from claims: {User.FindFirstValue(ClaimTypes.NameIdentifier)}");

            try
            {
                await PrepareViewData();
                Console.WriteLine("PrepareViewData completed successfully");
                Console.WriteLine("Returning Create view");
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in GET Create: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProtocolEntry protocolEntry)
        {
            Console.WriteLine("=== CREATE METHOD START ===");
            Console.WriteLine($"Timestamp: {DateTime.Now}");
            Console.WriteLine($"User Identity Name: {User.Identity?.Name}");
            Console.WriteLine($"User Identity IsAuthenticated: {User.Identity?.IsAuthenticated}");

            try
            {
                Console.WriteLine("=== MODEL STATE VALIDATION ===");
                Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");


                protocolEntry.Stranka = User.Identity?.Name ?? "Nepoznat korisnik";

                protocolEntry.Primalac = null;
                if (!ModelState.IsValid)
                {
                    Console.WriteLine("MODEL STATE IS INVALID:");
                    var errors = ModelState.Values.SelectMany(v => v.Errors);
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"  - Model error: {error.ErrorMessage}");
                    }

                    foreach (var kvp in ModelState)
                    {
                        if (kvp.Value.Errors.Count > 0)
                        {
                            Console.WriteLine($"  - Field '{kvp.Key}' has errors:");
                            foreach (var error in kvp.Value.Errors)
                            {
                                Console.WriteLine($"    * {error.ErrorMessage}");
                            }
                        }
                    }

                    Console.WriteLine("Preparing ViewData due to validation errors...");
                    await PrepareViewData();
                    return View(protocolEntry);
                }

                Console.WriteLine("=== PROTOCOL ENTRY DATA ===");
                Console.WriteLine($"Stranka: '{protocolEntry.Stranka}'");
                Console.WriteLine($"Primalac: '{protocolEntry.Primalac}'");
                Console.WriteLine($"Napomena: '{protocolEntry.Napomena}'");
                Console.WriteLine($"VrstaPostupka: '{protocolEntry.VrstaPostupka}'");
                Console.WriteLine($"Hitno: '{protocolEntry.Hitno}'");
                Console.WriteLine($"Dostavio: '{protocolEntry.Dostavio}'");
                Console.WriteLine($"DocumentId: {protocolEntry.DocumentId}");

                Console.WriteLine("=== GETTING NEXT PROTOCOL NUMBER ===");
                try
                {
                    var maxBroj = await _context.ProtocolEntries.MaxAsync(p => (int?)p.BrojProtokola) ?? 0;
                    Console.WriteLine($"Current max protocol number: {maxBroj}");

                    protocolEntry.BrojProtokola = maxBroj + 1;
                    Console.WriteLine($"New protocol number assigned: {protocolEntry.BrojProtokola}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR getting max protocol number: {ex.Message}");
                    throw;
                }

                protocolEntry.Datum = DateTime.Now;
                protocolEntry.ModifiedAt = DateTime.Now;
                Console.WriteLine($"Datum set to: {protocolEntry.Datum}");
                Console.WriteLine($"ModifiedAt set to: {protocolEntry.ModifiedAt}");

                Console.WriteLine("=== FILE UPLOAD HANDLING ===");
                if (protocolEntry.UploadedFile != null && protocolEntry.UploadedFile.Length > 0)
                {
                    Console.WriteLine($"File uploaded: {protocolEntry.UploadedFile.FileName}");
                    Console.WriteLine($"File size: {protocolEntry.UploadedFile.Length} bytes");
                    Console.WriteLine($"File content type: {protocolEntry.UploadedFile.ContentType}");

                    try
                    {
                        var document = await HandleFileUpload(protocolEntry.UploadedFile);
                        if (document != null)
                        {
                            protocolEntry.DocumentId = document.Id;
                            protocolEntry.OriginalFileName = protocolEntry.UploadedFile.FileName;
                            Console.WriteLine($"Document created successfully with ID: {document.Id}");
                            Console.WriteLine($"Original filename set to: {protocolEntry.OriginalFileName}");
                        }
                        else
                        {
                            Console.WriteLine("ERROR: Document creation returned null");
                        }
                    }
                    catch (Exception fileEx)
                    {
                        Console.WriteLine($"ERROR in file upload: {fileEx.Message}");
                        Console.WriteLine($"File upload stack trace: {fileEx.StackTrace}");
                    }
                }
                else
                {
                    Console.WriteLine("No file uploaded or file is empty");
                    if (protocolEntry.UploadedFile == null)
                        Console.WriteLine("  - UploadedFile is null");
                    else
                        Console.WriteLine($"  - UploadedFile.Length is {protocolEntry.UploadedFile.Length}");
                }

                Console.WriteLine("=== SAVING PROTOCOL ENTRY ===");
                Console.WriteLine($"Protocol entry before save:");
                Console.WriteLine($"  - BrojProtokola: {protocolEntry.BrojProtokola}");
                Console.WriteLine($"  - Datum: {protocolEntry.Datum}");
                Console.WriteLine($"  - Stranka: '{protocolEntry.Stranka}'");
                Console.WriteLine($"  - DocumentId: {protocolEntry.DocumentId}");
                Console.WriteLine($"  - ModifiedAt: {protocolEntry.ModifiedAt}");

                try
                {
                    _context.ProtocolEntries.Add(protocolEntry);
                    Console.WriteLine("Protocol entry added to context");

                    var saveResult = await _context.SaveChangesAsync();
                    Console.WriteLine($"SaveChanges result: {saveResult} entities saved");
                    Console.WriteLine($"Protocol entry ID after save: {protocolEntry.Id}");
                }
                catch (Exception saveEx)
                {
                    Console.WriteLine($"ERROR saving protocol entry: {saveEx.Message}");
                    Console.WriteLine($"Save stack trace: {saveEx.StackTrace}");
                    if (saveEx.InnerException != null)
                    {
                        Console.WriteLine($"Save inner exception: {saveEx.InnerException.Message}");
                    }
                    throw;
                }

                Console.WriteLine("=== QR CODE GENERATION ===");
                if (protocolEntry.Id > 0)
                {
                    Console.WriteLine($"Generating QR code for protocol ID: {protocolEntry.Id}");
                    try
                    {



                        var qrData = $"Protokol: {protocolEntry.BrojProtokola}\nDokument: {protocolEntry.OriginalFileName}\nDatum: {protocolEntry.Datum:dd.MM.yyyy}";
                        Console.WriteLine($"QR data: {qrData}");

                        protocolEntry.QrCodePath = _qrHelper.GenerateQrCode(qrData, protocolEntry.BrojProtokola);
                        Console.WriteLine($"QR code path: {protocolEntry.QrCodePath}");

                        _context.Update(protocolEntry);
                        var qrSaveResult = await _context.SaveChangesAsync();
                        Console.WriteLine($"QR code update save result: {qrSaveResult}");
                    }
                    catch (Exception qrEx)
                    {
                        Console.WriteLine($"QR code generation failed: {qrEx.Message}");
                        Console.WriteLine($"QR stack trace: {qrEx.StackTrace}");
                    }
                }
                else
                {
                    Console.WriteLine($"ERROR: Protocol ID is {protocolEntry.Id}, cannot generate QR code");
                }

                Console.WriteLine("=== SUCCESS ===");
                var successMessage = "Protokol uspješno kreiran!" +
                    (protocolEntry.UploadedFile != null ? " Fajl je uploadovan." : "");
                Console.WriteLine($"Success message: {successMessage}");
                TempData["SuccessMessage"] = successMessage;

                Console.WriteLine("Redirecting to Index...");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine("=== EXCEPTION IN CREATE ===");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                Console.WriteLine($"Exception message: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception type: {ex.InnerException.GetType().Name}");
                    Console.WriteLine($"Inner exception message: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner exception stack trace: {ex.InnerException.StackTrace}");
                }

                ModelState.AddModelError("", "Došlo je do greške pri kreiranju protokola: " + ex.Message);
            }

            Console.WriteLine("=== RETURNING TO VIEW WITH ERRORS ===");
            await PrepareViewData();
            return View(protocolEntry);
        }
        private async Task<Models.Document?> HandleFileUpload(IFormFile file)
        {
            Console.WriteLine("=== HANDLE FILE UPLOAD START ===");
            Console.WriteLine($"File name: {file.FileName}");
            Console.WriteLine($"File size: {file.Length}");
            Console.WriteLine($"File content type: {file.ContentType}");

            var userIdInt = GetCurrentUserId();
            if (!userIdInt.HasValue)
            {
                Console.WriteLine("ERROR: User ID not found or invalid - cannot upload file");
                return null;
            }

            Console.WriteLine($"Using user ID: {userIdInt.Value}");

            try
            {
                var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", "protocols", userIdInt.Value.ToString());
                Console.WriteLine($"Upload path: {uploadsPath}");
                Console.WriteLine($"WebRootPath: {_env.WebRootPath}");

                if (!Directory.Exists(uploadsPath))
                {
                    Console.WriteLine("Upload directory does not exist, creating...");
                    Directory.CreateDirectory(uploadsPath);
                    Console.WriteLine($"Created upload directory: {uploadsPath}");
                }
                else
                {
                    Console.WriteLine("Upload directory already exists");
                }

                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                Console.WriteLine($"Generated file name: {fileName}");
                Console.WriteLine($"Full file path: {filePath}");

                Console.WriteLine("Starting file copy...");
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                Console.WriteLine("File copied successfully");

                // Verify file was created
                if (System.IO.File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    Console.WriteLine($"File verification: exists = true, size = {fileInfo.Length} bytes");
                }
                else
                {
                    Console.WriteLine("ERROR: File was not created successfully");
                    return null;
                }

                Console.WriteLine("Creating Document entity...");
                var document = new Models.Document
                {
                    Title = Path.GetFileNameWithoutExtension(file.FileName),
                    FileName = file.FileName,
                    FilePath = $"/uploads/protocols/{userIdInt.Value}/{fileName}",
                    FileSize = file.Length,
                    FileExtension = Path.GetExtension(file.FileName).ToLower(),
                    FileType = GetFileType(Path.GetExtension(file.FileName)),
                    UserId = userIdInt.Value,
                    CreatedAt = DateTime.Now,
                    ModifiedAt = DateTime.Now,
                    Status = DocumentStatus.Zaprimljeno
                };

                Console.WriteLine($"Document entity created:");
                Console.WriteLine($"  - Title: {document.Title}");
                Console.WriteLine($"  - FileName: {document.FileName}");
                Console.WriteLine($"  - FilePath: {document.FilePath}");
                Console.WriteLine($"  - FileSize: {document.FileSize}");
                Console.WriteLine($"  - FileExtension: {document.FileExtension}");
                Console.WriteLine($"  - FileType: {document.FileType}");
                Console.WriteLine($"  - UserId: {document.UserId}");
                Console.WriteLine($"  - Status: {document.Status}");

                Console.WriteLine("Adding document to context...");
                _context.Documents.Add(document);

                Console.WriteLine("Saving document to database...");
                var saveResult = await _context.SaveChangesAsync();
                Console.WriteLine($"Document save result: {saveResult} entities saved");
                Console.WriteLine($"Document ID after save: {document.Id}");

                if (document.Id > 0)
                {
                    Console.WriteLine("Document saved successfully");
                    return document;
                }
                else
                {
                    Console.WriteLine("ERROR: Document ID is 0 after save");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in HandleFileUpload: {ex.Message}");
                Console.WriteLine($"HandleFileUpload stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"HandleFileUpload inner exception: {ex.InnerException.Message}");
                }
                return null;
            }
        }

        private FileType GetFileType(string extension)
        {
            return extension.ToLower() switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => FileType.Image,
                ".pdf" or ".doc" or ".docx" or ".txt" or ".xls" or ".xlsx" => FileType.Document,
                ".mp3" or ".wav" or ".ogg" => FileType.Audio,
                ".mp4" or ".mov" or ".avi" => FileType.Video,
                ".zip" or ".rar" or ".7z" => FileType.Archive,
                _ => FileType.Other
            };
        }

        private async Task PrepareViewData()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out var userIdInt))
            {
                var documents = await _context.Documents
                    .Where(d => d.UserId == userIdInt && !d.IsTrashed && d.ProtocolEntry == null)
                    .Select(d => new SelectListItem
                    {
                        Value = d.Id.ToString(),
                        Text = $"{d.Title} ({d.FileName})"
                    })
                    .ToListAsync();

                ViewBag.DocumentId = new SelectList(documents, "Value", "Text");
            }
            else
            {
                ViewBag.DocumentId = new SelectList(new List<SelectListItem>());
            }

            ViewBag.StatusOptions = new SelectList(new[]
            {
        new { Value = "Zaprimljeno", Text = "Zaprimljeno" },
        new { Value = "Uvid", Text = "Na uvidu" },
        new { Value = "UObradi", Text = "U obradi" },
        new { Value = "NaDopuni", Text = "Na dopuni" },
        new { Value = "Recenzija", Text = "Recenzija" },
        new { Value = "Odobreno", Text = "Odobreno" },
        new { Value = "Odbijeno", Text = "Odbijeno" },
        new { Value = "Arhivirano", Text = "Arhivirano" }
    }, "Value", "Text");

            ViewBag.HitnoOptions = new SelectList(new[]
            {
        new { Value = "Da", Text = "Da" },
        new { Value = "Ne", Text = "Ne" },
        new { Value = "Normalno", Text = "Normalno" }
    }, "Value", "Text");

            ViewBag.VrstaPostupkaOptions = new SelectList(new[]
            {
        new { Value = "Administrativni", Text = "Administrativni" },
        new { Value = "Pravni", Text = "Pravni" },
        new { Value = "Finansijski", Text = "Finansijski" },
        new { Value = "Kadrovski", Text = "Kadrovski" },
        new { Value = "Tehnički", Text = "Tehnički" },
        new { Value = "Ostalo", Text = "Ostalo" }
    }, "Value", "Text");
        }
        private int? GetCurrentUserId()
        {
            Console.WriteLine("=== GET CURRENT USER ID ===");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"Raw userId from claims: '{userId}'");

            if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out var userIdInt))
            {
                Console.WriteLine($"Successfully parsed userId to: {userIdInt}");
                return userIdInt;
            }

            Console.WriteLine("Failed to get or parse userId");
            Console.WriteLine($"User.Identity.IsAuthenticated: {User.Identity?.IsAuthenticated}");
            Console.WriteLine($"User.Identity.Name: {User.Identity?.Name}");

            Console.WriteLine("All user claims:");
            foreach (var claim in User.Claims)
            {
                Console.WriteLine($"  - {claim.Type}: {claim.Value}");
            }

            return null;
        }
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sign(int id, string signatureNotes)
        {
            try
            {
                var protocol = await _context.ProtocolEntries
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (protocol == null)
                {
                    return Json(new { success = false, message = "Protokol nije pronađen" });
                }

                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue)
                {
                    return Json(new { success = false, message = "Korisnik nije autentifikovan" });
                }

                protocol.IsSigned = true;
                protocol.SignedDate = DateTime.Now;
                protocol.SignedByUserId = currentUserId.Value;
                protocol.SignatureNotes = signatureNotes;
                protocol.ModifiedAt = DateTime.Now;

                if (protocol.DocumentId.HasValue)
                {
                    var document = await _context.Documents.FindAsync(protocol.DocumentId.Value);
                    if (document != null)
                    {
                        document.Status = DocumentStatus.Odobreno;
                        document.ModifiedAt = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(protocol.Email))
                {
                    Console.WriteLine($"[SIGN-DEBUG] Email u bazi: {protocol.Email}");
                    await SendSignatureNotificationEmail(protocol);
                }
                else
                {
                    Console.WriteLine("[SIGN-DEBUG] protocol.Email je null ili prazan");
                }

                return Json(new
                {
                    success = true,
                    message = "Protokol uspješno potpisan",
                    signedBy = User.Identity.Name,
                    signedDate = protocol.SignedDate.Value.ToString("dd.MM.yyyy HH:mm")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Greška pri potpisivanju: {ex.Message}" });
            }
        }
        

        private async Task SendSignatureNotificationEmail(ProtocolEntry protocol)
        {
            try
            {
                Console.WriteLine($"[EMAIL-DEBUG] Pokrećem slanje emaila za protokol {protocol.Id}");

                if (string.IsNullOrEmpty(protocol.Email))
                {
                    Console.WriteLine("[EMAIL-DEBUG] Nema email adrese u protocol.Email");
                    return;
                }

                var subject = $"Protokol {protocol.BrojProtokola} je potpisan";

                var body = $@"
            <p>Poštovani,</p>
            <p>Vaš protokol broj <strong>{protocol.BrojProtokola}</strong> je potpisan 
            dana {protocol.SignedDate:dd.MM.yyyy HH:mm} od strane <strong>{User?.Identity?.Name}</strong>.</p>
            <p><strong>Napomena:</strong> {protocol.SignatureNotes}</p>
            <br/>
            <p>Lijep pozdrav,<br/>ePisarnica</p>";

                Console.WriteLine($"[EMAIL-DEBUG] Šaljem email na: {protocol.Email}");
                await _emailService.SendEmailAsync(protocol.Email, subject, body);
                Console.WriteLine("[EMAIL-DEBUG] Email poslan!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL-ERROR] Greška pri slanju emaila: {ex.Message}");
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var protocolEntry = await _context.ProtocolEntries
                .Include(p => p.Document)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (protocolEntry == null)
                return NotFound();

            if (!string.IsNullOrEmpty(protocolEntry.QrCodePath))
            {
                bool qrDeleted = DeletePhysicalFile(protocolEntry.QrCodePath);
                if (!qrDeleted)
                    TempData["Warning"] = "QR code could not be deleted.";
            }

            if (protocolEntry.Document != null)
            {
                
                var signatures = _context.DigitalSignatures
                    .Where(ds => ds.DocumentId == protocolEntry.Document.Id);
                _context.DigitalSignatures.RemoveRange(signatures);

                if (!string.IsNullOrEmpty(protocolEntry.Document.FilePath))
                {
                    bool docDeleted = DeletePhysicalFile(protocolEntry.Document.FilePath);
                    if (!docDeleted)
                        TempData["Warning"] += $" Associated document '{protocolEntry.Document.FileName}' could not be deleted.";
                }

                _context.Documents.Remove(protocolEntry.Document);
            }

            _context.ProtocolEntries.Remove(protocolEntry);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool DeletePhysicalFile(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void LogProtocolView(int protocolId, string userName)
        {
            System.Diagnostics.Debug.WriteLine($"Protocol {protocolId} viewed by {userName} at {DateTime.Now}");
        }
        [HttpGet]
        public async Task<IActionResult> Search(string term)
        {
            if (string.IsNullOrEmpty(term))
                return Json(new List<object>());

            var query = _context.ProtocolEntries
                .Include(p => p.Document)
                .AsQueryable();

            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && currentUserId.HasValue)
            {
                query = query.Where(p => p.Document != null && p.Document.UserId == currentUserId.Value);
            }

            var results = await query
                .Where(p =>
                    p.BrojProtokola.ToString().Contains(term) ||
                    p.Stranka.Contains(term) ||
                    (p.Document != null && p.Document.Title.Contains(term))
                )
                .Take(10)
                .Select(p => new {
                    id = p.Id,
                    brojProtokola = p.BrojProtokola,
                    stranka = p.Stranka,
                    datum = p.Datum.ToString("dd.MM.yyyy"),
                    fileName = p.Document != null ? p.Document.FileName : null,
                    hasFile = p.Document != null
                })
                .ToListAsync();

            return Json(results);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateFile([FromBody] UpdateFileRequest request)
        {
            try
            {
                var protocol = await _context.ProtocolEntries
                    .Include(p => p.Document)
                    .FirstOrDefaultAsync(p => p.Id == request.ProtocolId);

                if (protocol?.Document == null)
                    return Json(new { success = false, message = "Dokument nije pronađen" });

                if (!string.IsNullOrEmpty(request.FileName))
                {
                    protocol.Document.Title = request.FileName;
                    protocol.Document.FileName = request.FileName + protocol.Document.FileExtension;
                    protocol.Document.ModifiedAt = DateTime.Now;
                }

                if (!string.IsNullOrEmpty(request.NewStatus) &&
                    Enum.TryParse<DocumentStatus>(request.NewStatus, out var newStatus))
                {
                    protocol.Document.Status = newStatus;
                    protocol.Document.ModifiedAt = DateTime.Now;
                }

                if (!string.IsNullOrEmpty(request.FileNote))
                {
                    protocol.Napomena += $"\n\n[{DateTime.Now:dd.MM.yyyy HH:mm}] {request.FileNote}";
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Fajl je uspješno ažuriran" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Greška: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDocument([FromBody] UpdateDocumentRequest request)
        {
            try
            {
                var document = await _context.Documents
                    .FirstOrDefaultAsync(d => d.Id == request.DocumentId);

                if (document == null)
                    return Json(new { success = false, message = "Dokument nije pronađen" });

                if (!string.IsNullOrEmpty(request.Title))
                {
                    document.Title = request.Title;
                    document.FileName = request.Title + document.FileExtension;
                }

                if (!string.IsNullOrEmpty(request.Status) &&
                    Enum.TryParse<DocumentStatus>(request.Status, out var newStatus))
                {
                    document.Status = newStatus;
                }

                document.ModifiedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Dokument je uspešno ažuriran",
                    document = new
                    {
                        title = document.Title,
                        fileName = document.FileName,
                        status = document.Status.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Greška: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddProtocolNote([FromBody] AddProtocolNoteRequest request)
        {
            try
            {
                var protocol = await _context.ProtocolEntries
                    .FirstOrDefaultAsync(p => p.Id == request.ProtocolId);

                if (protocol == null)
                    return Json(new { success = false, message = "Protokol nije pronađen" });

                if (!string.IsNullOrEmpty(protocol.Napomena))
                {
                    protocol.Napomena += $"\n\n[{DateTime.Now:dd.MM.yyyy HH:mm}] {request.Note}";
                }
                else
                {
                    protocol.Napomena = $"[{DateTime.Now:dd.MM.yyyy HH:mm}] {request.Note}";
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Napomena je dodana" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Greška: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProtocolInfo(int id)
        {
            var protocol = await _context.ProtocolEntries
                .Include(p => p.Document)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (protocol == null)
                return Json(new { success = false, message = "Protokol nije pronađen" });

            return Json(new
            {
                success = true,
                protocol = new
                {
                    brojProtokola = protocol.BrojProtokola,
                    stranka = protocol.Stranka,
                    datum = protocol.Datum.ToString("dd.MM.yyyy HH:mm"),
                    primalac = protocol.Primalac,
                    status = protocol.Document?.Status.ToString() ?? "Nema fajla"
                }
            });
        }

        // GET: Protocol/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var protocolEntry = await _context.ProtocolEntries
                .Include(p => p.Document)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (protocolEntry == null)
            {
                return NotFound();
            }

            await PrepareViewData();
            return View(protocolEntry);
        }

        // POST: Protocol/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProtocolEntry protocolEntry)
        {
            if (id != protocolEntry.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingEntry = await _context.ProtocolEntries
                        .Include(p => p.Document)
                        .FirstOrDefaultAsync(p => p.Id == id);

                    if (existingEntry == null)
                    {
                        return NotFound();
                    }

                    existingEntry.Stranka = protocolEntry.Stranka;
                    existingEntry.Primalac = protocolEntry.Primalac;
                    existingEntry.Napomena = protocolEntry.Napomena;
                    existingEntry.Hitno = protocolEntry.Hitno;
                    existingEntry.VrstaPostupka = protocolEntry.VrstaPostupka;
                    existingEntry.Dostavio = protocolEntry.Dostavio;
                    existingEntry.Telefon = protocolEntry.Telefon;
                    existingEntry.Email = protocolEntry.Email;
                    existingEntry.Adresa = protocolEntry.Adresa;
                    existingEntry.RokZaOdgovor = protocolEntry.RokZaOdgovor;
                    existingEntry.ModifiedAt = DateTime.Now;

                    if (protocolEntry.UploadedFile != null && protocolEntry.UploadedFile.Length > 0)
                    {
                        if (existingEntry.Document != null)
                        {
                            var oldFilePath = Path.Combine(_env.WebRootPath, existingEntry.Document.FilePath.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                            _context.Documents.Remove(existingEntry.Document);
                        }

                        var newDocument = await HandleFileUpload(protocolEntry.UploadedFile);
                        if (newDocument != null)
                        {
                            existingEntry.DocumentId = newDocument.Id;
                            existingEntry.OriginalFileName = protocolEntry.UploadedFile.FileName;
                        }
                    }

                    var documentStatus = Request.Form["DocumentStatus"];
                    if (!string.IsNullOrEmpty(documentStatus) && existingEntry.Document != null)
                    {
                        if (Enum.TryParse<DocumentStatus>(documentStatus, out var newStatus))
                        {
                            existingEntry.Document.Status = newStatus;
                            existingEntry.Document.ModifiedAt = DateTime.Now;
                        }
                    }

                    _context.Update(existingEntry);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Protokol uspješno ažuriran!";
                    return RedirectToAction(nameof(Details), new { id = existingEntry.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProtocolEntryExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Došlo je do greške pri ažuriranju protokola: " + ex.Message);
                }
            }

            await PrepareViewData();
            return View(protocolEntry);
        }

        private bool ProtocolEntryExists(int id)
        {
            return _context.ProtocolEntries.Any(e => e.Id == id);
        }

        // GET: Protocol/QuickEdit/5
        [HttpGet]
        public async Task<IActionResult> QuickEdit(int id)
        {
            var protocolEntry = await _context.ProtocolEntries
                .Include(p => p.Document)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (protocolEntry == null)
            {
                return Json(new { success = false, message = "Protokol nije pronađen" });
            }

            return Json(new
            {
                success = true,
                protocol = new
                {
                    id = protocolEntry.Id,
                    brojProtokola = protocolEntry.BrojProtokola,
                    stranka = protocolEntry.Stranka,
                    primalac = protocolEntry.Primalac,
                    napomena = protocolEntry.Napomena,
                    hitno = protocolEntry.Hitno,
                    vrstaPostupka = protocolEntry.VrstaPostupka,
                    dostavio = protocolEntry.Dostavio,
                    telefon = protocolEntry.Telefon,
                    email = protocolEntry.Email,
                    adresa = protocolEntry.Adresa,
                    rokZaOdgovor = protocolEntry.RokZaOdgovor?.ToString("yyyy-MM-dd"),
                    documentStatus = protocolEntry.Document?.Status.ToString(),
                    documentTitle = protocolEntry.Document?.Title
                }
            });
        }

        // POST: Protocol/QuickEdit
        [HttpPost]
        public async Task<IActionResult> QuickEdit([FromBody] QuickEditRequest request)
        {
            try
            {
                var protocolEntry = await _context.ProtocolEntries
                    .Include(p => p.Document)
                    .FirstOrDefaultAsync(p => p.Id == request.Id);

                if (protocolEntry == null)
                {
                    return Json(new { success = false, message = "Protokol nije pronađen" });
                }

                protocolEntry.Stranka = request.Stranka;
                protocolEntry.Primalac = request.Primalac;
                protocolEntry.Napomena = request.Napomena;
                protocolEntry.Hitno = request.Hitno;
                protocolEntry.VrstaPostupka = request.VrstaPostupka;
                protocolEntry.Dostavio = request.Dostavio;
                protocolEntry.Telefon = request.Telefon;
                protocolEntry.Email = request.Email;
                protocolEntry.Adresa = request.Adresa;
                protocolEntry.RokZaOdgovor = string.IsNullOrEmpty(request.RokZaOdgovor) ?
                    (DateTime?)null : DateTime.Parse(request.RokZaOdgovor);
                protocolEntry.ModifiedAt = DateTime.Now;

                if (!string.IsNullOrEmpty(request.DocumentStatus) && protocolEntry.Document != null)
                {
                    if (Enum.TryParse<DocumentStatus>(request.DocumentStatus, out var newStatus))
                    {
                        protocolEntry.Document.Status = newStatus;
                        protocolEntry.Document.ModifiedAt = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Protokol uspješno ažuriran" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Greška: {ex.Message}" });
            }
        }

        // POST: Protocol/ReplaceDocument/5
        [HttpPost]
        public async Task<IActionResult> ReplaceDocument(int id, IFormFile newFile)
        {
            try
            {
                var protocolEntry = await _context.ProtocolEntries
                    .Include(p => p.Document)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (protocolEntry == null)
                {
                    return Json(new { success = false, message = "Protokol nije pronađen" });
                }

                if (newFile == null || newFile.Length == 0)
                {
                    return Json(new { success = false, message = "Nije odabran novi fajl" });
                }

                if (protocolEntry.Document != null)
                {
                    var oldFilePath = Path.Combine(_env.WebRootPath, protocolEntry.Document.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }

                    _context.Documents.Remove(protocolEntry.Document);
                }

                var newDocument = await HandleFileUpload(newFile);
                if (newDocument != null)
                {
                    protocolEntry.DocumentId = newDocument.Id;
                    protocolEntry.OriginalFileName = newFile.FileName;
                    protocolEntry.ModifiedAt = DateTime.Now;

                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Dokument uspješno zamijenjen" });
                }

                return Json(new { success = false, message = "Greška pri uploadu fajla" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Greška: {ex.Message}" });
            }
        }

        // POST: Protocol/BulkUpdateStatus
        [HttpPost]
        public async Task<IActionResult> BulkUpdateStatus([FromBody] BulkUpdateRequest request)
        {
            try
            {
                var protocols = await _context.ProtocolEntries
                    .Include(p => p.Document)
                    .Where(p => request.ProtocolIds.Contains(p.Id))
                    .ToListAsync();

                if (!protocols.Any())
                {
                    return Json(new { success = false, message = "Nije pronađen nijedan protokol" });
                }

                if (!Enum.TryParse<DocumentStatus>(request.NewStatus, out var newStatus))
                {
                    return Json(new { success = false, message = "Nevalidan status" });
                }

                foreach (var protocol in protocols)
                {
                    protocol.ModifiedAt = DateTime.Now;

                    if (protocol.Document != null)
                    {
                        protocol.Document.Status = newStatus;
                        protocol.Document.ModifiedAt = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Status ažuriran za {protocols.Count} protokola",
                    updatedCount = protocols.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Greška: {ex.Message}" });
            }
        }

        // POST: Protocol/BulkAddNote
        [HttpPost]
        public async Task<IActionResult> BulkAddNote([FromBody] BulkNoteRequest request)
        {
            try
            {
                var protocols = await _context.ProtocolEntries
                    .Where(p => request.ProtocolIds.Contains(p.Id))
                    .ToListAsync();

                if (!protocols.Any())
                {
                    return Json(new { success = false, message = "Nije pronađen nijedan protokol" });
                }

                var timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                var noteText = $"[{timestamp}] {request.Note}";

                foreach (var protocol in protocols)
                {
                    protocol.ModifiedAt = DateTime.Now;

                    if (!string.IsNullOrEmpty(protocol.Napomena))
                    {
                        protocol.Napomena += $"\n\n{noteText}";
                    }
                    else
                    {
                        protocol.Napomena = noteText;
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Napomena dodana za {protocols.Count} protokola",
                    updatedCount = protocols.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Greška: {ex.Message}" });
            }
        }

        public async Task<IActionResult> GenerateReport(
    DateTime? dateFrom = null,
    DateTime? dateTo = null,
    string format = "pdf",
    string status = null,
    string vrstaPostupka = null,
    string selectedIds = null)
        {
            try
            {
                var query = _context.ProtocolEntries
                    .Include(p => p.Document)
                    .ThenInclude(d => d.Folder)
                    .AsQueryable();

                var currentUserId = GetCurrentUserId();
                var isAdmin = User.IsInRole("Admin");

                if (!isAdmin && currentUserId.HasValue)
                    query = query.Where(p => p.Document != null && p.Document.UserId == currentUserId.Value);

                if (!string.IsNullOrEmpty(selectedIds))
                {
                    var ids = selectedIds.Split(',').Select(int.Parse).ToList();
                    query = query.Where(p => ids.Contains(p.Id));
                }
                else
                {
                    if (dateFrom.HasValue)
                        query = query.Where(p => p.Datum >= dateFrom.Value);

                    if (dateTo.HasValue)
                        query = query.Where(p => p.Datum <= dateTo.Value.AddDays(1));

                    if (!string.IsNullOrEmpty(status) && Enum.TryParse<DocumentStatus>(status, out var documentStatus))
                        query = query.Where(p => p.Document != null && p.Document.Status == documentStatus);

                    if (!string.IsNullOrEmpty(vrstaPostupka))
                        query = query.Where(p => p.VrstaPostupka == vrstaPostupka);
                }

                var entries = await query.OrderBy(p => p.BrojProtokola).ToListAsync();

                return format.Equals("excel", StringComparison.OrdinalIgnoreCase)
                    ? GenerateExcelReport(entries, dateFrom, dateTo, status, vrstaPostupka)
                    : GeneratePdfReport(entries, dateFrom, dateTo, status, vrstaPostupka);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Greška pri generisanju izvještaja: {ex.Message}");
            }
        }


        private SelectList GetVrstaPostupkaSelectList()
        {
            var vrstePostupaka = new[]
            {
        new { Value = "Administrativni", Text = "Administrativni" },
        new { Value = "Pravni", Text = "Pravni" },
        new { Value = "Finansijski", Text = "Finansijski" },
        new { Value = "Kadrovski", Text = "Kadrovski" },
        new { Value = "Tehnički", Text = "Tehnički" },
        new { Value = "Ostalo", Text = "Ostalo" }
    };

            return new SelectList(vrstePostupaka, "Value", "Text");
        }

        
         private IActionResult GenerateExcelReport(List<ProtocolEntry> entries, DateTime? dateFrom, DateTime? dateTo, string status = null, string vrstaPostupka = null)
        {
            
            try
            {
                using (var package = new OfficeOpenXml.ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Protokol Izvještaj");

                    // Set up worksheet styling
                    worksheet.View.ShowGridLines = false;
                    worksheet.DefaultColWidth = 12;

                    // Company header
                    worksheet.Cells[1, 1].Value = "ePisarnica - Sistem Upravljanja Dokumentima";
                    worksheet.Cells[1, 1, 1, 10].Merge = true;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Size = 14;
                    worksheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(51, 51, 51));
                    worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;

                    // Date generated
                    worksheet.Cells[1, 11].Value = $"Generisan: {DateTime.Now:dd.MM.yyyy HH:mm}";
                    worksheet.Cells[1, 11].Style.Font.Size = 10;
                    worksheet.Cells[1, 11].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
                    worksheet.Cells[1, 11].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                    // Main title
                    worksheet.Cells[3, 1].Value = "IZVJEŠTAJ PROTOKOLA";
                    worksheet.Cells[3, 1, 3, 11].Merge = true;
                    worksheet.Cells[3, 1].Style.Font.Bold = true;
                    worksheet.Cells[3, 1].Style.Font.Size = 20;
                    worksheet.Cells[3, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(41, 128, 185));
                    worksheet.Cells[3, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    // Summary section
                    int currentRow = 5;
                    worksheet.Cells[currentRow, 1].Value = "📊 PREGLED IZVJEŠTAJA";
                    worksheet.Cells[currentRow, 1, currentRow, 11].Merge = true;
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
                    worksheet.Cells[currentRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(52, 73, 94));

                    // Summary background
                    worksheet.Cells[currentRow, 1, currentRow + 10, 11].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[currentRow, 1, currentRow + 10, 11].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(248, 249, 250));
                    worksheet.Cells[currentRow, 1, currentRow + 10, 11].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, System.Drawing.Color.FromArgb(222, 226, 230));

                    currentRow++;
                    worksheet.Cells[currentRow, 2].Value = $"Ukupno zapisa: {entries.Count}";
                    currentRow++;

                    if (dateFrom.HasValue || dateTo.HasValue)
                    {
                        worksheet.Cells[currentRow, 2].Value = $"Period: {(dateFrom.HasValue ? dateFrom.Value.ToString("dd.MM.yyyy") : "Početak")} - {(dateTo.HasValue ? dateTo.Value.ToString("dd.MM.yyyy") : "Kraj")}";
                        currentRow++;
                    }

                    if (!string.IsNullOrEmpty(status))
                    {
                        worksheet.Cells[currentRow, 2].Value = $"Filtrirano po statusu: {status}";
                        currentRow++;
                    }

                    if (!string.IsNullOrEmpty(vrstaPostupka))
                    {
                        worksheet.Cells[currentRow, 2].Value = $"Filtrirano po vrsti postupka: {vrstaPostupka}";
                        currentRow++;
                    }

                    if (entries.Any())
                    {
                        var urgentCount = entries.Count(e => !string.IsNullOrEmpty(e.Hitno) && e.Hitno.ToLower() == "da");
                        worksheet.Cells[currentRow, 2].Value = $"Hitni predmeti: {urgentCount}";
                        currentRow++;

                        var totalByStatus = entries.Where(e => e.Document != null).GroupBy(e => e.Document.Status).ToDictionary(g => g.Key, g => g.Count());
                        worksheet.Cells[currentRow, 2].Value = "Raspodjela po statusima:";
                        currentRow++;

                        foreach (var statusGroup in totalByStatus)
                        {
                            worksheet.Cells[currentRow, 3].Value = $"• {statusGroup.Key}: {statusGroup.Value}";
                            currentRow++;
                        }
                    }

                    currentRow += 2;

                    if (!entries.Any())
                    {
                        worksheet.Cells[currentRow, 1].Value = "Nema podataka za prikaz na osnovu zadatih kriterija.";
                        worksheet.Cells[currentRow, 1, currentRow, 11].Merge = true;
                        worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
                        worksheet.Cells[currentRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
                        worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    }
                    else
                    {
                        var headers = new[]
                        {
                    "Broj Protokola", "Datum", "Stranka", "Primalac", "Vrsta Postupka",
                    "Hitno", "Status Dokumenta", "Napomena", "Dostavio", "Telefon", "QR Kod"
                };

                        for (int i = 0; i < headers.Length; i++)
                        {
                            var headerCell = worksheet.Cells[currentRow, i + 1];
                            headerCell.Value = headers[i];
                            headerCell.Style.Font.Bold = true;
                            headerCell.Style.Font.Size = 11;
                            headerCell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                            headerCell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            headerCell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(52, 73, 94));
                            headerCell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, System.Drawing.Color.White);
                            headerCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            headerCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                        }

                        currentRow++;

                        foreach (var entry in entries.OrderBy(e => e.BrojProtokola))
                        {
                            var isUrgent = !string.IsNullOrEmpty(entry.Hitno) && entry.Hitno.ToLower() == "da";
                            var rowRange = worksheet.Cells[currentRow, 1, currentRow, 11];

                            if (isUrgent)
                            {
                                rowRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 243, 224));
                            }
                            else if (currentRow % 2 == 0)
                            {
                                rowRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(249, 249, 249)); 
                            }

                            rowRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, System.Drawing.Color.FromArgb(222, 226, 230));

                            worksheet.Cells[currentRow, 1].Value = entry.BrojProtokola;
                            worksheet.Cells[currentRow, 2].Value = entry.Datum.ToString("dd.MM.yyyy HH:mm");
                            worksheet.Cells[currentRow, 3].Value = entry.Stranka ?? "—";
                            worksheet.Cells[currentRow, 4].Value = entry.Primalac ?? "—";
                            worksheet.Cells[currentRow, 5].Value = entry.VrstaPostupka ?? "—";

                            var urgentCell = worksheet.Cells[currentRow, 6];
                            urgentCell.Value = entry.Hitno ?? "—";
                            if (isUrgent)
                            {
                                urgentCell.Style.Font.Bold = true;
                                urgentCell.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(220, 53, 69));
                            }

                            worksheet.Cells[currentRow, 7].Value = entry.Document?.Status.ToString() ?? "Bez dokumenta";
                            worksheet.Cells[currentRow, 8].Value = entry.Napomena ?? "—";
                            worksheet.Cells[currentRow, 9].Value = entry.Dostavio ?? "—";
                            worksheet.Cells[currentRow, 10].Value = entry.Telefon ?? "—";
                            worksheet.Cells[currentRow, 11].Value = !string.IsNullOrEmpty(entry.QrCodePath) ? "Dostupan" : "—";

                            currentRow++;
                        }

                        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                        for (int col = 1; col <= 11; col++)
                        {
                            if (worksheet.Column(col).Width < 10)
                                worksheet.Column(col).Width = 10;
                            if (worksheet.Column(col).Width > 40)
                                worksheet.Column(col).Width = 40;
                        }

                        currentRow += 2;
                        worksheet.Cells[currentRow, 1].Value = $"🔸 Legenda: Označene su hitne stavke narančastom bojom • Izvještaj kreiran {DateTime.Now:dd.MM.yyyy u HH:mm} • ePisarnica v2.0";
                        worksheet.Cells[currentRow, 1, currentRow, 11].Merge = true;
                        worksheet.Cells[currentRow, 1].Style.Font.Size = 9;
                        worksheet.Cells[currentRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
                        worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                    }

                    if (entries.Any())
                    {
                        worksheet.View.FreezePanes(currentRow - entries.Count(), 1);
                    }

                    var fileName = $"Protokol_Izvjestaj_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    return File(package.GetAsByteArray(),
                               "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                               fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Greška pri generisanju Excel izvještaja: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        


        // GET: Protocol/Print/5
        public async Task<IActionResult> Print(int id)
        {
            var protocolEntry = await _context.ProtocolEntries
                .Include(p => p.Document)
                .ThenInclude(d => d.Folder)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (protocolEntry == null)
            {
                return NotFound();
            }

            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    var document = new iTextSharpText.Document(iTextSharpText.PageSize.A4, 20, 20, 30, 30);
                    var writer = iTextSharpTextPdf.PdfWriter.GetInstance(document, memoryStream);

                    document.Open();

                    
                    var headerTable = new iTextSharpTextPdf.PdfPTable(2);
                    headerTable.WidthPercentage = 100;
                    headerTable.SetWidths(new float[] { 3, 1 }); 
                   
                    var leftCell = new iTextSharpTextPdf.PdfPCell();
                    leftCell.Border = iTextSharpText.Rectangle.NO_BORDER;
                    leftCell.Padding = 0;

                   
                    var titleFont = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA_BOLD, 18, iTextSharpText.BaseColor.DARK_GRAY);
                    var title = new iTextSharpText.Paragraph("PROTOKOL PRIMOPREDAJE", titleFont);
                    title.Alignment = iTextSharpText.Element.ALIGN_LEFT;
                    title.SpacingAfter = 10;

                   
                    var headerFont = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA_BOLD, 12);
                    var infoFont = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA, 11);

                    var protocolInfo = new iTextSharpText.Paragraph();
                    protocolInfo.Add(new iTextSharpText.Chunk("Broj protokola: ", headerFont));
                    protocolInfo.Add(new iTextSharpText.Chunk(protocolEntry.BrojProtokola.ToString(), infoFont));
                    protocolInfo.Add(new iTextSharpText.Chunk("\nDatum: ", headerFont));
                    protocolInfo.Add(new iTextSharpText.Chunk(protocolEntry.Datum.ToString("dd.MM.yyyy HH:mm"), infoFont));

                    leftCell.AddElement(title);
                    leftCell.AddElement(protocolInfo);

                  
                    var rightCell = new iTextSharpTextPdf.PdfPCell();
                    rightCell.Border = iTextSharpText.Rectangle.NO_BORDER;
                    rightCell.HorizontalAlignment = iTextSharpText.Element.ALIGN_RIGHT;
                    rightCell.VerticalAlignment = iTextSharpText.Element.ALIGN_TOP;
                    rightCell.Padding = 0;

                    if (!string.IsNullOrEmpty(protocolEntry.QrCodePath))
                    {
                        try
                        {
                            var qrCodePath = Path.Combine(_env.WebRootPath, protocolEntry.QrCodePath.TrimStart('/'));
                            if (System.IO.File.Exists(qrCodePath))
                            {
                                var qrImage = iTextSharpText.Image.GetInstance(qrCodePath);
                                qrImage.ScaleToFit(80, 80);
                                qrImage.Alignment = iTextSharpText.Element.ALIGN_RIGHT;
                                rightCell.AddElement(qrImage);
                            }
                            else
                            {
                                rightCell.AddElement(new iTextSharpText.Paragraph("QR kod\nnije dostupan", infoFont));
                            }
                        }
                        catch
                        {
                            rightCell.AddElement(new iTextSharpText.Paragraph("QR kod\nnije dostupan", infoFont));
                        }
                    }
                    else
                    {
                        rightCell.AddElement(new iTextSharpText.Paragraph("QR kod\nnije dostupan", infoFont));
                    }

                    headerTable.AddCell(leftCell);
                    headerTable.AddCell(rightCell);
                    document.Add(headerTable);

                    document.Add(new iTextSharpText.Paragraph(" "));
                    document.Add(new iTextSharpText.Paragraph(" "));

                    var infoTable = new iTextSharpTextPdf.PdfPTable(2);
                    infoTable.WidthPercentage = 100;
                    infoTable.SetWidths(new float[] { 1, 3 });

                    AddInfoRow(infoTable, "Stranka:", protocolEntry.Stranka, headerFont, infoFont);
                    AddInfoRow(infoTable, "Primalac:", protocolEntry.Primalac ?? "-", headerFont, infoFont);
                    AddInfoRow(infoTable, "Dostavio:", protocolEntry.Dostavio ?? "-", headerFont, infoFont);
                    AddInfoRow(infoTable, "Vrsta postupka:", protocolEntry.VrstaPostupka ?? "-", headerFont, infoFont);
                    AddInfoRow(infoTable, "Hitno:", protocolEntry.Hitno ?? "-", headerFont, infoFont);

                    if (protocolEntry.RokZaOdgovor.HasValue)
                    {
                        AddInfoRow(infoTable, "Rok za odgovor:", protocolEntry.RokZaOdgovor.Value.ToString("dd.MM.yyyy"), headerFont, infoFont);
                    }

                    document.Add(infoTable);
                    document.Add(new iTextSharpText.Paragraph(" "));

                    if (!string.IsNullOrEmpty(protocolEntry.Telefon) || !string.IsNullOrEmpty(protocolEntry.Email) || !string.IsNullOrEmpty(protocolEntry.Adresa))
                    {
                        var contactHeader = new iTextSharpText.Paragraph("KONTAKT INFORMACIJE:", headerFont);
                        contactHeader.SpacingBefore = 15;
                        contactHeader.SpacingAfter = 5;
                        document.Add(contactHeader);

                        var contactTable = new iTextSharpTextPdf.PdfPTable(2);
                        contactTable.WidthPercentage = 100;
                        contactTable.SetWidths(new float[] { 1, 3 });

                        if (!string.IsNullOrEmpty(protocolEntry.Telefon))
                            AddInfoRow(contactTable, "Telefon:", protocolEntry.Telefon, headerFont, infoFont);
                        if (!string.IsNullOrEmpty(protocolEntry.Email))
                            AddInfoRow(contactTable, "Email:", protocolEntry.Email, headerFont, infoFont);
                        if (!string.IsNullOrEmpty(protocolEntry.Adresa))
                            AddInfoRow(contactTable, "Adresa:", protocolEntry.Adresa, headerFont, infoFont);

                        document.Add(contactTable);
                        document.Add(new iTextSharpText.Paragraph(" "));
                    }

                    if (protocolEntry.Document != null)
                    {
                        var docHeader = new iTextSharpText.Paragraph("INFORMACIJE O DOKUMENTU:", headerFont);
                        docHeader.SpacingBefore = 15;
                        docHeader.SpacingAfter = 5;
                        document.Add(docHeader);

                        var docTable = new iTextSharpTextPdf.PdfPTable(2);
                        docTable.WidthPercentage = 100;
                        docTable.SetWidths(new float[] { 1, 3 });

                        AddInfoRow(docTable, "Naziv dokumenta:", protocolEntry.Document.Title, headerFont, infoFont);
                        AddInfoRow(docTable, "Originalni naziv:", protocolEntry.OriginalFileName ?? protocolEntry.Document.FileName, headerFont, infoFont);
                        AddInfoRow(docTable, "Status:", protocolEntry.Document.Status.ToString(), headerFont, infoFont);
                        AddInfoRow(docTable, "Veličina:", FormatFileSize(protocolEntry.Document.FileSize), headerFont, infoFont);
                        AddInfoRow(docTable, "Ekstenzija:", protocolEntry.Document.FileExtension, headerFont, infoFont);

                        document.Add(docTable);
                        document.Add(new iTextSharpText.Paragraph(" "));
                    }

                    if (!string.IsNullOrEmpty(protocolEntry.Napomena))
                    {
                        var notesHeader = new iTextSharpText.Paragraph("NAPOMENA:", headerFont);
                        notesHeader.SpacingBefore = 15;
                        notesHeader.SpacingAfter = 5;
                        document.Add(notesHeader);

                        var notes = new iTextSharpText.Paragraph(protocolEntry.Napomena, infoFont);
                        notes.Alignment = iTextSharpText.Element.ALIGN_JUSTIFIED;
                        document.Add(notes);
                        document.Add(new iTextSharpText.Paragraph(" "));
                    }

                    var footer = new iTextSharpText.Paragraph();
                    footer.SpacingBefore = 30;
                    footer.Alignment = iTextSharpText.Element.ALIGN_RIGHT;
                    footer.Font = iTextSharpText.FontFactory.GetFont(iTextSharpText.FontFactory.HELVETICA_OBLIQUE, 9);
                    footer.Add(new iTextSharpText.Chunk($"Štampano: {DateTime.Now:dd.MM.yyyy HH:mm}"));
                    document.Add(footer);

                    document.Close();

                    var fileName = $"Protokol_{protocolEntry.BrojProtokola}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    return File(memoryStream.ToArray(), "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Greška pri generisanju štampane verzije: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // Helper method for info rows (keep this the same)
        private void AddInfoRow(iTextSharpTextPdf.PdfPTable table, string label, string value, iTextSharpText.Font labelFont, iTextSharpText.Font valueFont)
        {
            var labelCell = new iTextSharpTextPdf.PdfPCell(new iTextSharpText.Phrase(label, labelFont));
            labelCell.Border = iTextSharpText.Rectangle.NO_BORDER;
            labelCell.BackgroundColor = new iTextSharpText.BaseColor(240, 240, 240);
            labelCell.Padding = 8;
            labelCell.PaddingLeft = 12;
            table.AddCell(labelCell);

            var valueCell = new iTextSharpTextPdf.PdfPCell(new iTextSharpText.Phrase(value, valueFont));
            valueCell.Border = iTextSharpText.Rectangle.NO_BORDER;
            valueCell.Padding = 8;
            table.AddCell(valueCell);
        }

        // Helper method to format file size (keep this the same)
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }


        // GET: Protocol/5/Assignments
        [HttpGet("{id}/Assignments")]
        public async Task<IActionResult> GetProtocolAssignments(int id)
        {
            var assignments = await _context.Assignments
                .Include(a => a.DodijeljenOdjel)
                .Include(a => a.DodijeljenUser)
                .Where(a => a.ProtocolEntryId == id)
                .OrderByDescending(a => a.DatumDodjele)
                .ToListAsync();

            return Ok(assignments);
        }

        // GET: Protocol/Assignments/Active
        [HttpGet("Assignments/Active")]
        public async Task<IActionResult> GetActiveAssignments()
        {
            var assignments = await _context.Assignments
                .Include(a => a.ProtocolEntry)
                .Include(a => a.DodijeljenOdjel)
                .Include(a => a.DodijeljenUser)
                .Where(a => a.Status == "Aktivno")
                .OrderByDescending(a => a.DatumDodjele)
                .ToListAsync();

            return Ok(assignments);
        }

        [HttpGet]
        public async Task<IActionResult> TestEmail()
        {
            await _emailService.SendEmailAsync("farex.uhc@gmail.com", "Test mail", "<p>Ovo je test!</p>");
            return Ok("Poslano");
        }

        private bool IsUserAdmin()
        {
            return User.IsInRole("Admin");
        }





        // ========== REQUEST MODEL CLASSES ==========

        public class QuickEditRequest
        {
            public int Id { get; set; }
            public string Stranka { get; set; }
            public string Primalac { get; set; }
            public string Napomena { get; set; }
            public string Hitno { get; set; }
            public string VrstaPostupka { get; set; }
            public string Dostavio { get; set; }
            public string Telefon { get; set; }
            public string Email { get; set; }
            public string Adresa { get; set; }
            public string RokZaOdgovor { get; set; }
            public string DocumentStatus { get; set; }
        }

        public class BulkUpdateRequest
        {
            public List<int> ProtocolIds { get; set; }
            public string NewStatus { get; set; }
        }

        public class BulkNoteRequest
        {
            public List<int> ProtocolIds { get; set; }
            public string Note { get; set; }
        }

        public class UpdateFileRequest
        {
            public int ProtocolId { get; set; }
            public int DocumentId { get; set; }
            public string NewStatus { get; set; }
            public string FileName { get; set; }
            public string FileNote { get; set; }
        }

        public class UpdateDocumentRequest
        {
            public int DocumentId { get; set; }
            public string Title { get; set; }
            public string Status { get; set; }
        }

        public class AddProtocolNoteRequest
        {
            public int ProtocolId { get; set; }
            public string Note { get; set; }
        }

        private SelectList GetStatusSelectList()
        {
            var statuses = Enum.GetValues<DocumentStatus>()
                .Select(s => new SelectListItem
                {
                    Value = s.ToString(),
                    Text = GetStatusDisplayName(s)
                })
                .ToList();

            statuses.Insert(0, new SelectListItem { Value = "", Text = "Svi statusi" });

            return new SelectList(statuses, "Value", "Text");
        }

        private string GetStatusDisplayName(DocumentStatus status)
        {
            return status switch
            {
                DocumentStatus.Zaprimljeno => "Zaprimljeno",
                DocumentStatus.Uvid => "Na uvidu",
                DocumentStatus.UObradi => "U obradi",
                DocumentStatus.NaDopuni => "Na dopuni",
                DocumentStatus.Recenzija => "Recenzija",
                DocumentStatus.Odobreno => "Odobreno",
                DocumentStatus.Odbijeno => "Odbijeno",
                DocumentStatus.Arhivirano => "Arhivirano",
                DocumentStatus.IsTrashed => "Obrisano",

                _ => status.ToString()
            };
        }

        public class UpdateStatusRequest
        {
            public int ProtocolId { get; set; }
            public string NewStatus { get; set; }
        }

        public class AddNoteRequest
        {
            public int ProtocolId { get; set; }
            public string Note { get; set; }
        }
    }
}
