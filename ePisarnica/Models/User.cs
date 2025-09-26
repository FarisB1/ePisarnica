using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePisarnica.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Username { get; set; }

        [Required, StringLength(50)]
        public string Ime { get; set; }

        [Required, StringLength(50)]
        public string Prezime { get; set; }

        [Required, StringLength(100)]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required]
        public string Role { get; set; } = "User";

        public virtual ICollection<Document> Documents { get; set; }
        public virtual ICollection<Folder> Folders { get; set; }
        public ICollection<Assignment> Assignments { get; set; }
        public int? DepartmentId { get; set; }

        [ForeignKey("DepartmentId")]
        public Department? Department { get; set; }

        public virtual ICollection<DigitalSignature> DigitalSignatures { get; set; }
        public virtual ICollection<SignatureCertificate> SignatureCertificates { get; set; }
    }
}