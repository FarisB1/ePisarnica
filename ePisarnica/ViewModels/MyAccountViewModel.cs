using System.ComponentModel.DataAnnotations;

namespace ePisarnica.ViewModels
{
    public class MyAccountViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Username { get; set; }

        [Required, StringLength(50)]
        public string Ime { get; set; }

        [Required, StringLength(50)]
        public string Prezime { get; set; }

        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; }

        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Šifra mora biti minimalno 6 karaktera.")]
        public string? NewPassword { get; set; }
    }
}
