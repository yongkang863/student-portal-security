using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Academic.CoCurriculum;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class ManageModel : PageModel
{
    private readonly DB _context;

    public ManageModel(DB context) { _context = context; }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public Cocurriculum Activity { get; set; }
    public List<StudentDto> EnrolledStudents { get; set; } = new();
    public List<SelectListItem> EligibleStudentsOptions { get; set; } = new();

    [BindProperty] public UpdateDetailsModel UpdateInput { get; set; }
    [BindProperty] public int? NewStudentId { get; set; }

    [TempData] public string SuccessMessage { get; set; }
    [TempData] public string ErrorMessage { get; set; }

    public class UpdateDetailsModel
    {
        [Required(ErrorMessage = "Code is required.")]
        [StringLength(10, ErrorMessage = "Code cannot exceed 10 characters.")]
        public string Code { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(30, ErrorMessage = "Name cannot exceed 30 characters.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Category is required.")]
        [StringLength(30, ErrorMessage = "Category cannot exceed 30 characters.")]
        public string Category { get; set; }

        [Required(ErrorMessage = "Schedule is required.")]
        [StringLength(40, ErrorMessage = "Schedule cannot exceed 40 characters.")]
        public string DayAndTimeRange { get; set; }

        [Required(ErrorMessage = "Venue is required.")]
        [StringLength(30, ErrorMessage = "Venue cannot exceed 30 characters.")]
        public string Venue { get; set; }

        [Required(ErrorMessage = "Capacity is required.")]
        [Range(1, 1000)]
        public int MaxCapacity { get; set; }
    }

    public class StudentDto
    {
        public int EnrollmentId { get; set; }
        public int StudentId { get; set; }
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string ProgrammeCode { get; set; }
        public DateOnly DateRegistered { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        Activity = await _context.Cocurriculums.FindAsync(Id);
        if (Activity == null) return NotFound();

        UpdateInput = new UpdateDetailsModel
        {
            Code = Activity.Code,
            Name = Activity.Name,
            Category = Activity.Category ?? "",
            DayAndTimeRange = Activity.DayAndTimeRange ?? "",
            Venue = Activity.Venue ?? "",
            MaxCapacity = Activity.MaxCapacity ?? 0
        };

        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateDetailsAsync()
    {
        var activity = await _context.Cocurriculums.FindAsync(Id);
        if (activity == null) return NotFound();

        if (!ModelState.IsValid)
        {
            await LoadDataAsync();
            return Page();
        }

        // Adjust Seat Availability logic if Max Capacity changed
        int currentEnrolledCount = await _context.Enrollments.CountAsync(e => e.CocurriculumId == Id);

        if (UpdateInput.MaxCapacity < currentEnrolledCount)
        {
            ErrorMessage = $"Cannot reduce capacity to {UpdateInput.MaxCapacity}. There are already {currentEnrolledCount} students enrolled. Please remove students first.";
            return RedirectToPage(new { Id });
        }

        activity.Code = UpdateInput.Code;
        activity.Name = UpdateInput.Name;
        activity.Category = UpdateInput.Category;
        activity.DayAndTimeRange = UpdateInput.DayAndTimeRange;
        activity.Venue = UpdateInput.Venue;
        activity.MaxCapacity = UpdateInput.MaxCapacity;

        // Recalculate seats
        activity.SeatAvailable = UpdateInput.MaxCapacity - currentEnrolledCount;

        await _context.SaveChangesAsync();
        SuccessMessage = "Activity details updated successfully.";

        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostRemoveStudentAsync(int enrollmentId)
    {
        var enrollment = await _context.Enrollments.FindAsync(enrollmentId);
        if (enrollment != null && enrollment.CocurriculumId == Id)
        {
            var activity = await _context.Cocurriculums.FindAsync(Id);
            if (activity != null) activity.SeatAvailable += 1;

            _context.Enrollments.Remove(enrollment);
            await _context.SaveChangesAsync();
            SuccessMessage = "Student removed from the activity successfully.";
        }
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostRemoveAllAsync()
    {
        var enrollments = await _context.Enrollments.Where(e => e.CocurriculumId == Id).ToListAsync();
        if (enrollments.Any())
        {
            var activity = await _context.Cocurriculums.FindAsync(Id);
            if (activity != null) activity.SeatAvailable = activity.MaxCapacity;

            _context.Enrollments.RemoveRange(enrollments);
            await _context.SaveChangesAsync();
            SuccessMessage = $"Successfully cleared roster. Removed {enrollments.Count} students.";
        }
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostEnrollStudentAsync()
    {
        if (!NewStudentId.HasValue) return RedirectToPage(new { Id });

        var activity = await _context.Cocurriculums.FindAsync(Id);
        if (activity == null) return NotFound();

        if ((activity.SeatAvailable ?? 0) <= 0)
        {
            ErrorMessage = "Cannot enroll student. The activity is currently full.";
            return RedirectToPage(new { Id });
        }

        // Strict check: Student can only join ONE co-curriculum total across the system
        bool isAlreadyEnrolledInAnything = await _context.Enrollments.AnyAsync(e => e.StudentId == NewStudentId.Value);
        if (isAlreadyEnrolledInAnything)
        {
            ErrorMessage = "Registration failed. This student is already enrolled in a co-curriculum activity.";
            return RedirectToPage(new { Id });
        }

        _context.Enrollments.Add(new Enrollment
        {
            CocurriculumId = Id,
            StudentId = NewStudentId.Value,
            DateRegistered = DateOnly.FromDateTime(DateTime.Today)
        });

        activity.SeatAvailable -= 1;
        await _context.SaveChangesAsync();

        SuccessMessage = "Student manually enrolled successfully.";
        return RedirectToPage(new { Id });
    }

    private async Task LoadDataAsync()
    {
        Activity ??= await _context.Cocurriculums.FindAsync(Id);

        // 1. Load enrolled students (Explicit mapping using Dictionary lookup)
        var enrollments = await _context.Enrollments
            .Where(e => e.CocurriculumId == Id)
            .ToListAsync();

        var studentIds = enrollments.Select(e => e.StudentId).ToList();

        var students = await _context.Users
            .Include(u => u.Programme)
            .Where(u => studentIds.Contains(u.Uid))
            .ToDictionaryAsync(u => u.Uid);

        foreach (var e in enrollments)
        {
            if (students.TryGetValue(e.StudentId, out var student))
            {
                EnrolledStudents.Add(new StudentDto
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    UserId = student.UserId,
                    FullName = student.FullName,
                    ProgrammeCode = student.Programme?.Code ?? "N/A",
                    DateRegistered = e.DateRegistered
                });
            }
        }

        // 2. Load eligible students for the search dropdown (Active Students NOT enrolled in ANYTHING)
        var allEnrolledStudentIds = await _context.Enrollments.Select(e => e.StudentId).ToListAsync();

        EligibleStudentsOptions = await _context.Users
            .Where(u => u.UsersRoleId == 3 && u.StudyStatus == "Active" && !allEnrolledStudentIds.Contains(u.Uid))
            .OrderBy(u => u.FullName)
            .Select(u => new SelectListItem { Value = u.Uid.ToString(), Text = $"{u.UserId} - {u.FullName}" })
            .ToListAsync();
    }
}