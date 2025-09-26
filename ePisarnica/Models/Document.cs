using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePisarnica.Models
{
    public enum DocumentStatus
    {
        Zaprimljeno, 
        Uvid, 
        UObradi, 
        NaDopuni, 
        Recenzija, 
        Odobreno, 
        Odbijeno, 
        Arhivirano,
        IsTrashed,
        Potpisan
    }

    public enum FileType
    {
        Document,
        Image,
        Audio,
        Video,
        Archive,
        Other
    }

    public class Document
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string FilePath { get; set; }

        [Required]
        public string FileName { get; set; }

        [Required]
        public long FileSize { get; set; }

        [Required]
        public FileType FileType { get; set; }

        [Required]
        public string FileExtension { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime ModifiedAt { get; set; } = DateTime.Now;

        public DocumentStatus Status { get; set; } = DocumentStatus.Zaprimljeno;

        public int? FolderId { get; set; }
        public virtual Folder Folder { get; set; }

        public int UserId { get; set; }
        public virtual User User { get; set; }

        public bool IsTrashed { get; set; } = false;
        public DateTime? TrashedAt { get; set; }

        public bool IsShared { get; set; } = false;

        public ProtocolEntry? ProtocolEntry { get; set; }
        public virtual ICollection<DigitalSignature> DigitalSignatures { get; set; }
    }
}