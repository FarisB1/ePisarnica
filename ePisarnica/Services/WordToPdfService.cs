using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iTextSharp.text.pdf;
using System.IO;
using System.Threading.Tasks;

namespace ePisarnica.Services
{
    public class WordToPdfService : IWordToPdfService
    {
        public async Task<byte[]> ConvertWordToPdfAsync(byte[] wordBytes)
        {
            try
            {
                using (var wordStream = new MemoryStream(wordBytes))
                using (var pdfStream = new MemoryStream())
                {
                    // Create PDF document using fully qualified names
                    iTextSharp.text.Document pdfDocument = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4);
                    PdfWriter writer = PdfWriter.GetInstance(pdfDocument, pdfStream);
                    pdfDocument.Open();

                    // Open Word document
                    using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(wordStream, false))
                    {
                        Body body = wordDoc.MainDocumentPart.Document.Body;

                        // Extract text from Word document
                        string text = ExtractTextFromBody(body);

                        // Add text to PDF using fully qualified names
                        iTextSharp.text.Font font = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 12);
                        iTextSharp.text.Paragraph paragraph = new iTextSharp.text.Paragraph(text, font);
                        pdfDocument.Add(paragraph);
                    }

                    pdfDocument.Close();
                    return pdfStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error converting Word to PDF: {ex.Message}", ex);
            }
        }

        public bool IsWordDocument(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            string extension = Path.GetExtension(fileName).ToLower();
            return extension == ".doc" || extension == ".docx";
        }

        private string ExtractTextFromBody(Body body)
        {
            var textBuilder = new System.Text.StringBuilder();

            foreach (var element in body.Elements())
            {
                if (element is DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
                {
                    textBuilder.AppendLine(paragraph.InnerText);
                }
                else if (element is DocumentFormat.OpenXml.Wordprocessing.Table table)
                {
                    foreach (var row in table.Elements<TableRow>())
                    {
                        foreach (var cell in row.Elements<TableCell>())
                        {
                            textBuilder.Append(cell.InnerText + "\t");
                        }
                        textBuilder.AppendLine();
                    }
                }
            }

            return textBuilder.ToString();
        }
    }
}