using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Exam.SemesterResult
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
        [BindProperty(SupportsGet = true)] public int? TargetYear { get; set; }
        [BindProperty(SupportsGet = true)] public int? TargetSem { get; set; }

        public User? Student { get; set; }
        public Models.SemesterResult? CurrentSemesterResult { get; set; }
        public List<SelectListItem> GradeOptions { get; set; } = new();
        public List<SelectListItem> TermOptions { get; set; } = new();

        // NEW: Flag to control frontend editing
        public bool IsEditable { get; set; }

        [BindProperty] public List<CourseInput> CourseInputs { get; set; } = new();

        public class CourseInput
        {
            public int CourseId { get; set; }
            public string CourseCode { get; set; } = "";
            public string CourseTitle { get; set; } = "";
            public int CreditHours { get; set; }
            public int? SelectedGradeId { get; set; }
        }

        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDataAsync();
            if (Student == null) return NotFound();
            return Page();
        }

        private async Task LoadDataAsync()
        {
            Student = await _context.Users
                .Include(u => u.Programme)
                .FirstOrDefaultAsync(u => u.Uid == Uid && u.UsersRoleId == 3);

            if (Student == null || Student.Programme == null) return;

            // If Year/Sem not specified in URL, default to the student's current year/sem
            TargetYear ??= Student.YearLevel ?? 1;
            TargetSem ??= Student.Semester ?? 1;

            // --- Determine if the requested term is Editable (Current or Previous Semester) ---
            int currentY = Student.YearLevel ?? 1;
            int currentS = Student.Semester ?? 1;

            int lastY = currentY;
            int lastS = currentS - 1;
            if (lastS < 1)
            {
                lastY = currentY - 1;
                lastS = 3; // Max semester is 3 based on your structure
            }

            IsEditable = (TargetYear == currentY && TargetSem == currentS) ||
                         (TargetYear == lastY && TargetSem == lastS);
            // ---------------------------------------------------------------------------------

            // Populate the Term Dropdown
            int maxYear = Student.Programme.YearToComplete ?? 3;
            for (int y = 1; y <= maxYear; y++)
            {
                for (int s = 1; s <= 3; s++)
                {
                    TermOptions.Add(new SelectListItem
                    {
                        Value = $"{y}-{s}",
                        Text = $"Year {y}, Semester {s}",
                        Selected = (y == TargetYear && s == TargetSem)
                    });
                }
            }

            var grades = await _context.GradingSchemes.Where(g => g.Status == "Active").OrderByDescending(g => g.GradePoint).ToListAsync();
            GradeOptions = grades.Select(g => new SelectListItem { Value = g.Id.ToString(), Text = $"{g.GradeLabel} ({g.GradePoint})" }).ToList();

            CurrentSemesterResult = await _context.SemesterResults
                .FirstOrDefaultAsync(sr => sr.StudentId == Uid && sr.YearLevel == TargetYear && sr.Semester == TargetSem);

            var expectedCourses = await _context.Set<ProgrammeStructure>()
                .Include(ps => ps.Course)
                .Where(ps => ps.ProgrammeId == Student.ProgrammeId
                          && ps.SessionJoined == Student.SessionJoined
                          && ps.YearLevel == TargetYear
                          && ps.Semester == TargetSem)
                .Select(ps => ps.Course)
                .ToListAsync();

            var existingResults = new Dictionary<int, int?>();
            if (CurrentSemesterResult != null)
            {
                existingResults = await _context.CourseResults
                    .Where(cr => cr.SemesterResultId == CurrentSemesterResult.Id)
                    .ToDictionaryAsync(cr => cr.CourseId, cr => cr.GradeId);
            }

            CourseInputs = expectedCourses.Select(c => new CourseInput
            {
                CourseId = c!.Id,
                CourseCode = c.Code,
                CourseTitle = c.Title,
                CreditHours = c.CreditHours,
                SelectedGradeId = existingResults.ContainsKey(c.Id) ? existingResults[c.Id] : null
            }).OrderBy(c => c.CourseCode).ToList();
        }

        public async Task<IActionResult> OnPostSaveResultsAsync()
        {
            var student = await _context.Users.FindAsync(Uid);
            if (student == null) return NotFound();

            // --- Security Check: Re-verify edit permissions on POST ---
            int currentY = student.YearLevel ?? 1;
            int currentS = student.Semester ?? 1;
            int lastY = currentY;
            int lastS = currentS - 1;
            if (lastS < 1) { lastY = currentY - 1; lastS = 3; }

            bool isEditable = (TargetYear == currentY && TargetSem == currentS) ||
                              (TargetYear == lastY && TargetSem == lastS);

            if (!isEditable)
            {
                ErrorMessage = "Action Denied: You can only modify results for the student's current or previous semester.";
                return RedirectToPage(new { uid = Uid, targetYear = TargetYear, targetSem = TargetSem });
            }
            // -----------------------------------------------------------

            if (CourseInputs == null || !CourseInputs.Any())
            {
                ErrorMessage = "No courses to save.";
                return RedirectToPage(new { uid = Uid, targetYear = TargetYear, targetSem = TargetSem });
            }

            // 1. Get or Create SemesterResult record
            var semResult = await _context.SemesterResults
                .FirstOrDefaultAsync(sr => sr.StudentId == Uid && sr.YearLevel == TargetYear && sr.Semester == TargetSem);

            if (semResult == null)
            {
                semResult = new Models.SemesterResult { StudentId = Uid, YearLevel = TargetYear.Value, Semester = TargetSem.Value, AcademicStatus = "Active" };
                _context.SemesterResults.Add(semResult);
                await _context.SaveChangesAsync();
            }

            // 2. Fetch Grading Data for calculations
            var gradingSchemes = await _context.GradingSchemes.ToDictionaryAsync(g => g.Id, g => g);
            decimal totalSemesterPoints = 0;
            int totalSemesterCredits = 0;

            // 3. Upsert Course Results
            foreach (var input in CourseInputs)
            {
                if (!input.SelectedGradeId.HasValue) continue;

                var cr = await _context.CourseResults.FirstOrDefaultAsync(c => c.SemesterResultId == semResult.Id && c.CourseId == input.CourseId);

                if (cr == null)
                {
                    cr = new CourseResult { SemesterResultId = semResult.Id, CourseId = input.CourseId };
                    _context.CourseResults.Add(cr);
                }

                cr.GradeId = input.SelectedGradeId.Value;
                cr.GradePointEarned = gradingSchemes[input.SelectedGradeId.Value].GradePoint;

                totalSemesterPoints += (cr.GradePointEarned.Value * input.CreditHours);
                totalSemesterCredits += input.CreditHours;
            }

            await _context.SaveChangesAsync();

            // 4. Calculate GPA for this semester
            if (totalSemesterCredits > 0)
            {
                semResult.Gpa = totalSemesterPoints / totalSemesterCredits;
            }

            // 5. Calculate Global CGPA
            var allStudentCourseResults = await _context.CourseResults
                .Include(cr => cr.SemesterResult)
                .Include(cr => cr.Course)
                .Where(cr => cr.SemesterResult!.StudentId == Uid && cr.GradePointEarned != null)
                .ToListAsync();

            decimal totalGlobalPoints = 0;
            int totalGlobalCredits = 0;

            foreach (var r in allStudentCourseResults)
            {
                totalGlobalPoints += (r.GradePointEarned!.Value * r.Course!.CreditHours);
                totalGlobalCredits += r.Course.CreditHours;
            }

            if (totalGlobalCredits > 0)
            {
                semResult.Cgpa = totalGlobalPoints / totalGlobalCredits;
            }

            // Update Academic Status Logic
            if (semResult.Cgpa < 2.0m) semResult.AcademicStatus = "Probation";
            else if (semResult.Cgpa >= 3.5m) semResult.AcademicStatus = "Dean's List";
            else semResult.AcademicStatus = "Pass";

            await _context.SaveChangesAsync();

            SuccessMessage = $"Results for Year {TargetYear} Semester {TargetSem} successfully updated. GPA: {semResult.Gpa:0.00} | CGPA: {semResult.Cgpa:0.00}";
            return RedirectToPage(new { uid = Uid, targetYear = TargetYear, targetSem = TargetSem });
        }
    }
}