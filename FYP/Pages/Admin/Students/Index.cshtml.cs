using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Students;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class IndexModel : PageModel
{
    private readonly DB _context;

    public IndexModel(DB context)
    {
        _context = context;
    }

    public List<User> StudentList { get; set; } = new();
    public Dictionary<int, bool> SafeToAdvance { get; set; } = new();

    // --- FILTERS & SEARCH PROPERTIES ---
    [BindProperty(SupportsGet = true)] public string? SearchQuery { get; set; }
    [BindProperty(SupportsGet = true)] public int? FilterDepartmentId { get; set; }
    [BindProperty(SupportsGet = true)] public int? FilterProgrammeId { get; set; }
    [BindProperty(SupportsGet = true)] public string? FilterTutorialGroup { get; set; }
    [BindProperty(SupportsGet = true)] public int? FilterSessionJoined { get; set; }
    [BindProperty(SupportsGet = true)] public int? FilterYearLevel { get; set; }
    [BindProperty(SupportsGet = true)] public int? FilterSemester { get; set; }
    [BindProperty(SupportsGet = true)] public string? FilterStudyStatus { get; set; }

    // --- DROPDOWN OPTIONS ---
    public List<SelectListItem> DepartmentOptions { get; set; } = new();
    public List<SelectListItem> ProgrammeOptions { get; set; } = new();
    public List<SelectListItem> TutorialGroupOptions { get; set; } = new();
    public List<SelectListItem> SessionOptions { get; set; } = new();
    public List<SelectListItem> StatusOptions { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadFilterDropdownsAsync();

        // Base Query
        var query = _context.Users
            .Include(u => u.Role)
            .Include(u => u.Programme)
            .Where(u => u.UsersRoleId == 3);

        // Apply Search
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var lowerSearch = SearchQuery.ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(lowerSearch) ||
                                     u.UserId.ToLower().Contains(lowerSearch));
        }

        // Apply Filters
        if (FilterDepartmentId.HasValue)
            query = query.Where(u => u.DepartmentId == FilterDepartmentId.Value);

        if (FilterProgrammeId.HasValue)
            query = query.Where(u => u.ProgrammeId == FilterProgrammeId.Value);

        if (!string.IsNullOrWhiteSpace(FilterTutorialGroup))
            query = query.Where(u => u.TutorialGroup == FilterTutorialGroup);

        if (FilterSessionJoined.HasValue)
            query = query.Where(u => u.SessionJoined == FilterSessionJoined.Value);

        if (FilterYearLevel.HasValue)
            query = query.Where(u => u.YearLevel == FilterYearLevel.Value);

        if (FilterSemester.HasValue)
            query = query.Where(u => u.Semester == FilterSemester.Value);

        if (!string.IsNullOrWhiteSpace(FilterStudyStatus))
            query = query.Where(u => u.StudyStatus == FilterStudyStatus);

        // Execute Query
        StudentList = await query.OrderBy(u => u.UserId).ToListAsync();

        // Evaluate Eligibility for Active Students in the filtered list
        var activeStudents = StudentList.Where(u => u.StudyStatus == "Active").ToList();
        var activeStudentIds = activeStudents.Select(u => u.Uid).ToList();

        if (activeStudentIds.Any())
        {
            var semesterResults = await _context.Set<SemesterResult>()
                .Where(sr => activeStudentIds.Contains(sr.StudentId))
                .ToListAsync();

            var srIds = semesterResults.Select(sr => sr.Id).ToList();

            var courseResults = await _context.Set<CourseResult>()
                .Include(cr => cr.Grade)
                .Where(cr => srIds.Contains(cr.SemesterResultId))
                .ToListAsync();

            foreach (var student in activeStudents)
            {
                var currentSemResult = semesterResults.FirstOrDefault(sr =>
                    sr.StudentId == student.Uid && sr.YearLevel == student.YearLevel && sr.Semester == student.Semester);

                if (currentSemResult != null)
                {
                    var studentGrades = courseResults.Where(cr => cr.SemesterResultId == currentSemResult.Id).ToList();

                    bool hasFail = studentGrades.Any(cr => cr.Grade != null && cr.Grade.GradeLabel == "F");
                    bool hasUnassigned = studentGrades.Any(cr => cr.GradeId == null);

                    SafeToAdvance[student.Uid] = studentGrades.Any() && !hasFail && !hasUnassigned;
                }
                else
                {
                    SafeToAdvance[student.Uid] = false;
                }
            }
        }
    }

    private async Task LoadFilterDropdownsAsync()
    {
        DepartmentOptions = await _context.Set<Department>()
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.DepartmentName })
            .ToListAsync();

        ProgrammeOptions = await _context.Set<Programme>()
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = $"{p.Code} - {p.Name}" })
            .ToListAsync();

        var sessions = await _context.Users
            .Where(u => u.UsersRoleId == 3 && u.SessionJoined != null)
            .Select(u => u.SessionJoined)
            .Distinct()
            .OrderByDescending(s => s)
            .ToListAsync();
        SessionOptions = sessions.Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString() }).ToList();

        var tutGroups = await _context.Users
            .Where(u => u.UsersRoleId == 3 && u.TutorialGroup != null)
            .Select(u => u.TutorialGroup)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
        TutorialGroupOptions = tutGroups.Select(t => new SelectListItem { Value = t, Text = t }).ToList();

        StatusOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "Active", Text = "Active" },
            new SelectListItem { Value = "Inactive", Text = "Inactive" }
        };
    }

    // --- ADVANCE SEMESTER HANDLER ---
    public async Task<IActionResult> OnPostAdvanceSemesterAsync(string confirmAdvance, List<int> selectedStudentIds)
    {
        if (string.IsNullOrEmpty(confirmAdvance) || confirmAdvance.Trim().ToLower() != "confirm")
        {
            TempData["ErrorMessage"] = "Invalid confirmation. Semester advancement aborted.";
            return RedirectToPage();
        }

        if (selectedStudentIds == null || !selectedStudentIds.Any())
        {
            TempData["ErrorMessage"] = "No students were selected for advancement.";
            return RedirectToPage();
        }

        var studentsToAdvance = await _context.Users
            .Include(u => u.Programme)
            .Where(u => u.UsersRoleId == 3 && u.StudyStatus == "Active" && selectedStudentIds.Contains(u.Uid))
            .ToListAsync();

        foreach (var student in studentsToAdvance)
        {
            int maxYear = student.Programme?.YearToComplete ?? 0;
            int currentYear = student.YearLevel ?? 1;
            int currentSemester = student.Semester ?? 1;

            if (currentSemester < 3)
            {
                student.Semester = currentSemester + 1;
            }
            else
            {
                if (currentYear < maxYear)
                {
                    student.YearLevel = currentYear + 1;
                    student.Semester = 1;
                }
                else if (currentYear >= maxYear)
                {
                    student.StudyStatus = "Inactive";
                }
            }
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Successfully advanced {studentsToAdvance.Count} active student(s) to the next semester.";

        return RedirectToPage();
    }

    // --- TOGGLE LOCK HANDLER ---
    public async Task<IActionResult> OnPostToggleLockAsync(int uid)
    {
        var user = await _context.Users.FindAsync(uid);
        if (user != null)
        {
            if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.Now)
            {
                user.LockedUntil = null;
                user.FailedLoginAttempts = 0;
            }
            else
            {
                user.LockedUntil = DateTime.MaxValue;
            }
            await _context.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    // --- DELETE HANDLER ---
    public async Task<IActionResult> OnPostDeleteAsync(int uid)
    {
        var user = await _context.Users.FindAsync(uid);
        if (user != null)
        {
            var audits = await _context.UsersLoginAudits.Where(a => a.Uid == uid).ToListAsync();
            _context.UsersLoginAudits.RemoveRange(audits);

            var resets = await _context.Set<UsersPasswordReset>().Where(r => r.Uid == uid).ToListAsync();
            _context.Set<UsersPasswordReset>().RemoveRange(resets);

            var attendances = await _context.Set<Attendance>().Where(a => a.StudentId == uid).ToListAsync();
            _context.Set<Attendance>().RemoveRange(attendances);

            var examSeats = await _context.Set<ExamSeat>().Where(e => e.StudentId == uid).ToListAsync();
            _context.Set<ExamSeat>().RemoveRange(examSeats);

            var enrollments = await _context.Set<Enrollment>().Where(e => e.StudentId == uid).ToListAsync();
            _context.Set<Enrollment>().RemoveRange(enrollments);

            var bookings = await _context.Set<Booking>().Where(b => b.StudentId == uid).ToListAsync();
            _context.Set<Booking>().RemoveRange(bookings);

            var leaves = await _context.Set<StudentLeave>().Where(l => l.StudentId == uid).ToListAsync();
            _context.Set<StudentLeave>().RemoveRange(leaves);

            var semesterResults = await _context.Set<SemesterResult>().Where(sr => sr.StudentId == uid).ToListAsync();
            var srIds = semesterResults.Select(sr => sr.Id).ToList();
            var courseResults = await _context.Set<CourseResult>().Where(cr => srIds.Contains(cr.SemesterResultId)).ToListAsync();

            _context.Set<CourseResult>().RemoveRange(courseResults);
            _context.Set<SemesterResult>().RemoveRange(semesterResults);

            var bills = await _context.Set<Models.Bill>().Where(b => b.UserId == uid).ToListAsync();
            var billIds = bills.Select(b => b.Id).ToList();
            var payments = await _context.Set<PaymentTransaction>().Where(p => billIds.Contains(p.BillId)).ToListAsync();

            _context.Set<PaymentTransaction>().RemoveRange(payments);
            _context.Set<Models.Bill>().RemoveRange(bills);

            await _context.SaveChangesAsync();
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
        return RedirectToPage();
    }
}