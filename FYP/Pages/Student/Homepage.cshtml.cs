using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FYP.Database;
using FYP.Models;

namespace FYP.Pages.Student
{
    [Authorize(Policy = "StudentOnly", AuthenticationSchemes = "StudentCookie")]
    public class HomepageModel : PageModel
    {
        private readonly DB _context;

        public HomepageModel(DB context)
        {
            _context = context;
        }

        public Models.User? CurrentUser { get; set; }
        public decimal CurrentCgpa { get; set; } = 0;
        public int TotalCreditsEarned { get; set; } = 0;
        public decimal OutstandingBalance { get; set; } = 0;

        public class ExamInfo
        {
            public string CourseCode { get; set; } = "";
            public string CourseTitle { get; set; } = "";
            public DateOnly ExamDate { get; set; }
            public TimeOnly StartTime { get; set; }
            public string Venue { get; set; } = "";
        }

        public List<ExamInfo> UpcomingExams { get; set; } = new();
        public List<Announcement> RecentAnnouncements { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidString) || !int.TryParse(uidString, out int uid))
            {
                return RedirectToPage("/Login");
            }

            // 1. Get Student Details & Programme
            CurrentUser = await _context.Users
                .Include(u => u.Programme)
                .FirstOrDefaultAsync(u => u.Uid == uid && u.UsersRoleId == 3);

            if (CurrentUser == null) return RedirectToPage("/Login");

            // 2. Get CGPA & Credits Earned
            var latestResult = await _context.SemesterResults
                .Where(sr => sr.StudentId == uid)
                .OrderByDescending(sr => sr.YearLevel)
                .ThenByDescending(sr => sr.Semester)
                .FirstOrDefaultAsync();

            if (latestResult != null)
            {
                CurrentCgpa = latestResult.Cgpa ?? 0;

                // Calculate credits earned from passed courses
                var allPassedCourses = await _context.CourseResults
                    .Include(cr => cr.Course)
                    .Where(cr => cr.SemesterResult!.StudentId == uid && cr.GradePointEarned > 0)
                    .ToListAsync();

                TotalCreditsEarned = allPassedCourses.Sum(cr => cr.Course?.CreditHours ?? 0);
            }

            // 3. Get Outstanding Financial Balance (Assuming StatusId 1 = Pending/Unpaid)
            OutstandingBalance = await _context.Bills
                .Where(b => b.UserId == uid && b.StatusId == 1)
                .SumAsync(b => b.Amount);

            // 4. Get Next 3 Upcoming Exams
            var today = DateOnly.FromDateTime(DateTime.Today);
            var examSeats = await _context.ExamSeats
                .Include(es => es.ExamSlot)
                .ThenInclude(slot => slot!.Course)
                .Include(es => es.ExamSlot!.Venue)
                .Where(es => es.StudentId == uid && es.ExamSlot!.ExamDate >= today)
                .OrderBy(es => es.ExamSlot!.ExamDate)
                .ThenBy(es => es.ExamSlot!.StartTime)
                .Take(3)
                .ToListAsync();

            UpcomingExams = examSeats.Select(es => new ExamInfo
            {
                CourseCode = es.ExamSlot?.Course?.Code ?? "N/A",
                CourseTitle = es.ExamSlot?.Course?.Title ?? "N/A",
                ExamDate = es.ExamSlot?.ExamDate ?? today,
                StartTime = es.ExamSlot?.StartTime ?? new TimeOnly(0, 0),
                Venue = es.ExamSlot?.Venue?.Name ?? "TBA"
            }).ToList();

            // 5. Get Top 3 Latest Announcements
            RecentAnnouncements = await _context.Announcements
                .OrderByDescending(a => a.PublishDate)
                .Take(3)
                .ToListAsync();

            return Page();
        }
    }
}