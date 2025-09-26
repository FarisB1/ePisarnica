using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePisarnica.Models
{
    public class ProtocolEntry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BrojProtokola { get; set; }

        [Required]
        public DateTime Datum { get; set; } = DateTime.Now;

        [Required, MaxLength(200)]
        public string Stranka { get; set; } = string.Empty;

        public string? Napomena { get; set; }

        public int? DocumentId { get; set; }

        [ForeignKey("DocumentId")]
        public Document? Document { get; set; }

        public string? QrCodePath { get; set; }

        [MaxLength(100)]
        public string? Primalac { get; set; }

        [MaxLength(100)]
        public string? Dostavio { get; set; }

        [MaxLength(50)]
        public string? Hitno { get; set; }

        [MaxLength(50)]
        public string? VrstaPostupka { get; set; }

        public DateTime? RokZaOdgovor { get; set; }

        [MaxLength(200)]
        public string? Adresa { get; set; }

        [MaxLength(50)]
        public string? Telefon { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }


        [NotMapped]
        public IFormFile? UploadedFile { get; set; }

        [MaxLength(200)]
        public string? OriginalFileName { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public ICollection<Assignment>? Assignments { get; set; }
        public bool IsNew { get; set; } = true;
        public bool IsSigned { get; set; } = false;
        public DateTime? SignedDate { get; set; }
        public int? SignedByUserId { get; set; }

        [ForeignKey("SignedByUserId")]
        public User? SignedByUser { get; set; }

        public string? SignatureNotes { get; set; }

    }
}
