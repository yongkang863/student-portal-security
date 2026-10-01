using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Academic.Advisory;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class IndexModel : PageModel
{
    private readonly DB _context;

    public IndexModel(DB context)
    {
        _context = context;
    }

    // --- Search & Filter Properties ---
    [BindProperty(SupportsGet = true)] public int? DeptId { get; set; }
    [BindProperty(SupportsGet = true)] public int? StaffId { get; set; }
    [BindProperty(SupportsGet = true)] public int? ProgId { get; set; }
    [BindProperty(SupportsGet = true)] public int? SessionJoined { get; set; }
    [BindProperty(SupportsGet = true)] public string? TutorialGroup { get; set; }

    // --- Data to Display ---
    public User? SelectedStaff { get; set; }
    public Models.Advisory? CurrentAdvisory { get; set; }
    public List<StudentAdvisoryDto> StudentList { get; set; } = new();

    // --- Input for Editing Advisory Details ---
    [BindProperty] public AdvisoryEditModel AdvisoryEdit { get; set; } = new();

    // --- Dropdowns ---
    public List<SelectListItem> DepartmentOptions { get; set; } = new();
    public List<SelectListItem> StaffOptions { get; set; } = new();
    public List<SelectListItem> ProgrammeOptions { get; set; } = new();
    public List<SelectListItem> SessionOptions { get; set; } = new();
    public List<SelectListItem> TutorialGroupOptions { get; set; } = new();

    [TempData] public string SuccessMessage { get; set; }
    [TempData] public string ErrorMessage { get; set; }

    public class StudentAdvisoryDto
    {
        public int Uid { get; set; }
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string ProgrammeCode { get; set; }
        public int? SessionJoined { get; set; }
        public string TutorialGroup { get; set; }
        public string CurrentAdvisorName { get; set; }
        public bool IsAssignedToCurrentStaff { get; set; }
    }

    public class AdvisoryEditModel
    {
        [StringLength(40, ErrorMessage = "Office Location cannot exceed 40 characters.")]
        public string? OfficeLocation { get; set; }

        [StringLength(20, ErrorMessage = "Office Extension cannot exceed 20 characters.")]
        public string? OfficeExtension { get; set; }

        [StringLength(40, ErrorMessage = "Consultation Hours cannot exceed 40 characters.")]
        public string? ConsultationHours { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadDropdownsAsync();

        if (DeptId.HasValue && StaffId.HasValue)
        {
            SelectedStaff = await _context.Users.FirstOrDefaultAsync(u => u.Uid == StaffId.Value && u.UsersRoleId == 2);

            if (SelectedStaff != null)
            {
                // Fetch Advisory Details
                CurrentAdvisory = await _context.Set<Models.Advisory>().FirstOrDefaultAsync(a => a.StaffId == StaffId.Value);
                if (CurrentAdvisory != null)
                {
                    AdvisoryEdit.OfficeLocation = CurrentAdvisory.OfficeLocation;
                    AdvisoryEdit.OfficeExtension = CurrentAdvisory.OfficeExtension;
                    AdvisoryEdit.ConsultationHours = CurrentAdvisory.ConsultationHours;
                }

                // Base query: Students in the SAME department as the selected staff
                var query = _context.Users
                    .Include(u => u.Programme)
                    .Include(u => u.Advisory).ThenInclude(a => a.Staff)
                    .Where(u => u.UsersRoleId == 3 && u.DepartmentId == DeptId.Value);

                // Apply Student Filters
                if (ProgId.HasValue) query = query.Where(u => u.ProgrammeId == ProgId.Value);
                if (SessionJoined.HasValue) query = query.Where(u => u.SessionJoined == SessionJoined.Value);
                if (!string.IsNullOrEmpty(TutorialGroup)) query = query.Where(u => u.TutorialGroup == TutorialGroup);

                var rawStudents = await query.OrderBy(u => u.Programme.Code).ThenBy(u => u.SessionJoined).ThenBy(u => u.FullName).ToListAsync();

                // Map to DTO for the View
                foreach (var s in rawStudents)
                {
                    StudentList.Add(new StudentAdvisoryDto
                    {
                        Uid = s.Uid,
                        UserId = s.UserId,
                        FullName = s.FullName,
                        ProgrammeCode = s.Programme?.Code ?? "N/A",
                        SessionJoined = s.SessionJoined,
                        TutorialGroup = s.TutorialGroup ?? "N/A",
                        CurrentAdvisorName = s.Advisory?.Staff?.FullName ?? "Unassigned",
                        IsAssignedToCurrentStaff = s.Advisory?.StaffId == StaffId.Value
                    });
                }
            }
        }
    }

    // --- HANDLER: Update Advisory Details ---
    public async Task<IActionResult> OnPostUpdateAdvisoryAsync()
    {
        if (!DeptId.HasValue || !StaffId.HasValue) return RedirectToPage(new { DeptId, StaffId, ProgId, SessionJoined, TutorialGroup });

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Failed to update details: One or more fields exceeded the maximum character limit.";
            return RedirectToPage(new { DeptId, StaffId, ProgId, SessionJoined, TutorialGroup });
        }

        var advisory = await _context.Set<Models.Advisory>().FirstOrDefaultAsync(a => a.StaffId == StaffId.Value);

        if (advisory == null)
        {
            // Create if doesn't exist
            advisory = new Models.Advisory { StaffId = StaffId.Value };
            _context.Set<Models.Advisory>().Add(advisory);
        }

        advisory.OfficeLocation = AdvisoryEdit.OfficeLocation;
        advisory.OfficeExtension = AdvisoryEdit.OfficeExtension;
        advisory.ConsultationHours = AdvisoryEdit.ConsultationHours;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Advisory details (Consultation Hours, Location) updated successfully.";

        return RedirectToPage(new { DeptId, StaffId, ProgId, SessionJoined, TutorialGroup });
    }

    // --- HANDLER: Update Student Assignments ---
    public async Task<IActionResult> OnPostSaveAssignmentsAsync(List<int> SelectedStudentIds, string DisplayedStudentIds)
    {
        if (!DeptId.HasValue || !StaffId.HasValue) return RedirectToPage();

        // 1. Ensure Advisory record exists for ID generation
        var advisory = await _context.Set<Models.Advisory>().FirstOrDefaultAsync(a => a.StaffId == StaffId.Value);
        if (advisory == null)
        {
            advisory = new Models.Advisory { StaffId = StaffId.Value };
            _context.Set<Models.Advisory>().Add(advisory);
            await _context.SaveChangesAsync();
        }

        // 2. Parse UIDs currently visible on screen
        var displayedIds = string.IsNullOrEmpty(DisplayedStudentIds)
            ? new List<int>() : DisplayedStudentIds.Split(',').Select(int.Parse).ToList();

        if (!displayedIds.Any()) return RedirectToPage(new { DeptId, StaffId, ProgId, SessionJoined, TutorialGroup });

        // 3. Update the students
        var studentsToUpdate = await _context.Users.Where(u => displayedIds.Contains(u.Uid)).ToListAsync();
        int assignedCount = 0, unassignedCount = 0;

        foreach (var student in studentsToUpdate)
        {
            bool isChecked = SelectedStudentIds.Contains(student.Uid);

            if (isChecked)
            {
                if (student.AdvisoryId != advisory.Id)
                {
                    student.AdvisoryId = advisory.Id;
                    assignedCount++;
                }
            }
            else
            {
                if (student.AdvisoryId == advisory.Id)
                {
                    student.AdvisoryId = null;
                    unassignedCount++;
                }
            }
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Student assignments updated. Added {assignedCount} and removed {unassignedCount}.";

        return RedirectToPage(new { DeptId, StaffId, ProgId, SessionJoined, TutorialGroup });
    }

    private async Task LoadDropdownsAsync()
    {
        DepartmentOptions = await _context.Set<Department>()
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.DepartmentName })
            .ToListAsync();

        if (DeptId.HasValue)
        {
            StaffOptions = await _context.Users
                .Where(u => u.UsersRoleId == 2 && u.DepartmentId == DeptId.Value)
                .Select(u => new SelectListItem { Value = u.Uid.ToString(), Text = u.FullName })
                .ToListAsync();

            ProgrammeOptions = await _context.Set<Models.Programme>()
                .Where(p => p.DepartmentId == DeptId.Value)
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = $"{p.Code} - {p.Name}" })
                .ToListAsync();

            var sessions = await _context.Users
                .Where(u => u.UsersRoleId == 3 && u.DepartmentId == DeptId.Value && u.SessionJoined != null)
                .Select(u => u.SessionJoined.Value).Distinct().ToListAsync();
            SessionOptions = sessions.OrderByDescending(s => s).Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString() }).ToList();

            var groups = await _context.Users
                .Where(u => u.UsersRoleId == 3 && u.DepartmentId == DeptId.Value && !string.IsNullOrEmpty(u.TutorialGroup))
                .Select(u => u.TutorialGroup).Distinct().ToListAsync();
            TutorialGroupOptions = groups.OrderBy(g => g).Select(g => new SelectListItem { Value = g, Text = g }).ToList();
        }
    }
}