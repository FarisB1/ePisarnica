using System.ComponentModel.DataAnnotations;

namespace ePisarnica.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username or email is required")]
        [Display(Name = "Username or Email")]
        public string UsernameOrEmail { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}