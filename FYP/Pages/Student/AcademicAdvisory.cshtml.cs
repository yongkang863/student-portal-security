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
    public class AcademicAdvisoryModel : PageModel
    {
        private readonly DB _context;

        public AcademicAdvisoryModel(DB context)
        {
            _context = context;
        }

        public User CurrentUser { get; set; }
        public User Advisor { get; set; }
        public Advisory AdvisoryInfo { get; set; }
        public string AdvisorPhotoBase64 { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // 1. Get Current Student ID
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidString) || !int.TryParse(uidString, out int uid))
            {
                return RedirectToPage("/Student/Login");
            }

            // 2. Fetch Student + Advisory + Staff + Department
            CurrentUser = await _context.Users
                .Include(u => u.Advisory)
                    .ThenInclude(a => a.Staff)          // Load Advisor User
                        .ThenInclude(s => s.Department) // Load Advisor's Department
                .FirstOrDefaultAsync(u => u.Uid == uid);

            if (CurrentUser == null) return NotFound();

            // 3. Extract Advisor Info
            if (CurrentUser.Advisory != null)
            {
                AdvisoryInfo = CurrentUser.Advisory;
                Advisor = CurrentUser.Advisory.Staff;

                // 4. Process Advisor Photo
                if (Advisor != null && Advisor.Photo != null && Advisor.Photo.Length > 0)
                {
                    string base64 = Convert.ToBase64String(Advisor.Photo);
                    AdvisorPhotoBase64 = $"data:image/jpg;base64,{base64}";
                }
            }

            return Page();
        }
    }
}