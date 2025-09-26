namespace ePisarnica.ViewModels
{

    public class RoleManagementViewModel
    {
        public int UserId { get; set; }
        public string Ime { get; set; }
        public string Prezime { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string CurrentRole { get; set; }
        public string SelectedRole { get; set; }
        public List<string> AvailableRoles { get; set; } = new List<string> { "User", "Admin" };
    }
}

