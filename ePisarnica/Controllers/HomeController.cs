using ePisarnica.Models;
using ePisarnica.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ePisarnica.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;

        public HomeController(ILogger<HomeController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var viewModel = new DashboardViewModel
            {
                TotalProtocols = await _context.ProtocolEntries.CountAsync(),
                TotalDocuments = await _context.Documents.CountAsync(),
                TotalUsers = await _context.Users.CountAsync(),
                ActiveUsers = await _context.Users.CountAsync(u => u.Department != null && u.Department.Aktivan),
                RecentProtocols = await _context.ProtocolEntries
                    .Include(p => p.Document)
                    .OrderByDescending(p => p.Datum)
                    .Take(5)
                    .ToListAsync(),
                RecentUsers = await _context.Users
                    .Include(u => u.Department)
                    .OrderByDescending(u => u.Id)
                    .Take(5)
                    .ToListAsync(),
                PendingAssignments = await _context.Assignments
                    .Include(a => a.ProtocolEntry)
                    .Include(a => a.DodijeljenUser)
                    .Include(a => a.DodijeljenOdjel)
                    .Where(a => a.Status == "Aktivno")
                    .OrderBy(a => a.Rok)
                    .Take(5)
                    .ToListAsync()
            };

            var sevenDaysAgo = DateTime.Now.AddDays(-7);
            viewModel.RecentActivity = await _context.ProtocolEntries
                .Where(p => p.Datum >= sevenDaysAgo)
                .CountAsync();

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}