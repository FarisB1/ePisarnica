// PDFSigningService.cs
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using iTextSharp.text;
using ePisarnica.Helpers;

namespace ePisarnica.Services
{
    public interface IPDFSigningService
    {
        byte[] SignPdf(byte[] pdfData, string signatureImageBase64, string reason, string location, 
                      string certificatePath, string certificatePassword);
        
        bool ValidateSignature(byte[] pdfData);
    }

    public class PDFSigningService : IPDFSigningService
    {
        public byte[] SignPdf(byte[] pdfData, string signatureImageBase64, string reason, string location,
                      string certificatePath, string certificatePassword)
        {
            string fullCertPath = Path.IsPathRooted(certificatePath)
                ? certificatePath
                : Path.Combine(Directory.GetCurrentDirectory(), certificatePath);

            if (!File.Exists(fullCertPath))
            {
                throw new FileNotFoundException($"Certificate file not found: {fullCertPath}");
            }

            using (var inputStream = new MemoryStream(pdfData))
            using (var outputStream = new MemoryStream())
            {
                PdfReader pdfReader = new PdfReader(inputStream);

                using (FileStream pfxStream = new FileStream(fullCertPath, FileMode.Open, FileAccess.Read))
                {
                    var builder = new Pkcs12StoreBuilder();
                    var pfxKeyStore = builder.Build();
                    pfxKeyStore.Load(pfxStream, certificatePassword.ToCharArray());

                    // Find first key entry
                    string alias = null;
                    foreach (string al in pfxKeyStore.Aliases)
                    {
                        if (pfxKeyStore.IsKeyEntry(al))
                        {
                            alias = al;
                            break;
                        }
                    }

                    if (alias == null)
                    {
                        throw new Exception("Private key not found in PFX file.");
                    }

                    ICipherParameters privateKey = pfxKeyStore.GetKey(alias).Key;
                    ICollection<X509Certificate> certChain = new List<X509Certificate>
            {
                pfxKeyStore.GetCertificate(alias).Certificate
            };

                    // Create signature stamper
                    PdfStamper pdfStamper = PdfStamper.CreateSignature(pdfReader, outputStream, '\0', null, true);
                    PdfSignatureAppearance signatureAppearance = pdfStamper.SignatureAppearance;
                    signatureAppearance.Reason = reason;
                    signatureAppearance.Location = location;

                    // --- Find last text position ---
                    var strategy = new TextLocationStrategy();
                    var parser = new iTextSharp.text.pdf.parser.PdfReaderContentParser(pdfReader);
                    parser.ProcessContent(pdfReader.NumberOfPages, strategy);

                    float baseY = strategy.MinY == float.MaxValue ? 100 : strategy.MinY;
                    float baseX = strategy.MaxX == 0 ? 100 : strategy.MaxX;

                    // --- Define signature rectangle ---
                    float width = 150;
                    float height = 50;

                    float x = baseX - 150;               // right of last text
                    float y = baseY - (height + 10);     // just below text

                    Rectangle rect = new Rectangle(x, y, x + width, y + height);

                    // --- Draw white background ---
                    PdfContentByte canvas = pdfStamper.GetUnderContent(pdfReader.NumberOfPages);
                    canvas.SaveState();
                    canvas.SetColorFill(BaseColor.WHITE);
                    canvas.Rectangle(rect.Left, rect.Bottom, rect.Width, rect.Height);
                    canvas.Fill();
                    canvas.RestoreState();

                    // --- Add signature image if provided ---
                    if (!string.IsNullOrEmpty(signatureImageBase64))
                    {
                        try
                        {
                            var signatureImageBytes = Convert.FromBase64String(signatureImageBase64.Split(',')[1]);
                            using (var imageStream = new MemoryStream(signatureImageBytes))
                            {
                                iTextSharp.text.Image signatureImg = iTextSharp.text.Image.GetInstance(imageStream);
                                signatureImg.ScaleToFit(width, height);
                                signatureAppearance.SignatureGraphic = signatureImg;
                                signatureAppearance.SignatureRenderingMode = PdfSignatureAppearance.RenderingMode.GRAPHIC_AND_DESCRIPTION;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Could not add signature image: {ex.Message}");
                        }
                    }

                    // --- Apply visible signature ---
                    signatureAppearance.SetVisibleSignature(rect, pdfReader.NumberOfPages, "digital_signature");

                    IExternalSignature pks = new PrivateKeySignature(privateKey, DigestAlgorithms.SHA256);
                    MakeSignature.SignDetached(
                        signatureAppearance,
                        pks,
                        certChain,
                        null,
                        null,
                        null,
                        0,
                        CryptoStandard.CMS
                    );

                    pdfStamper.Close();
                }

                pdfReader.Close();
                return outputStream.ToArray();
            }
        }




        public bool ValidateSignature(byte[] pdfData)
        {
            try
            {
                using (var stream = new MemoryStream(pdfData))
                {
                    PdfReader reader = new PdfReader(stream);
                    
                    AcroFields af = reader.AcroFields;
                    var names = af.GetSignatureNames();
                    
                    if (names.Count == 0)
                    {
                        return false;
                    }

                    foreach (string name in names)
                    {
                        PdfPKCS7 pkcs7 = af.VerifySignature(name);
                        if (!pkcs7.Verify())
                        {
                            return false;
                        }
                    }
                    
                    return true; 
                }
            }
            catch
            {
                return false;
            }
        }
    }
}