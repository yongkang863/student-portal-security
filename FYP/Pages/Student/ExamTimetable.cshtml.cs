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
    public class ExamTimetableModel : PageModel
    {
        private readonly DB _context;

        public ExamTimetableModel(DB context)
        {
            _context = context;
        }

        public ExamSchedule? CurrentSchedule { get; set; }
        public Models.User? CurrentUser { get; set; }
        public string? StudentPhotoBase64 { get; set; }

        // Helper class to bind slots with the student's specific seat
        public class ExamDetails
        {
            public ExamSlot Slot { get; set; } = null!;
            public string? SeatNumber { get; set; }
            public bool IsUpcoming => Slot.ExamDate >= DateOnly.FromDateTime(DateTime.Today);
        }

        public List<ExamDetails> Exams { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidString) || !int.TryParse(uidString, out int uid))
            {
                return RedirectToPage("/Login");
            }

            CurrentUser = await _context.Users
                .Include(u => u.Programme)
                .FirstOrDefaultAsync(u => u.Uid == uid);

            if (CurrentUser == null || CurrentUser.ProgrammeId == null || CurrentUser.SessionJoined == null)
            {
                return Page();
            }

            // Convert photo to Base64 for the Exam Slip
            if (CurrentUser.Photo != null && CurrentUser.Photo.Length > 0)
            {
                string base64 = Convert.ToBase64String(CurrentUser.Photo);
                StudentPhotoBase64 = $"data:image/jpeg;base64,{base64}";
            }

            // 1. Get the PUBLISHED exam schedule for this student
            CurrentSchedule = await _context.ExamSchedules
                .Include(s => s.Programme)
                .Where(s => s.ProgrammeId == CurrentUser.ProgrammeId
                         && s.SessionJoined == CurrentUser.SessionJoined
                         && s.Status == "Published")
                .FirstOrDefaultAsync();

            if (CurrentSchedule != null)
            {
                // 2. Fetch all exam slots for this schedule
                var slots = await _context.ExamSlots
                    .Include(s => s.Course)
                    .Include(s => s.Venue)
                    .Where(s => s.ScheduleId == CurrentSchedule.Id)
                    .OrderBy(s => s.ExamDate).ThenBy(s => s.StartTime)
                    .ToListAsync();

                // 3. Fetch the assigned seats for this specific student
                var studentSeats = await _context.ExamSeats
                    .Where(seat => seat.StudentId == uid)
                    .ToDictionaryAsync(seat => seat.ExamSlotId, seat => seat.SeatNumber);

                // 4. Bind them together
                foreach (var slot in slots)
                {
                    Exams.Add(new ExamDetails
                    {
                        Slot = slot,
                        SeatNumber = studentSeats.ContainsKey(slot.Id) ? studentSeats[slot.Id] : "TBA"
                    });
                }
            }

            return Page();
        }
    }
}