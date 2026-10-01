using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FYP.Pages.Admin;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class DashboardModel : PageModel
{
    private readonly DB _context;

    public DashboardModel(DB context)
    {
        _context = context;
    }

    // --- Counters ---
    public int PendingLeaveCount { get; set; }
    public int NewBookingCount { get; set; }
    public int OverduePaymentCount { get; set; }
    public string CurrentAdminName { get; set; } = "Admin";

    // --- Lists ---
    public List<Announcement> RecentAnnouncements { get; set; } = new();

    // List to hold Audit Data + User Name
    public List<AuditViewModel> RecentLogins { get; set; } = new();

    public class AuditViewModel
    {
        public DateTime Time { get; set; }
        public string UserName { get; set; }
        public string Role { get; set; }
        public string IpAddress { get; set; }
        public string Status { get; set; }
    }

    public async Task OnGetAsync()
    {
        // 1. Get Current Admin Name
        if (User.Identity.IsAuthenticated)
        {
            CurrentAdminName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Admin";
        }

        // 2. Fetch Counters (UPDATED: Now adds both Student and Staff leaves)
        int studentLeaves = await _context.StudentLeaves.CountAsync(l => l.StatusId == 3);
        int staffLeaves = await _context.StaffLeaves.CountAsync(l => l.StatusId == 3);
        PendingLeaveCount = studentLeaves + staffLeaves;

        NewBookingCount = await _context.Bookings.CountAsync(b => b.StatusId == 3);
        var today = DateOnly.FromDateTime(DateTime.Now);
        OverduePaymentCount = await _context.Bills.CountAsync(b => b.DueDate < today);

        // 3. Fetch Announcements
        RecentAnnouncements = await _context.Announcements
            .OrderByDescending(a => a.Id)
            .Take(3)
            .ToListAsync();

        // 4. Fetch Recent Login Audits (Joined with Users table)
        RecentLogins = await _context.UsersLoginAudits
            .OrderByDescending(l => l.LoginTime)
            .Take(5) // Show last 5 logins
            .Join(_context.Users,
                audit => audit.Uid,
                user => user.Uid,
                (audit, user) => new AuditViewModel
                {
                    Time = audit.LoginTime,
                    UserName = user.FullName,
                    Role = user.UsersRoleId == 1 ? "Admin" : (user.UsersRoleId == 2 ? "Staff" : "Student"),
                    IpAddress = audit.IpAddress ?? "Unknown",
                    Status = audit.Status
                })
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