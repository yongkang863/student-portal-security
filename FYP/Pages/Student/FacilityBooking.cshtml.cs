using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using FYP.Database;
using FYP.Models;
using System.Security.Claims;

namespace FYP.Pages.Student
{
    public class FacilityBookingModel : PageModel
    {
        private readonly DB _context;

        public FacilityBookingModel(DB context)
        {
            _context = context;
        }

        public FYP.Models.Facility BookingFacility { get; set; } = default!;

        [BindProperty(SupportsGet = true)]
        public string? SelectedDate { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Please select a time slot.")]
        public string SelectedTimeSlot { get; set; } = default!;

        public List<TimeSlotInfo> DailySlots { get; set; } = new();

        public class TimeSlotInfo
        {
            public TimeOnly Time { get; set; }
            public bool IsAvailable { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            BookingFacility = await _context.Facilities
                .Include(f => f.Photos)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (BookingFacility == null) return NotFound();

            if (string.IsNullOrEmpty(SelectedDate))
            {
                SelectedDate = DateTime.Now.ToString("yyyy-MM-dd");
            }

            // Get logged in user ID
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int studentId = string.IsNullOrEmpty(uidString) ? 1 : int.Parse(uidString);

            // Pass studentId to generate slots
            await GenerateTimeSlotsAsync(BookingFacility.Id, SelectedDate, studentId);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null) return NotFound();

            BookingFacility = await _context.Facilities.FirstOrDefaultAsync(f => f.Id == id);
            if (BookingFacility == null) return NotFound();

            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int studentId = string.IsNullOrEmpty(uidString) ? 1 : int.Parse(uidString);

            if (!ModelState.IsValid)
            {
                await GenerateTimeSlotsAsync(BookingFacility.Id, SelectedDate!, studentId);
                return Page();
            }

            var parsedTime = TimeOnly.Parse(SelectedTimeSlot);
            var parsedDate = DateOnly.Parse(SelectedDate!);

            // 1. Check if it was booked by someone else
            var isAlreadyBooked = await _context.Bookings.AnyAsync(b =>
                b.FacilityId == BookingFacility.Id &&
                b.BookingDate == SelectedDate &&
                b.TimeSlot == parsedTime &&
                (b.StatusId == 1 || b.StatusId == 3));

            if (isAlreadyBooked)
            {
                ModelState.AddModelError("", "Sorry, this time slot was just booked by someone else.");
                await GenerateTimeSlotsAsync(BookingFacility.Id, SelectedDate!, studentId);
                return Page();
            }

            // 2. Check if the student has a timetable conflict (Server-side validation)
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Uid == studentId);
            if (student != null && student.ProgrammeId != null && student.SessionJoined != null)
            {
                // UPDATED LOGIC: Included `<=` to block the end time as well
                var hasClassConflict = await _context.TimetableSlots.AnyAsync(t =>
                    t.ProgrammeId == student.ProgrammeId &&
                    t.SessionJoined == student.SessionJoined &&
                    (t.TutorialGroup == student.TutorialGroup || string.IsNullOrEmpty(t.TutorialGroup) || t.TutorialGroup == "ALL") &&
                    t.Date == parsedDate &&
                    parsedTime >= t.StartTime && parsedTime <= t.EndTime);

                if (hasClassConflict)
                {
                    ModelState.AddModelError("", "Booking failed! You have a scheduled class during this time slot.");
                    await GenerateTimeSlotsAsync(BookingFacility.Id, SelectedDate!, studentId);
                    return Page();
                }
            }

            var newBooking = new Booking
            {
                FacilityId = BookingFacility.Id,
                BookingDate = SelectedDate!,
                TimeSlot = parsedTime,
                StatusId = 3, // Pending Status
                StudentId = studentId
            };

            _context.Bookings.Add(newBooking);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Successfully requested {BookingFacility.Name} on {SelectedDate} at {SelectedTimeSlot}!";
            return RedirectToPage("/Student/Facility");
        }

        private async Task GenerateTimeSlotsAsync(int facilityId, string date, int studentId)
        {
            // 1. Fetch bookings that are Approved (1) or Pending (3) 
            var existingBookings = await _context.Bookings
                .Where(b => b.FacilityId == facilityId && b.BookingDate == date && (b.StatusId == 1 || b.StatusId == 3))
                .Select(b => b.TimeSlot)
                .ToListAsync();

            // 2. Fetch the student's timetable for the selected date
            var parsedDate = DateOnly.Parse(date);
            var studentClasses = new List<TimetableSlot>();
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Uid == studentId);

            if (student != null && student.ProgrammeId != null && student.SessionJoined != null)
            {
                studentClasses = await _context.TimetableSlots
                    .Where(t => t.ProgrammeId == student.ProgrammeId
                             && t.SessionJoined == student.SessionJoined
                             && (t.TutorialGroup == student.TutorialGroup || string.IsNullOrEmpty(t.TutorialGroup) || t.TutorialGroup == "ALL")
                             && t.Date == parsedDate)
                    .ToListAsync();
            }

          
            for (int hour = 8; hour < 20; hour++)
            {
                var currentSlot = new TimeOnly(hour, 0);

                bool isPastToday = false;
                if (date == DateTime.Now.ToString("yyyy-MM-dd"))
                {
                    if (hour <= DateTime.Now.Hour) isPastToday = true;
                }

                
                bool hasClass = studentClasses.Any(c => currentSlot >= c.StartTime && currentSlot <= c.EndTime);

                DailySlots.Add(new TimeSlotInfo
                {
                    Time = currentSlot,
                    IsAvailable = !isPastToday && !existingBookings.Contains(currentSlot) && !hasClass
                });
            }
        }
    }
}