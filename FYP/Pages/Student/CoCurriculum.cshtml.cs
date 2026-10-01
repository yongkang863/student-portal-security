using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Data;

namespace FYP.Pages.Student
{
    [Authorize(Policy = "StudentOnly", AuthenticationSchemes = "StudentCookie")]
    public class CoCurriculumModel : PageModel
    {
        private readonly DB _context;

        public CoCurriculumModel(DB context)
        {
            _context = context;
        }

        public List<Cocurriculum> Activities { get; set; } = new();
        public List<int> MyEnrollmentIds { get; set; } = new();

        public DateTime? RegistrationStart { get; set; }
        public DateTime? RegistrationEnd { get; set; }

        [TempData] public string SuccessMessage { get; set; }
        [TempData] public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uidString, out int uid)) return RedirectToPage("/Student/Login");

            Activities = await _context.Cocurriculums
                .OrderBy(c => c.Category)
                .ThenBy(c => c.Name)
                .ToListAsync();

            var firstActive = Activities.FirstOrDefault(a => a.StartDate.HasValue);
            if (firstActive != null)
            {
                RegistrationStart = firstActive.StartDate;
                RegistrationEnd = firstActive.EndDate;
            }

            MyEnrollmentIds = await _context.Enrollments
                .Where(e => e.StudentId == uid)
                .Select(e => e.CocurriculumId)
                .ToListAsync();

            return Page();
        }

        public async Task<JsonResult> OnGetSeatDataAsync()
        {
            var now = DateTime.Now;
            var data = await _context.Cocurriculums
                .Select(c => new
                {
                    id = c.Id,
                    seats = c.SeatAvailable ?? 0,
                    isNotStarted = c.StartDate.HasValue && now < c.StartDate.Value,
                    isEnded = c.EndDate.HasValue && now > c.EndDate.Value,
                    isDateMissing = !c.StartDate.HasValue
                })
                .ToListAsync();

            return new JsonResult(data);
        }

        public async Task<IActionResult> OnPostRegisterAsync(int id)
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uidString, out int uid)) return RedirectToPage("/Student/Login");

            var currentUser = await _context.Users.FindAsync(uid);
            if (currentUser == null) return NotFound();

            // Check if Role is 3 and Status is "Active"
            if (currentUser.UsersRoleId != 3 || currentUser.StudyStatus != "Active")
            {
                ErrorMessage = "Registration failed. Only active students are eligible to register for co-curriculum activities.";
                return RedirectToPage();
            }

            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                bool hasAnyEnrollment = await _context.Enrollments.AnyAsync(e => e.StudentId == uid);
                if (hasAnyEnrollment)
                {
                    ErrorMessage = "Registration failed. You are only allowed to join one activity.";
                    return RedirectToPage();
                }

                var activity = await _context.Cocurriculums.FindAsync(id);
                if (activity == null) return NotFound();

                DateTime now = DateTime.Now;
                if (!activity.StartDate.HasValue)
                {
                    ErrorMessage = "Registration is not yet open for this activity.";
                    return RedirectToPage();
                }
                if (now < activity.StartDate.Value)
                {
                    ErrorMessage = $"Registration opens on {activity.StartDate.Value:dd MMM yyyy, hh:mm tt}.";
                    return RedirectToPage();
                }
                if (activity.EndDate.HasValue && now > activity.EndDate.Value)
                {
                    ErrorMessage = "Registration has closed.";
                    return RedirectToPage();
                }

                if ((activity.SeatAvailable ?? 0) <= 0)
                {
                    ErrorMessage = "Registration failed. The last seat was just taken.";
                    return RedirectToPage();
                }

                var enrollment = new Enrollment
                {
                    StudentId = uid,
                    CocurriculumId = id,
                    DateRegistered = DateOnly.FromDateTime(now)
                };

                activity.SeatAvailable -= 1;
                _context.Enrollments.Add(enrollment);
                _context.Cocurriculums.Update(activity);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                SuccessMessage = $"Successfully registered for {activity.Name}!";
            }
            catch
            {
                await transaction.RollbackAsync();
                ErrorMessage = "System busy. Please try again.";
            }

            return RedirectToPage();
        }

        // --- UPDATED DROP LOGIC ---
        public async Task<IActionResult> OnPostDropAsync(int id)
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uidString, out int uid)) return RedirectToPage("/Student/Login");

            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // 1. Fetch Activity First to check Date
                var activity = await _context.Cocurriculums.FindAsync(id);
                if (activity == null) return NotFound();

                // 2. CHECK DATE: Prevent dropping if closed
                if (activity.EndDate.HasValue && DateTime.Now > activity.EndDate.Value)
                {
                    ErrorMessage = "Cannot drop activity. The registration period has ended.";
                    return RedirectToPage();
                }

                // 3. Find Enrollment
                var enrollment = await _context.Enrollments.FirstOrDefaultAsync(e => e.StudentId == uid && e.CocurriculumId == id);
                if (enrollment == null) return RedirectToPage();

                // 4. Return Seat
                activity.SeatAvailable += 1;
                _context.Cocurriculums.Update(activity);

                _context.Enrollments.Remove(enrollment);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                SuccessMessage = "Activity dropped successfully.";
            }
            catch
            {
                await transaction.RollbackAsync();
                ErrorMessage = "Error dropping activity.";
            }

            return RedirectToPage();
        }
    }
}