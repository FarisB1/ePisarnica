using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ePisarnica.Models
{
    public class Folder
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        public string Color { get; set; } = "#6c757d";

        public int? ParentFolderId { get; set; }
        public virtual Folder ParentFolder { get; set; }

        public int UserId { get; set; }
        public virtual User User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<Document> Documents { get; set; }
    }
}