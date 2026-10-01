using FYP.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.RegularExpressions; // Required for Regex
using System.Threading.Tasks;

namespace FYP.Pages.Student
{
    public class ResetPasswordModel : PageModel
    {
        private readonly DB _context;

        public ResetPasswordModel(DB context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; }

        [BindProperty]
        public string NewPassword { get; set; }

        [BindProperty]
        public string ConfirmPassword { get; set; }

        public string? ErrorMessage { get; set; }
        public bool IsTokenValid { get; set; } = true;

        public async Task OnGetAsync()
        {
            if (string.IsNullOrEmpty(Token))
            {
                ErrorMessage = "Invalid token.";
                IsTokenValid = false;
                return;
            }

            var resetRecord = await _context.UsersPasswordResets
                .FirstOrDefaultAsync(r => r.Token == Token);

            if (resetRecord == null || resetRecord.IsUsed || resetRecord.ExpiryTime < DateTime.Now)
            {
                ErrorMessage = "This password reset link is invalid or has expired.";
                IsTokenValid = false;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                IsTokenValid = true;
                return Page();
            }

            // --- STRICT PASSWORD VALIDATION ---
            // 1. Minimum 8 characters
            if (NewPassword.Length < 8)
            {
                ErrorMessage = "Password must be at least 8 characters long.";
                IsTokenValid = true;
                return Page();
            }

            // 2. Regex Check:
            // (?=.*[a-z])       : At least one lowercase
            // (?=.*[A-Z])       : At least one uppercase
            // (?=.*\d)          : At least one number
            // (?=.*[\W_])       : At least one symbol (non-word char or underscore)
            var strictPolicy = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$");

            if (!strictPolicy.IsMatch(NewPassword))
            {
                ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one symbol (e.g. !@#$).";
                IsTokenValid = true;
                return Page();
            }
            // ----------------------------------

            var resetRecord = await _context.UsersPasswordResets
                .FirstOrDefaultAsync(r => r.Token == Token);

            if (resetRecord == null || resetRecord.IsUsed || resetRecord.ExpiryTime < DateTime.Now)
            {
                ErrorMessage = "This link is no longer valid or has been used.";
                IsTokenValid = false;
                return Page();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Uid == resetRecord.Uid);
            if (user == null)
            {
                ErrorMessage = "User account not found.";
                return Page();
            }

            // Hash the new password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);

            // Unlock account and reset failures
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;

            // Mark token as used
            resetRecord.IsUsed = true;

            await _context.SaveChangesAsync();

            return RedirectToPage("/Student/Login");
        }
    }
}