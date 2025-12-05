using System.ComponentModel.DataAnnotations;

namespace ePisarnica.ViewModels
{
    public class AddUserViewModel
    {
        [Required, StringLength(100)]
        public string Username { get; set; }

        [Required, StringLength(50)]
        public string Ime { get; set; }

        [Required, StringLength(50)]
        public string Prezime { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, MinLength(6)]
        public string Password { get; set; }

        [Required]
        public string Role { get; set; }
    }
}
