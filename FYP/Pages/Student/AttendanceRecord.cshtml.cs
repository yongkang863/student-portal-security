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
    public class AttendanceRecordModel : PageModel
    {
        private readonly DB _context;

        public AttendanceRecordModel(DB context)
        {
            _context = context;
        }

        public class AttendanceRecordItem
        {
            public TimetableSlot Slot { get; set; } = null!;
            public string Status { get; set; } = string.Empty;
            public double Hours { get; set; }
        }

        public class CourseAttendanceSummary
        {
            public int CourseId { get; set; }
            public string CourseCode { get; set; } = string.Empty;
            public string CourseTitle { get; set; } = string.Empty;
            public double TotalHours { get; set; }
            public double AttendedHours { get; set; }
            public double Percentage => TotalHours > 0 ? Math.Round((AttendedHours / TotalHours) * 100, 1) : 0;
        }

        public List<AttendanceRecordItem> PastClasses { get; set; } = new();
        public List<CourseAttendanceSummary> CourseSummaries { get; set; } = new();

        public double OverallTotalHours { get; set; }
        public double OverallAttendedHours { get; set; }
        public double OverallPercentage => OverallTotalHours > 0 ? Math.Round((OverallAttendedHours / OverallTotalHours) * 100, 1) : 0;

        public async Task<IActionResult> OnGetAsync()
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidString) || !int.TryParse(uidString, out int uid))
            {
                return RedirectToPage("/Login");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Uid == uid);
            if (user == null || user.ProgrammeId == null || user.SessionJoined == null)
            {
                return Page();
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            var timeNow = TimeOnly.FromDateTime(DateTime.Now);

            // 1. Fetch all slots that have ALREADY ENDED for this student
            var pastSlots = await _context.TimetableSlots
                .Include(t => t.Course)
                .Where(t => t.ProgrammeId == user.ProgrammeId
                         && t.SessionJoined == user.SessionJoined
                         && (t.TutorialGroup == user.TutorialGroup || string.IsNullOrEmpty(t.TutorialGroup) || t.TutorialGroup == "ALL")
                         && (t.Date < today || (t.Date == today && t.EndTime <= timeNow)))
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.StartTime)
                .ToListAsync();

            // 2. Fetch all recorded attendances
            var studentAttendances = await _context.Attendances
                .Where(a => a.StudentId == uid)
                .ToDictionaryAsync(a => a.SlotId, a => a.Status);

            var summaryDict = new Dictionary<int, CourseAttendanceSummary>();

            // 3. Process each class and calculate hours
            foreach (var slot in pastSlots)
            {
                // Calculate duration in hours
                TimeSpan duration = slot.EndTime - slot.StartTime;
                double hours = duration.TotalHours;

                OverallTotalHours += hours;

                // Initialize course summary if not exists
                if (!summaryDict.ContainsKey(slot.CourseId))
                {
                    summaryDict[slot.CourseId] = new CourseAttendanceSummary
                    {
                        CourseId = slot.CourseId,
                        CourseCode = slot.Course?.Code ?? "Unknown",
                        CourseTitle = slot.Course?.Title ?? "Unknown Course"
                    };
                }

                summaryDict[slot.CourseId].TotalHours += hours;

                string status = studentAttendances.ContainsKey(slot.Id) ? studentAttendances[slot.Id] : "Absent";

                if (status.Equals("Present", StringComparison.OrdinalIgnoreCase))
                {
                    OverallAttendedHours += hours;
                    summaryDict[slot.CourseId].AttendedHours += hours;
                }

                PastClasses.Add(new AttendanceRecordItem
                {
                    Slot = slot,
                    Status = status,
                    Hours = hours
                });
            }

            CourseSummaries = summaryDict.Values.OrderBy(c => c.CourseCode).ToList();

            return Page();
        }
    }
}