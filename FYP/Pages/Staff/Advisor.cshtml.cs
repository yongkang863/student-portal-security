using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FYP.Pages.Staff
{
    [Authorize(Policy = "StaffOnly", AuthenticationSchemes = "StaffAuth")]
    public class AdvisorModel : PageModel
    {
        private readonly DB _context;

        public AdvisorModel(DB context)
        {
            _context = context;
        }

        public IList<User> Advisees { get; set; } = new List<User>();

        // --- NEW: Property to hold programmes for the dropdown filter ---
        public IList<Programme> AvailableProgrammes { get; set; } = new List<Programme>();

        // --- NEW: Bindable properties for the search and filter ---
        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ProgrammeFilter { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var uidString = User.FindFirst("Uid")?.Value ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentStaffId = int.TryParse(uidString, out int id) ? id : HttpContext.Session.GetInt32("Uid") ?? 0;

            if (currentStaffId == 0) return RedirectToPage("/Staff/Login");

            // Fetch available programmes so we can display them in the filter dropdown
            AvailableProgrammes = await _context.Programmes.OrderBy(p => p.Code).ToListAsync();

            // Start building the query
            var query = _context.Users
                .Include(u => u.Programme)
                .Include(u => u.Advisory)
                .Where(u => u.Advisory != null && u.Advisory.StaffId == currentStaffId)
                .AsQueryable();

            // --- NEW: Apply Search Filter ---
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                query = query.Where(u =>
                    u.FullName.Contains(SearchQuery) ||
                    u.Email.Contains(SearchQuery) ||
                    u.Uid.ToString().Contains(SearchQuery));
            }

            // --- NEW: Apply Programme Filter ---
            if (ProgrammeFilter.HasValue && ProgrammeFilter.Value > 0)
            {
                query = query.Where(u => u.ProgrammeId == ProgrammeFilter.Value);
            }

            // Execute the query
            Advisees = await query.ToListAsync();

            return Page();
        }
    }
}