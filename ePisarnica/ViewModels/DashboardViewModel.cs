using ePisarnica.Models;
using System.Collections.Generic;

namespace ePisarnica.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalProtocols { get; set; }
        public int TotalDocuments { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int RecentActivity { get; set; }
        public List<ProtocolEntry> RecentProtocols { get; set; } = new List<ProtocolEntry>();
        public List<User> RecentUsers { get; set; } = new List<User>();
        public List<Assignment> PendingAssignments { get; set; } = new List<Assignment>();
    }
}
