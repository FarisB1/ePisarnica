namespace ePisarnica.Helpers
{
    public class DigitalSignatureDTO
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public int UserId { get; set; }
        public string SignatureData { get; set; }
        public string SignatureHash { get; set; }
        public DateTime SignedAt { get; set; }
        public string Reason { get; set; }
        public string Location { get; set; }
        public bool IsValid { get; set; }
        public DateTime? ValidatedAt { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
    }
}
