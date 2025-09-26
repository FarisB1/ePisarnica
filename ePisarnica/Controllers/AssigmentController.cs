using ePisarnica.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ePisarnica.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AssignmentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AssignmentController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Assignment/ForProtocol/5
        [HttpGet("ForProtocol/{protocolId}")]
        public async Task<IActionResult> GetAssignmentsForProtocol(int protocolId)
        {
            var assignments = await _context.Assignments
                .Include(a => a.DodijeljenOdjel)
                .Include(a => a.DodijeljenUser)
                .Where(a => a.ProtocolEntryId == protocolId)
                .OrderByDescending(a => a.DatumDodjele)
                .ToListAsync();

            return Ok(assignments);
        }

        // GET: api/Assignment/Active
        [HttpGet("Active")]
        public async Task<IActionResult> GetActiveAssignments()
        {
            var assignments = await _context.Assignments
                .Include(a => a.ProtocolEntry)
                .Include(a => a.DodijeljenOdjel)
                .Include(a => a.DodijeljenUser)
                .Where(a => a.Status == "Aktivno")
                .OrderByDescending(a => a.DatumDodjele)
                .ToListAsync();

            return Ok(assignments);
        }

        // POST: api/Assignment
        [HttpPost]
        public async Task<IActionResult> CreateAssignment([FromBody] CreateAssignmentRequest request)
        {
            try
            {
                // Validate protocol entry exists
                var protocolEntry = await _context.ProtocolEntries
                    .FirstOrDefaultAsync(p => p.Id == request.ProtocolEntryId);

                if (protocolEntry == null)
                {
                    return BadRequest(new { message = "Protokol nije pronađen" });
                }

                // Validate that either user or department is specified
                if (!request.DodijeljenUserId.HasValue && !request.DodijeljenOdjelId.HasValue)
                {
                    return BadRequest(new { message = "Morate odabrati službenika ili odjel" });
                }

                // Validate user exists if specified
                if (request.DodijeljenUserId.HasValue)
                {
                    var user = await _context.Users
                        .FirstOrDefaultAsync(u => u.Id == request.DodijeljenUserId.Value);

                    if (user == null)
                    {
                        return BadRequest(new { message = "Službenik nije pronađen" });
                    }
                }

                // Validate department exists if specified
                if (request.DodijeljenOdjelId.HasValue)
                {
                    var department = await _context.Departments
                        .FirstOrDefaultAsync(d => d.Id == request.DodijeljenOdjelId.Value);

                    if (department == null)
                    {
                        return BadRequest(new { message = "Odjel nije pronađen" });
                    }
                }

                var assignment = new Assignment
                {
                    ProtocolEntryId = request.ProtocolEntryId,
                    DodijeljenOdjelId = request.DodijeljenOdjelId,
                    DodijeljenUserId = request.DodijeljenUserId,
                    Rok = request.Rok,
                    Prioritet = request.Prioritet,
                    Napomena = request.Napomena,
                    Status = "Aktivno",
                    DatumDodjele = DateTime.Now
                };

                _context.Assignments.Add(assignment);
                await _context.SaveChangesAsync();

                // Load related data for response
                await _context.Entry(assignment)
                    .Reference(a => a.DodijeljenOdjel)
                    .LoadAsync();

                await _context.Entry(assignment)
                    .Reference(a => a.DodijeljenUser)
                    .LoadAsync();

                return Ok(new
                {
                    message = "Predmet uspješno dodijeljen",
                    assignment = assignment
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Greška: {ex.Message}" });
            }
        }

        // PUT: api/Assignment/5/Complete
        [HttpPut("{id}/Complete")]
        public async Task<IActionResult> CompleteAssignment(int id, [FromBody] CompleteAssignmentRequest request)
        {
            try
            {
                var assignment = await _context.Assignments
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (assignment == null)
                {
                    return NotFound(new { message = "Dodjela nije pronađena" });
                }

                assignment.Status = "Završeno";
                assignment.DatumZavrsetka = DateTime.Now;
                assignment.Napomena = request.Napomena;

                _context.Assignments.Update(assignment);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Dodjela označena kao završena" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Greška: {ex.Message}" });
            }
        }

        // PUT: api/Assignment/5/Cancel
        [HttpPut("{id}/Cancel")]
        public async Task<IActionResult> CancelAssignment(int id, [FromBody] CancelAssignmentRequest request)
        {
            try
            {
                var assignment = await _context.Assignments
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (assignment == null)
                {
                    return NotFound(new { message = "Dodjela nije pronađena" });
                }

                assignment.Status = "Otkazano";
                assignment.Napomena = request.Napomena;

                _context.Assignments.Update(assignment);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Dodjela otkazana" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Greška: {ex.Message}" });
            }
        }

        // GET: api/Assignment/Departments
        [HttpGet("Departments")]
        public async Task<IActionResult> GetDepartments()
        {
            var departments = await _context.Departments
                .Where(d => d.Aktivan)
                .OrderBy(d => d.Naziv)
                .ToListAsync();

            return Ok(departments);
        }

        // GET: api/Assignment/Users
        [HttpGet("Users")]
        public async Task<IActionResult> GetUsers([FromQuery] int? departmentId = null)
        {
            var query = _context.Users.AsQueryable();

            if (departmentId.HasValue)
            {
                query = query.Where(u => u.DepartmentId == departmentId.Value);
            }

            var users = await query
                .OrderBy(u => u.Username)
                .Select(u => new
                {
                    u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    u.DepartmentId
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/Assignment/ActiveUsers
        [HttpGet("ActiveUsers")]
        public async Task<IActionResult> GetActiveUsers([FromQuery] int? departmentId = null)
        {
            // Since we don't have an Active property, we'll consider all users as active
            // You can modify this logic if you have a different way to determine active users
            var query = _context.Users.AsQueryable();

            if (departmentId.HasValue)
            {
                query = query.Where(u => u.DepartmentId == departmentId.Value);
            }

            var users = await query
                .OrderBy(u => u.Username)
                .Select(u => new
                {
                    u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    u.DepartmentId
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/Assignment/UsersWithAssignments
        [HttpGet("UsersWithAssignments")]
        public async Task<IActionResult> GetUsersWithAssignments([FromQuery] int? departmentId = null)
        {
            var query = _context.Users.AsQueryable();

            if (departmentId.HasValue)
            {
                query = query.Where(u => u.DepartmentId == departmentId.Value);
            }

            // Get users who have active assignments
            var usersWithActiveAssignments = await _context.Assignments
                .Where(a => a.Status == "Aktivno" && a.DodijeljenUserId != null)
                .Select(a => a.DodijeljenUserId.Value)
                .Distinct()
                .ToListAsync();

            var users = await query
                .OrderBy(u => u.Username)
                .Select(u => new
                {
                    u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    u.DepartmentId,
                    HasActiveAssignments = usersWithActiveAssignments.Contains(u.Id)
                })
                .ToListAsync();

            return Ok(users);
        }
    }

    // Request models
    public class CreateAssignmentRequest
    {
        public int ProtocolEntryId { get; set; }
        public int? DodijeljenOdjelId { get; set; }
        public int? DodijeljenUserId { get; set; }
        public DateTime? Rok { get; set; }
        public string? Prioritet { get; set; }
        public string? Napomena { get; set; }
    }

    public class CompleteAssignmentRequest
    {
        public string? Napomena { get; set; }
    }

    public class CancelAssignmentRequest
    {
        public string? Napomena { get; set; }
    }
}