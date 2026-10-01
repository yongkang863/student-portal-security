using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FYP.Database;
using FYP.Models;

namespace FYP.Pages.Student
{
    [Authorize(Policy = "StudentOnly", AuthenticationSchemes = "StudentCookie")]
    public class SemesterResultModel : PageModel
    {
        private readonly DB _context;

        public SemesterResultModel(DB context)
        {
            _context = context;
        }

        public List<SemesterResult> AvailableSemesters { get; set; } = new();
        public SemesterResult? SelectedSemester { get; set; }
        public List<CourseResult> CourseResults { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? ResultId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidString) || !int.TryParse(uidString, out int uid))
            {
                return RedirectToPage("/Login");
            }

            // 1. Fetch all available semester results for the dropdown
            AvailableSemesters = await _context.SemesterResults
                .Where(sr => sr.StudentId == uid)
                .OrderByDescending(sr => sr.YearLevel)
                .ThenByDescending(sr => sr.Semester)
                .ToListAsync();

            if (!AvailableSemesters.Any())
            {
                return Page(); // No results found at all
            }

            // 2. Determine which semester to show (default to the most recent one)
            int targetResultId = ResultId ?? AvailableSemesters.First().Id;

            SelectedSemester = AvailableSemesters.FirstOrDefault(sr => sr.Id == targetResultId);

            if (SelectedSemester != null)
            {
                // 3. Fetch the specific course results for the selected semester
                CourseResults = await _context.CourseResults
                    .Include(cr => cr.Course)
                    .Include(cr => cr.Grade)
                    .Where(cr => cr.SemesterResultId == SelectedSemester.Id)
                    .OrderBy(cr => cr.Course!.Code)
                    .ToListAsync();
            }

            return Page();
        }
    }
}