using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using System.ComponentModel.DataAnnotations;
using BCrypt.Net;
using Microsoft.AspNetCore.WebUtilities; // For decoding
using System.Text;                     // For encoding

namespace FYP.Pages.Staff;

public class PasswordResetModel : PageModel
{
    private readonly DB _context;

    public PasswordResetModel(DB context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public string ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        public string Token { get; set; } // The combined safe token

        // UID removed completely!

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "Password must have at least 8 chars, one uppercase, one lowercase, one number and one special char.")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    // 1. Capture the single combined token from the URL
    public void OnGet(string token)
    {
        Input = new InputModel
        {
            Token = token
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (string.IsNullOrEmpty(Input.Token) || !Input.Token.Contains('.'))
        {
            ErrorMessage = "Invalid or malformed token.";
            return Page();
        }

        // 2. Split the token into its two parts
        var tokenParts = Input.Token.Split('.');
        var safeEmailString = tokenParts[0];
        var rawToken = tokenParts[1];

        // 3. Decode the Email safely
        string decodedEmail;
        try
        {
            var emailBytes = WebEncoders.Base64UrlDecode(safeEmailString);
            decodedEmail = Encoding.UTF8.GetString(emailBytes);
        }
        catch
        {
            ErrorMessage = "Invalid token format.";
            return Page();
        }

        // 4. Find User by the decoded Email
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == decodedEmail);

        if (user == null || string.IsNullOrEmpty(user.MfaSecret))
        {
            ErrorMessage = "Invalid or expired token.";
            return Page();
        }

        // 5. Verify the random part of the token against the DB hash
        bool isTokenValid = false;
        try
        {
            isTokenValid = BCrypt.Net.BCrypt.Verify(rawToken, user.MfaSecret);
        }
        catch { isTokenValid = false; }

        if (!isTokenValid)
        {
            ErrorMessage = "Invalid or expired token.";
            return Page();
        }

        // 6. Success! Update Password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Input.NewPassword);
        user.MfaSecret = null;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        await _context.SaveChangesAsync();

        // Pass success message back to the Login page
        TempData["SuccessMessage"] = "Your password has been successfully reset. Please log in with your new credentials.";
        return RedirectToPage("/Staff/Login");
    }
}