using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace FYP.Pages.Staff;

[Authorize]
public class SettingsModel : PageModel
{
    private readonly DB _context;

    public SettingsModel(DB context)
    {
        _context = context;
    }

    [BindProperty]
    public ChangePasswordInput Input { get; set; }

    public string ErrorMessage { get; set; }
    public string SuccessMessage { get; set; }

    public class ChangePasswordInput
    {
        [Required(ErrorMessage = "Current password is required.")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Please confirm your new password.")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // --- Server-Side Password Rules Validation ---
        if (Input.NewPassword.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters long.";
            return Page();
        }
        if (!Regex.IsMatch(Input.NewPassword, @"[a-zA-Z]"))
        {
            ErrorMessage = "Password must contain at least one letter.";
            return Page();
        }
        if (!Regex.IsMatch(Input.NewPassword, @"[0-9]"))
        {
            ErrorMessage = "Password must contain at least one number.";
            return Page();
        }
        if (!Regex.IsMatch(Input.NewPassword, @"[^a-zA-Z0-9\s]"))
        {
            ErrorMessage = "Password must contain at least one symbol.";
            return Page();
        }
        // Blocks emojis
        if (Input.NewPassword.Any(char.IsSurrogate))
        {
            ErrorMessage = "Emojis are not allowed in the password.";
            return Page();
        }

        // --- Fetch Current Staff User ---
        var uidClaim = User.FindFirst("Uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int currentUid = 0;

        if (!int.TryParse(uidClaim, out currentUid))
        {
            currentUid = HttpContext.Session.GetInt32("Uid") ?? 0;
        }

        if (currentUid == 0) return RedirectToPage("/Staff/Login");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Uid == currentUid);
        if (user == null) return NotFound();

        // --- Verify Current Password ---
        if (!BCrypt.Net.BCrypt.Verify(Input.CurrentPassword, user.PasswordHash))
        {
            ErrorMessage = "The current password you entered is incorrect.";
            return Page();
        }

        // --- Update and Save ---
        // FIXED: Now properly encrypting the new password with BCrypt before saving!
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Input.NewPassword);

        await _context.SaveChangesAsync();

        SuccessMessage = "Your password has been changed successfully!";
        ModelState.Clear(); // Clears the form inputs

        return Page();
    }
}