using ePisarnica.Models;
using ePisarnica.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace ePisarnica.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;

        public AccountController(AppDbContext context, IConfiguration configuration, ILogger<AccountController> logger)
        {
            _context = context;
            _configuration = configuration; 
            _logger = logger;
        }

        // GET: /Account/Register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var exists = await _context.Users.AnyAsync(u => u.Username == model.Username || u.Email == model.Email);
            if (exists)
            {
                ModelState.AddModelError("", "Username or email already taken.");
                return View(model);
            }

            var hashedPassword = ComputeSha256Hash(model.Password);

            var user = new User
            {
                Username = model.Username,
                Ime = model.Ime,
                Prezime = model.Prezime,
                Email = model.Email,
                PasswordHash = hashedPassword,
                Role = "User"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await SignInUser(user, model.RememberMe);

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var hashedPassword = ComputeSha256Hash(model.Password);

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                (u.Username == model.UsernameOrEmail || u.Email == model.UsernameOrEmail)
                && u.PasswordHash == hashedPassword);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid login attempt. Please check your credentials.");
                return View(model);
            }

            await SignInUser(user, model.RememberMe);

            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        private async Task SignInUser(User user, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("Username", user.Username),
                new Claim(ClaimTypes.Name, $"{user.Ime} {user.Prezime}")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(12)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        [HttpGet]
        public IActionResult Logout()
        {
            return View();
        }

        // POST: Handle actual logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutPost()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            TempData["Message"] = "You have been logged out.";
            return RedirectToAction("Login");
        }



        // GET: /Account/Account
        [HttpGet]
        public async Task<IActionResult> Account()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return RedirectToAction("Login");

            return View(user);
        }

        // POST: /Account/Account
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Account(string username, string email, string password)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return RedirectToAction("Login");

            if (!string.IsNullOrEmpty(username))
                user.Username = username;

            if (!string.IsNullOrEmpty(email))
                user.Email = email;

            if (!string.IsNullOrEmpty(password) && password.Length >= 6)
                user.PasswordHash = ComputeSha256Hash(password);

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(username))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));
            }

            ViewData["Message"] = "Account updated successfully.";
            return View(user);
        }

        // GET: /Account/ManageUsers (Admin only)
        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> ManageUsers()
        {
            var users = await _context.Users
                .Select(u => new RoleManagementViewModel
                {
                    UserId = u.Id,
                    Ime = u.Ime,
                    Prezime = u.Prezime,
                    Username = u.Username,
                    Email = u.Email,
                    CurrentRole = u.Role,
                    SelectedRole = u.Role
                })
                .ToListAsync();

            return View(users);
        }

        // POST: /Account/UpdateRole (Admin only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateRole(int userId, string newRole)
        {
            _logger.LogInformation("UpdateRole called with userId: {UserId}, newRole: {NewRole}", userId, newRole);

            try
            {
                var validRoles = new List<string> { "User", "Admin" };
                if (!validRoles.Contains(newRole))
                {
                    _logger.LogWarning("Invalid role specified: {NewRole}", newRole);
                    return Json(new { success = false, message = "Invalid role specified." });
                }

                _logger.LogInformation("Looking for user with ID: {UserId}", userId);

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", userId);
                    return Json(new { success = false, message = "User not found." });
                }

                _logger.LogInformation("Found user: {Username} (ID: {UserId}), Current role: {CurrentRole}",
                    user.Username, user.Id, user.Role);

                var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                _logger.LogInformation("Current authenticated user ID: {CurrentUserId}", currentUserId);

                if (user.Id == currentUserId && newRole != "Admin")
                {
                    _logger.LogWarning("Admin tried to remove their own admin privileges. User ID: {UserId}", userId);
                    return Json(new { success = false, message = "You cannot remove your own admin privileges." });
                }

                _logger.LogInformation("Updating user {Username} role from {OldRole} to {NewRole}",
                    user.Username, user.Role, newRole);

                user.Role = newRole;

                _context.Entry(user).Property(u => u.Role).IsModified = true;

                var changes = _context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Modified)
                    .Select(e => new
                    {
                        Entity = e.Entity.GetType().Name,
                        State = e.State,
                        ModifiedProperties = e.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToList()
                    })
                    .ToList();

                _logger.LogInformation("Changes to be saved: {@Changes}", changes);

                var result = await _context.SaveChangesAsync();
                _logger.LogInformation("SaveChanges completed. Affected rows: {RowsAffected}", result);

                _logger.LogInformation("Role updated successfully for user {Username}", user.Username);

                return Json(new { success = true, message = "Role updated successfully." });
            }
            catch (DbUpdateConcurrencyException concurrencyEx)
            {
                _logger.LogError(concurrencyEx, "Concurrency error occurred while updating role for user ID: {UserId}", userId);
                return Json(new { success = false, message = "Concurrency error occurred. Please try again." });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database update error occurred while updating role for user ID: {UserId}", userId);

                if (dbEx.InnerException != null)
                {
                    _logger.LogError(dbEx.InnerException, "Inner exception details for user ID: {UserId}", userId);
                }

                return Json(new { success = false, message = "Database error occurred. Please try again." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while updating role for user ID: {UserId}", userId);

                _logger.LogError("Exception type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);

                return Json(new { success = false, message = $"An unexpected error occurred: {ex.Message}" });
            }
        }

        // GET: /Account/UserDetails (Admin only - for modal/details view)
        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UserDetails(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new RoleManagementViewModel
                {
                    UserId = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    CurrentRole = u.Role,
                    SelectedRole = u.Role
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound();
            }

            return PartialView("_UserDetailsPartial", user);
        }

        // GET: /Account/MyAccount
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> MyAccount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return RedirectToAction("Login");

            var vm = new MyAccountViewModel
            {
                Id = user.Id,
                Username = user.Username,
                Ime = user.Ime,
                Prezime = user.Prezime,
                Email = user.Email
            };

            return View(vm);
        }

        // POST: /Account/MyAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> MyAccount(MyAccountViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users.FindAsync(model.Id);
            if (user == null)
                return RedirectToAction("Login");

            user.Username = model.Username;
            user.Ime = model.Ime;
            user.Prezime = model.Prezime;
            user.Email = model.Email;

            if (!string.IsNullOrEmpty(model.NewPassword))
                user.PasswordHash = ComputeSha256Hash(model.NewPassword);

            await _context.SaveChangesAsync();

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await SignInUser(user, true);

            TempData["Message"] = "Podaci vašeg računa su uspješno promijenjeni.";
            return RedirectToAction("MyAccount");
        }


        private static string ComputeSha256Hash(string rawData)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            var builder = new StringBuilder();
            foreach (var b in bytes) builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }
}