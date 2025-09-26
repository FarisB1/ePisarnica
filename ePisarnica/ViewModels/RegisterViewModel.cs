using System.ComponentModel.DataAnnotations;

namespace ePisarnica.ViewModels
{
    public class RegisterViewModel
    {
        [Required, StringLength(100)]
        public string Username { get; set; }
        
        [Required, StringLength(100)]
        public string Ime { get; set; }
        
        [Required, StringLength(100)]
        public string Prezime { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }

        [Required, DataType(DataType.Password), Compare("Password")]
        public string ConfirmPassword { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}