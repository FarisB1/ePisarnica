using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePisarnica.Models
{
    public class Assignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProtocolEntryId { get; set; }

        [ForeignKey("ProtocolEntryId")]
        public ProtocolEntry ProtocolEntry { get; set; }

        public int? DodijeljenOdjelId { get; set; }

        [ForeignKey("DodijeljenOdjelId")]
        public Department? DodijeljenOdjel { get; set; }

        public int? DodijeljenUserId { get; set; }

        [ForeignKey("DodijeljenUserId")]
        public User? DodijeljenUser { get; set; }

        public DateTime? Rok { get; set; }

        [MaxLength(50)]
        public string? Prioritet { get; set; } // "Nizak", "Srednji", "Visok", "Hitno"

        public DateTime DatumDodjele { get; set; } = DateTime.Now;

        public DateTime? DatumZavrsetka { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Aktivno"; // "Aktivno", "Završeno", "Otkazano"

        [MaxLength(500)]
        public string? Napomena { get; set; }
    }

    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Naziv { get; set; }

        [MaxLength(10)]
        public string? Sifra { get; set; }

        public bool Aktivan { get; set; } = true;

        // Navigation properties
        public ICollection<Assignment> Assignments { get; set; }
        public ICollection<User> Users { get; set; }
    }
}