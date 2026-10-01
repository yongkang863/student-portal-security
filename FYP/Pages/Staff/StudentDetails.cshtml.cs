using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FYP.Pages.Staff
{
    [Authorize(Policy = "StaffOnly", AuthenticationSchemes = "StaffAuth")]
    public class StudentDetailsModel : PageModel
    {
        private readonly DB _context;

        public StudentDetailsModel(DB context)
        {
            _context = context;
        }

        public User StudentInfo { get; set; }
        public string StudentPhotoBase64 { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            // 1. Verify Staff is logged in (using the robust method we just fixed!)
            var uidString = User.FindFirst("Uid")?.Value ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentStaffId = int.TryParse(uidString, out int staffId) ? staffId : HttpContext.Session.GetInt32("Uid") ?? 0;

            if (currentStaffId == 0) return RedirectToPage("/Staff/Login");

            // 2. Fetch the specific student by ID, including their Programme
            StudentInfo = await _context.Users
                .Include(u => u.Programme)
                    .ThenInclude(p => p.Department) // Assuming Programme has a Department
                .FirstOrDefaultAsync(u => u.Uid == id);

            if (StudentInfo == null)
            {
                return NotFound("Student not found.");
            }

            // 3. Process the Student's Photo if it exists
            if (StudentInfo.Photo != null && StudentInfo.Photo.Length > 0)
            {
                string base64 = Convert.ToBase64String(StudentInfo.Photo);
                StudentPhotoBase64 = $"data:image/jpg;base64,{base64}";
            }

            return Page();
        }
    }
}