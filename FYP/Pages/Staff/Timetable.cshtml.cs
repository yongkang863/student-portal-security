using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FYP.Pages.Staff;

[Authorize(Policy = "StaffOnly", AuthenticationSchemes = "StaffAuth")]
public class TimetableModel : PageModel
{
    private readonly DB _context;

    public TimetableModel(DB context)
    {
        _context = context;
    }

    public List<TimetableSlot> Slots { get; set; } = new();
    public List<TimetableSlot> DailyClasses { get; set; } = new();

    public DateOnly StartOfWeek { get; set; }
    public DateOnly EndOfWeek { get; set; }
    public DateOnly CurrentViewDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? SelectedDate { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var uidString = User.FindFirst("Uid")?.Value ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        int currentStaffId = int.TryParse(uidString, out int id) ? id : HttpContext.Session.GetInt32("Uid") ?? 0;

        if (currentStaffId == 0) return RedirectToPage("/Staff/Login");

        DateTime currentDate = SelectedDate ?? DateTime.Today;
        CurrentViewDate = DateOnly.FromDateTime(currentDate);

        int diff = (7 + (currentDate.DayOfWeek - DayOfWeek.Sunday)) % 7;
        var startDt = currentDate.AddDays(-1 * diff).Date;
        var endDt = startDt.AddDays(6).Date;

        StartOfWeek = DateOnly.FromDateTime(startDt);
        EndOfWeek = DateOnly.FromDateTime(endDt);

        Slots = await _context.TimetableSlots
            .Include(t => t.Course)
            .Include(t => t.Venue)
            .Where(t => t.StaffId == currentStaffId
                     && t.Date >= StartOfWeek
                     && t.Date <= EndOfWeek)
            .ToListAsync();

        DailyClasses = Slots.Where(s => s.Date == CurrentViewDate).OrderBy(s => s.StartTime).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateCodeAsync(int slotId)
    {
        // --- FIX: Robust ID Extraction ---
        var uidString = User.FindFirst("Uid")?.Value ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        int currentStaffId = int.TryParse(uidString, out int id) ? id : HttpContext.Session.GetInt32("Uid") ?? 0;

        if (currentStaffId == 0) return new JsonResult(new { success = false, message = "Session expired. Please log in again." });

        var slot = await _context.TimetableSlots.FirstOrDefaultAsync(s => s.Id == slotId && s.StaffId == currentStaffId);
        if (slot == null) return new JsonResult(new { success = false, message = "Invalid slot or unauthorized." });

        var currentDateTime = DateTime.Now;
        var slotStart = slot.Date.ToDateTime(slot.StartTime);
        var slotEnd = slot.Date.ToDateTime(slot.EndTime);

        if (currentDateTime < slotStart || currentDateTime > slotEnd)
        {
            return new JsonResult(new { success = false, message = "Code can only be generated during the active class time." });
        }

        var random = new Random();
        slot.AttendanceCode = random.Next(100000, 999999);
        await _context.SaveChangesAsync();

        return new JsonResult(new { success = true, code = slot.AttendanceCode });
    }

    public async Task<IActionResult> OnGetClassListAsync(int slotId)
    {
        var slot = await _context.TimetableSlots.FirstOrDefaultAsync(s => s.Id == slotId);
        if (slot == null) return new JsonResult(new { success = false, message = "Slot not found." });

        var currentDateTime = DateTime.Now;
        var slotStart = slot.Date.ToDateTime(slot.StartTime);
        if (currentDateTime < slotStart)
        {
            return new JsonResult(new { success = false, message = "Class has not started yet." });
        }

        // --- FIX: Checking the RoleName property inside the Role object ---
        // If your User model uses a foreign key ID instead of a navigation property, 
        // you can change this to: u.RoleId == 3 (assuming 3 is Student based on your DB screenshot)
        var studentsQuery = _context.Users.Where(u => u.Role.RoleName == "Student"
                                              && u.ProgrammeId == slot.ProgrammeId
                                              && u.SessionJoined == slot.SessionJoined);

        if (!string.IsNullOrEmpty(slot.TutorialGroup) && slot.TutorialGroup != "ALL")
        {
            studentsQuery = studentsQuery.Where(u => u.TutorialGroup == slot.TutorialGroup);
        }

        var enrolledStudents = await studentsQuery.Select(u => new { u.Uid, u.FullName, u.TutorialGroup }).ToListAsync();
        var studentIds = enrolledStudents.Select(s => s.Uid).ToList();

        var attendanceRecords = await _context.Attendances.Where(a => a.SlotId == slotId).ToListAsync();

        var result = enrolledStudents.Select(student => {
            var att = attendanceRecords.FirstOrDefault(a => a.StudentId == student.Uid);

            string displayIp = "-";
            if (att != null && att.UpdateMethod == "Code" && !string.IsNullOrEmpty(att.Remark) && att.Remark.Contains("IP:"))
            {
                displayIp = att.Remark.Split("IP:").LastOrDefault()?.Trim() ?? "-";
            }

            return new
            {
                studentId = student.Uid,
                studentName = student.FullName,
                tutorialGroup = student.TutorialGroup ?? "N/A",
                isPresent = att != null && att.Status == "Present",
                updateMethod = att?.UpdateMethod ?? "-",
                takenTime = att?.TakenTime?.ToString("hh:mm tt") ?? "-",
                ipAddress = displayIp
            };
        }).OrderBy(s => s.studentName).ToList();

        return new JsonResult(new { success = true, data = result });
    }

    public class SaveAttendanceRequest
    {
        public int SlotId { get; set; }
        public List<int> PresentStudentIds { get; set; } = new();
    }

    public async Task<IActionResult> OnPostSaveAttendanceAsync([FromBody] SaveAttendanceRequest request)
    {
        // --- FIX: Robust ID Extraction ---
        var uidString = User.FindFirst("Uid")?.Value ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        int currentStaffId = int.TryParse(uidString, out int id) ? id : HttpContext.Session.GetInt32("Uid") ?? 0;

        if (currentStaffId == 0) return new JsonResult(new { success = false, message = "Session expired. Please log in again." });

        var slot = await _context.TimetableSlots.FirstOrDefaultAsync(s => s.Id == request.SlotId && s.StaffId == currentStaffId);
        if (slot == null) return new JsonResult(new { success = false, message = "Invalid slot or you are not authorized for this class." });

        var existingRecords = await _context.Attendances.Where(a => a.SlotId == request.SlotId).ToListAsync();

        foreach (var studentId in request.PresentStudentIds)
        {
            var record = existingRecords.FirstOrDefault(a => a.StudentId == studentId);
            if (record == null)
            {
                _context.Attendances.Add(new Attendance
                {
                    SlotId = request.SlotId,
                    StudentId = studentId,
                    Status = "Present",
                    TakenTime = DateTime.Now,
                    UpdateMethod = "Manual",
                    Remark = "Marked by Lecturer",
                    StaffId = currentStaffId
                });
            }
            else
            {
                record.Status = "Present";
                if (record.UpdateMethod != "Code") // Don't overwrite Code method if they already marked it themselves
                {
                    record.UpdateMethod = "Manual";
                    record.Remark = "Marked by Lecturer";
                }
                record.StaffId = currentStaffId;
            }
        }

        var absentStudents = existingRecords.Where(a => !request.PresentStudentIds.Contains(a.StudentId)).ToList();
        foreach (var absent in absentStudents)
        {
            absent.Status = "Absent";
            absent.UpdateMethod = "Manual";
            absent.Remark = "Marked absent by Lecturer";
            absent.StaffId = currentStaffId;
        }

        await _context.SaveChangesAsync();
        return new JsonResult(new { success = true });
    }
}