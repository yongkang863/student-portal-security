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
    public class AttendanceRegistrationModel : PageModel
    {
        private readonly DB _context;

        public AttendanceRegistrationModel(DB context)
        {
            _context = context;
        }

        [BindProperty]
        public string? InputCode { get; set; }

        public TimetableSlot? ActiveSlot { get; set; }
        public bool AlreadyRecorded { get; set; } = false;

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadActiveSlotAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(InputCode) || InputCode.Length != 6 || !int.TryParse(InputCode, out int parsedCode))
            {
                TempData["ErrorMessage"] = "Please enter a valid 6-digit numeric code.";
                await LoadActiveSlotAsync();
                return Page();
            }

            await LoadActiveSlotAsync();

            if (ActiveSlot == null)
            {
                TempData["ErrorMessage"] = "No active class found at this time to mark attendance.";
                return Page();
            }

            if (AlreadyRecorded)
            {
                TempData["InfoMessage"] = "Your attendance for this class is already recorded.";
                return Page();
            }

            if (ActiveSlot.AttendanceCode != parsedCode)
            {
                TempData["ErrorMessage"] = "Invalid attendance code. Please try again.";
                return Page();
            }

            // Code matches, record attendance
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int uid = int.Parse(uidString!);

            // Capture the LIVE IP address of the device submitting the code
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

            var newAttendance = new Attendance
            {
                Status = "Present",
                TakenTime = DateTime.Now,
                UpdateMethod = "Code",
                Remark = $"Code verified. IP: {ipAddress}",
                SlotId = ActiveSlot.Id,
                StudentId = uid
            };

            _context.Attendances.Add(newAttendance);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Attendance marked successfully!";
            AlreadyRecorded = true;

            return Page();
        }

        private async Task LoadActiveSlotAsync()
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(uidString) && int.TryParse(uidString, out int uid))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Uid == uid);
                if (user != null && user.ProgrammeId != null && user.SessionJoined != null)
                {
                    var today = DateOnly.FromDateTime(DateTime.Today);
                    var timeNow = TimeOnly.FromDateTime(DateTime.Now);

                    ActiveSlot = await _context.TimetableSlots
                        .Include(t => t.Course)
                        .Include(t => t.Venue)
                        .Where(t => t.ProgrammeId == user.ProgrammeId
                                 && t.SessionJoined == user.SessionJoined
                                 && (t.TutorialGroup == user.TutorialGroup || string.IsNullOrEmpty(t.TutorialGroup) || t.TutorialGroup == "ALL")
                                 && t.Date == today
                                 && t.StartTime <= timeNow
                                 && t.EndTime >= timeNow)
                        .FirstOrDefaultAsync();

                    if (ActiveSlot != null)
                    {
                        AlreadyRecorded = await _context.Attendances
                            .AnyAsync(a => a.SlotId == ActiveSlot.Id && a.StudentId == uid);
                    }
                }
            }
        }
    }
}