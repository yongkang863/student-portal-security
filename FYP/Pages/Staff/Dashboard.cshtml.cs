using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FYP.Pages.Staff;

public class DashboardModel : PageModel
{
    private readonly DB _context;

    public DashboardModel(DB context)
    {
        _context = context;
    }

    public string CurrentStaffName { get; set; } = "Staff";
    public int MyPendingLeaveCount { get; set; }
    public int MyUnpaidBills { get; set; }

    // --- FIX 1: Renamed to 'Announcements' to match Frontend ---
    public List<Announcement> Announcements { get; set; } = new();

    // --- FIX 2: Renamed to 'MyRecentLogins' to match Frontend ---
    public List<AuditViewModel> MyRecentLogins { get; set; } = new();

    public class AuditViewModel
    {
        public DateTime Time { get; set; }
        public string IpAddress { get; set; }
        public string Status { get; set; }

        public List<Course> TeachingCourses { get; set; } = new();
    }

    public async Task OnGetAsync()
    {
        int currentUid = 0;

        // Safely attempt to get the ID from Claims or Session
        if (User.Identity.IsAuthenticated)
        {
            CurrentStaffName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Staff";

            var uidClaim = User.FindFirst("Uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(uidClaim) && int.TryParse(uidClaim, out int parsedId))
            {
                currentUid = parsedId;
            }
            else
            {
                // Fallback: Check if you stored it in Session instead
                currentUid = HttpContext.Session.GetInt32("Uid") ?? 0;
            }
        }

        if (currentUid > 0)
        {
            // 1. Fetch Personal Stats
            MyPendingLeaveCount = await _context.StudentLeaves
                .CountAsync(l => l.StudentId == currentUid && l.StatusId == 3);

            // Fetch Recent Logins
            MyRecentLogins = await _context.UsersLoginAudits
                .Where(audit => audit.Uid == currentUid)
                .OrderByDescending(l => l.LoginTime)
                .Take(5)
                .Select(audit => new AuditViewModel
                {
                    Time = audit.LoginTime,
                    IpAddress = audit.IpAddress ?? "-",
                    Status = audit.Status
                })
                .ToListAsync();
        }

        // 2. Fetch Announcements
        Announcements = await _context.Announcements
            .Where(a => a.TargetRole == "All" || a.TargetRole == "Staff")
            .OrderByDescending(a => a.PublishDate)
            .Take(5)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        // 1. Sign out of your specific StaffAuth cookie system
        await HttpContext.SignOutAsync("StaffAuth");

        // 2. Clear the Session data (since you are now using Sessions)
        HttpContext.Session.Clear();

        // 3. FIX: Redirect to the correct folder!
        return RedirectToPage("/Staff/Login");
    }
}