using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using System.IO;
using System.Collections.Generic;

namespace ePisarnica.Services
{
    public interface IPDFSignatureDetectionService
    {
        bool HasDigitalSignature(string filePath);
        List<PDFSignatureInfo> GetSignatureInfo(string filePath);
    }

    public class PDFSignatureDetectionService : IPDFSignatureDetectionService
    {
        public bool HasDigitalSignature(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                using (var reader = new PdfReader(filePath))
                {
                    AcroFields af = reader.AcroFields;
                    var signatureNames = af.GetSignatureNames();

                    return signatureNames.Count > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public List<PDFSignatureInfo> GetSignatureInfo(string filePath)
        {
            var signatures = new List<PDFSignatureInfo>();

            try
            {
                if (!File.Exists(filePath))
                    return signatures;

                using (var reader = new PdfReader(filePath))
                {
                    AcroFields af = reader.AcroFields;
                    var signatureNames = af.GetSignatureNames();

                    foreach (string name in signatureNames)
                    {
                        try
                        {
                            PdfPKCS7 pkcs7 = af.VerifySignature(name);
                            var signature = new PDFSignatureInfo
                            {
                                SignerName = pkcs7.SignName,
                                SigningTime = pkcs7.SignDate,
                                Reason = GetFieldProperty(af, name, "Reason"),
                                Location = GetFieldProperty(af, name, "Location"),
                                IsValid = pkcs7.Verify(),
                                SignatureName = name,
                                CertificateSubject = pkcs7.SigningCertificate?.SubjectDN?.ToString()
                            };

                            signatures.Add(signature);
                        }
                        catch
                        {
                            // Skip invalid signatures
                            signatures.Add(new PDFSignatureInfo
                            {
                                SignatureName = name,
                                IsValid = false,
                                Error = "Cannot verify signature"
                            });
                        }
                    }
                }
            }
            catch
            {
                
            }

            return signatures;
        }

        private string GetFieldProperty(AcroFields af, string fieldName, string propertyName)
        {
            try
            {
                
                AcroFields.Item fieldItem = af.GetFieldItem(fieldName);
                if (fieldItem != null)
                {
                    
                    PdfDictionary fieldDict = fieldItem.GetMerged(0);
                    if (fieldDict != null)
                    {
                       
                        PdfName pdfPropertyName = new PdfName(propertyName);

                        
                        if (fieldDict.Get(pdfPropertyName) != null)
                        {
                           
                            PdfObject propertyValue = fieldDict.Get(pdfPropertyName);

                            if (propertyValue is PdfString pdfString)
                            {
                                return pdfString.ToString();
                            }
                            else if (propertyValue is PdfName pdfName)
                            {
                                return pdfName.ToString();
                            }
                            else if (propertyValue is PdfLiteral pdfLiteral)
                            {
                                return pdfLiteral.ToString();
                            }

                            return propertyValue?.ToString();
                        }
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    public class PDFSignatureInfo
    {
        public string SignatureName { get; set; }
        public string SignerName { get; set; }
        public DateTime SigningTime { get; set; }
        public string Reason { get; set; }
        public string Location { get; set; }
        public bool IsValid { get; set; }
        public string CertificateSubject { get; set; }
        public string Error { get; set; }
    }
}