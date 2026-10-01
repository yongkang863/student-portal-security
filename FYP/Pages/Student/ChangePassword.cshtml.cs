using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace FYP.Pages.Student
{
    [Authorize(Policy = "StudentOnly", AuthenticationSchemes = "StudentCookie")]
    public class ChangePasswordModel : PageModel
    {
        private readonly DB _context;

        public ChangePasswordModel(DB context)
        {
            _context = context;
        }

        [BindProperty]
        public string CurrentPassword { get; set; }

        [BindProperty]
        public string NewPassword { get; set; }

        [BindProperty]
        public string ConfirmNewPassword { get; set; }

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // 1. Get Logged-in User
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidString) || !int.TryParse(uidString, out int uid))
            {
                return RedirectToPage("/Student/Login");
            }

            var user = await _context.Users.FindAsync(uid);
            if (user == null) return NotFound();

            // 2. Verify Current Password
            bool isCurrentPasswordCorrect = BCrypt.Net.BCrypt.Verify(CurrentPassword, user.PasswordHash);
            if (!isCurrentPasswordCorrect)
            {
                ErrorMessage = "The current password you entered is incorrect.";
                return Page();
            }

            // 3. Check if New Password matches Confirmation
            if (NewPassword != ConfirmNewPassword)
            {
                ErrorMessage = "New password and confirmation do not match.";
                return Page();
            }

            // 4. Strict Password Policy Validation (Same as ResetPassword)
            if (NewPassword.Length < 8)
            {
                ErrorMessage = "New password must be at least 8 characters long.";
                return Page();
            }

            // Regex: Lower, Upper, Number, Symbol
            var strictPolicy = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$");
            if (!strictPolicy.IsMatch(NewPassword))
            {
                ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one symbol (e.g. !@#$).";
                return Page();
            }

            // 5. Prevent reusing the exact same password (Optional UX improvement)
            if (BCrypt.Net.BCrypt.Verify(NewPassword, user.PasswordHash))
            {
                ErrorMessage = "New password cannot be the same as your current password.";
                return Page();
            }

            // 6. Update and Save
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);

            // Optional: Reset security flags if you have them
            // user.FailedLoginAttempts = 0; 

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            SuccessMessage = "Your password has been changed successfully.";

            // Clear fields
            CurrentPassword = "";
            NewPassword = "";
            ConfirmNewPassword = "";

            return Page();
        }
    }
}