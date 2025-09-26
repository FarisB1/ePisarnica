namespace ePisarnica.Models
{
    public class SignPdfRequest
    {
        public int DocumentId { get; set; }
        public string SignatureData { get; set; } // Base64 encoded signature image
        public string Reason { get; set; }
        public string Location { get; set; }
    }
}