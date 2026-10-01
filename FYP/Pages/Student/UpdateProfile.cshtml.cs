using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FYP.Pages.Student
{
    [Authorize(Policy = "StudentOnly", AuthenticationSchemes = "StudentCookie")]
    public class UpdateProfileModel : PageModel
    {
        private readonly DB _context;

        public UpdateProfileModel(DB context)
        {
            _context = context;
        }

        [BindProperty]
        public UpdateProfileInput Input { get; set; } = new();

        public string? Message { get; set; }

        public class UpdateProfileInput
        {
            [Required(ErrorMessage = "Contact number is required.")]
            [RegularExpression(@"^01\d{8}$", ErrorMessage = "Phone number must be exactly 10 digits and start with '01' (e.g., 0123456789).")]
            [Display(Name = "Contact Number")]
            public string ContactNo { get; set; }

            [Required(ErrorMessage = "Emergency contact is required.")]
            [RegularExpression(@"^01\d{8}$", ErrorMessage = "Phone number must be exactly 10 digits and start with '01' (e.g., 0123456789).")]
            [Display(Name = "Emergency Contact")]
            public string EmergencyContact { get; set; }

            [Required(ErrorMessage = "Address is required.")]
            [StringLength(100, ErrorMessage = "Address cannot exceed 100 characters.")]
            public string Address { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uidString, out int uid)) return RedirectToPage("/Student/Login");

            var user = await _context.Users.FindAsync(uid);
            if (user == null) return NotFound();

            // Pre-fill existing data
            Input = new UpdateProfileInput
            {
                ContactNo = user.ContactNo,
                EmergencyContact = user.EmergencyContact,
                Address = user.Address
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uidString, out int uid)) return RedirectToPage("/Student/Login");

            var user = await _context.Users.FindAsync(uid);
            if (user == null) return NotFound();

            // Update allowed fields
            user.ContactNo = Input.ContactNo;
            user.EmergencyContact = Input.EmergencyContact;
            user.Address = Input.Address;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToPage("/Student/Profile");
        }
    }
}