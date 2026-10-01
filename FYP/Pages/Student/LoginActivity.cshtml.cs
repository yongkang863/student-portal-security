using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FYP.Pages.Student
{
    [Authorize(Policy = "StudentOnly", AuthenticationSchemes = "StudentCookie")]
    public class LoginActivityModel : PageModel
    {
        private readonly DB _context;

        public LoginActivityModel(DB context)
        {
            _context = context;
        }

        public IList<UsersLoginAudit> LoginLogs { get; set; } = new List<UsersLoginAudit>();

        public async Task<IActionResult> OnGetAsync()
        {
            // 1. Get Current User ID
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uidString, out int uid))
            {
                return RedirectToPage("/Student/Login");
            }

            // 2. Fetch Audit Logs (Latest first)
            LoginLogs = await _context.UsersLoginAudits
                .Where(a => a.Uid == uid)
                .OrderByDescending(a => a.LoginTime)
                .Take(50) // Limit to last 50 entries for performance
                .ToListAsync();

            return Page();
        }
    }
}