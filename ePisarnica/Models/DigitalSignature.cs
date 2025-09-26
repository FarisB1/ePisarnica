using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePisarnica.Models
{
    public class DigitalSignature
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DocumentId { get; set; }

        [ForeignKey("DocumentId")]
        public Document Document { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        [Required]
        public string SignatureData { get; set; } // Base64 encoded signature image or data

        [Required]
        public string SignatureHash { get; set; } // SHA256 hash for verification

        public DateTime SignedAt { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string Reason { get; set; } // Razor potpisivanja

        [MaxLength(500)]
        public string Location { get; set; } // Lokacija potpisivanja

        public bool IsValid { get; set; } = true; // Je li potpis valjan

        public DateTime? ValidatedAt { get; set; } // Kada je potpis validiran
    }

    public class SignatureCertificate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]

        public User User { get; set; }

        [Required]
        public string PublicKey { get; set; } // Javni ključ za verifikaciju

        public DateTime IssuedAt { get; set; } = DateTime.Now;

        public DateTime ExpiresAt { get; set; } = DateTime.Now.AddYears(1);

        public bool IsRevoked { get; set; } = false;

        [MaxLength(500)]
        public string RevocationReason { get; set; }
    }
}