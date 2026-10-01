using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FYP.Pages.Student
{
    [Authorize(Policy = "StudentOnly", AuthenticationSchemes = "StudentCookie")]
    public class ProgrammeStructureModel : PageModel
    {
        private readonly DB _context;

        public ProgrammeStructureModel(DB context)
        {
            _context = context;
        }

        // Data to display
        public Models.Programme? CurrentProgramme { get; set; }
        public int? UserSessionJoined { get; set; }

        // Structure: Dictionary<Year, Dictionary<Semester, List<StructureItem>>>
        public Dictionary<int, Dictionary<int, List<ProgrammeStructure>>> OrganizedStructure { get; set; } = new();

        public async Task OnGetAsync()
        {
            // 1. Get Current User ID
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uidString, out int uid)) return;

            // 2. Fetch User's Programme & Session Info
            var user = await _context.Users
                .Include(u => u.Programme)
                .FirstOrDefaultAsync(u => u.Uid == uid);

            if (user == null || user.Programme == null) return;

            CurrentProgramme = user.Programme;
            UserSessionJoined = user.SessionJoined;

            // 3. Fetch Structure Data
            // We filter by the User's ProgrammeID AND their SessionJoined
            var flatStructure = await _context.ProgrammeStructures
                .Include(ps => ps.Course) // Eager load Course details
                .Where(ps => ps.ProgrammeId == user.ProgrammeId && ps.SessionJoined == user.SessionJoined)
                .OrderBy(ps => ps.YearLevel)
                .ThenBy(ps => ps.Semester)
                .ToListAsync();

            // 4. Organize Data for the View (Year -> Semester -> Courses)
            // We initialize the dictionary based on the Programme's "YearToComplete"
            int totalYears = CurrentProgramme.YearToComplete ?? 1; // Default to 1 if null

            for (int y = 1; y <= totalYears; y++)
            {
                OrganizedStructure[y] = new Dictionary<int, List<ProgrammeStructure>>();

                // Assuming 3 semesters per year as per your instruction
                for (int s = 1; s <= 3; s++)
                {
                    // Find courses for this specific Year & Semester
                    var coursesInSem = flatStructure
                        .Where(x => x.YearLevel == y && x.Semester == s)
                        .OrderBy(x => x.Course?.Code)
                        .ToList();

                    OrganizedStructure[y][s] = coursesInSem;
                }
            }
        }
    }
}