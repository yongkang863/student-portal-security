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
    public class TimetableModel : PageModel
    {
        private readonly DB _context;

        public TimetableModel(DB context)
        {
            _context = context;
        }

        public List<TimetableSlot> Slots { get; set; } = new();
        public List<TimetableSlot> DailyClasses { get; set; } = new();
        public Dictionary<int, string> StaffNames { get; set; } = new();

        public DateOnly StartOfWeek { get; set; }
        public DateOnly EndOfWeek { get; set; }
        public DateOnly CurrentViewDate { get; set; }
        public int? UpcomingSlotId { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? SelectedDate { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidString) || !int.TryParse(uidString, out int uid))
            {
                return RedirectToPage("/Login");
            }

            // --- MOVE DATE SETUP HERE ---
            // So the page always has dates to render, preventing the crash
            DateTime currentDate = SelectedDate ?? DateTime.Today;
            CurrentViewDate = DateOnly.FromDateTime(currentDate);

            int diff = (7 + (currentDate.DayOfWeek - DayOfWeek.Sunday)) % 7;
            var startDt = currentDate.AddDays(-1 * diff).Date;
            var endDt = startDt.AddDays(6).Date;

            StartOfWeek = DateOnly.FromDateTime(startDt);
            EndOfWeek = DateOnly.FromDateTime(endDt);
            // ----------------------------

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Uid == uid);

            if (user == null || user.ProgrammeId == null || user.SessionJoined == null)
            {
                // Now it's safe to return the page; dates are set, slots will just be empty.
                return Page();
            }

            // Fetch timetable slots specifically for this student's cohort
            Slots = await _context.TimetableSlots
                .Include(t => t.Course)
                .Include(t => t.Venue)
                .Where(t => t.ProgrammeId == user.ProgrammeId
                         && t.SessionJoined == user.SessionJoined
                         && (t.TutorialGroup == user.TutorialGroup || string.IsNullOrEmpty(t.TutorialGroup) || t.TutorialGroup == "ALL")
                         && t.Date >= StartOfWeek
                         && t.Date <= EndOfWeek)
                .ToListAsync();

            // Fetch Lecturer (Staff) Names
            var staffIds = Slots.Where(s => s.StaffId.HasValue).Select(s => s.StaffId.Value).Distinct().ToList();
            if (staffIds.Any())
            {
                StaffNames = await _context.Users
                    .Where(u => staffIds.Contains(u.Uid))
                    .ToDictionaryAsync(u => u.Uid, u => u.FullName);
            }

            // Identify classes for the specific selected day
            DailyClasses = Slots.Where(s => s.Date == CurrentViewDate).OrderBy(s => s.StartTime).ToList();

            // Identify the upcoming class (only relevant if viewing actual today)
            if (CurrentViewDate == DateOnly.FromDateTime(DateTime.Today))
            {
                var timeNow = TimeOnly.FromDateTime(DateTime.Now);
                UpcomingSlotId = DailyClasses
                    .Where(s => s.StartTime > timeNow)
                    .Select(s => s.Id)
                    .FirstOrDefault();
            }

            return Page();
        }
    }
}