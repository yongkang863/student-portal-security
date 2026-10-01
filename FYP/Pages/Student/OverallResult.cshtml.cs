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
    public class OverallResultModel : PageModel
    {
        private readonly DB _context;

        public OverallResultModel(DB context)
        {
            _context = context;
        }

        public Models.User? CurrentUser { get; set; }
        public List<SemesterResult> AllSemesters { get; set; } = new();
        public Dictionary<int, List<CourseResult>> SemesterCourses { get; set; } = new();

        public int TotalCreditsAttempted { get; set; }
        public int TotalCreditsEarned { get; set; }
        public decimal FinalCgpa { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidString) || !int.TryParse(uidString, out int uid))
            {
                return RedirectToPage("/Login");
            }

            // 1. Fetch User and Programme Details for the Transcript Header
            CurrentUser = await _context.Users
                .Include(u => u.Programme)
                .FirstOrDefaultAsync(u => u.Uid == uid);

            if (CurrentUser == null) return Page();

            // 2. Fetch all Semester Results
            AllSemesters = await _context.SemesterResults
                .Where(sr => sr.StudentId == uid)
                .OrderBy(sr => sr.YearLevel).ThenBy(sr => sr.Semester)
                .ToListAsync();

            if (AllSemesters.Any())
            {
                FinalCgpa = AllSemesters.Last().Cgpa ?? 0;

                // 3. Fetch all Course Results and group by Semester
                var semesterIds = AllSemesters.Select(s => s.Id).ToList();

                var allCourseResults = await _context.CourseResults
                    .Include(cr => cr.Course)
                    .Include(cr => cr.Grade)
                    .Where(cr => semesterIds.Contains(cr.SemesterResultId))
                    .ToListAsync();

                foreach (var sem in AllSemesters)
                {
                    var coursesForSem = allCourseResults.Where(cr => cr.SemesterResultId == sem.Id).ToList();
                    SemesterCourses[sem.Id] = coursesForSem;

                    // Calculate total credits
                    foreach (var cr in coursesForSem)
                    {
                        if (cr.Course != null)
                        {
                            TotalCreditsAttempted += cr.Course.CreditHours;
                            // Only count as earned if not failed (assuming grade point > 0 implies pass, adjust based on your specific rules)
                            if (cr.Grade != null && cr.Grade.GradePoint > 0)
                            {
                                TotalCreditsEarned += cr.Course.CreditHours;
                            }
                        }
                    }
                }
            }

            return Page();
        }
    }
}