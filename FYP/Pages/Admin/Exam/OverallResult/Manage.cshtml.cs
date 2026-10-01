using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;

namespace FYP.Pages.Admin.Exam.OverallResult
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
    public class ManageModel : PageModel
    {
        private readonly DB _context;

        public ManageModel(DB context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)] public int Uid { get; set; }

        public User? TargetStudent { get; set; }
        public List<Models.SemesterResult> AllSemesters { get; set; } = new();
        public Dictionary<int, List<CourseResult>> SemesterCourses { get; set; } = new();

        public int TotalCreditsAttempted { get; set; }
        public int TotalCreditsEarned { get; set; }
        public decimal FinalCgpa { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            TargetStudent = await _context.Users
                .Include(u => u.Programme)
                .FirstOrDefaultAsync(u => u.Uid == Uid && u.UsersRoleId == 3);

            if (TargetStudent == null) return NotFound();

            // Fetch all Semester Results for this specific student
            AllSemesters = await _context.SemesterResults
                .Where(sr => sr.StudentId == Uid)
                .OrderBy(sr => sr.YearLevel).ThenBy(sr => sr.Semester)
                .ToListAsync();

            if (AllSemesters.Any())
            {
                FinalCgpa = AllSemesters.Last().Cgpa ?? 0;

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

                    foreach (var cr in coursesForSem)
                    {
                        if (cr.Course != null)
                        {
                            TotalCreditsAttempted += cr.Course.CreditHours;

                            // If grade point > 0, it means they passed/earned the credits
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