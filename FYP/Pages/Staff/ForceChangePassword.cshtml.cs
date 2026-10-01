using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace FYP.Pages.Staff;

public class ForceChangePasswordModel : PageModel
{
    private readonly DB _context;

    public ForceChangePasswordModel(DB context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public string ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        // Updated Regex: Forces standard keyboard characters (\x21-\x7E) blocking emojis and spaces completely.
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[\x21-\x7E]{8,}$",
            ErrorMessage = "Password must have at least 8 chars, one uppercase, one lowercase, one number, and one special char. Emojis and spaces are not allowed.")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetInt32("ForceChangeUid") == null)
        {
            return RedirectToPage("Login");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var uid = HttpContext.Session.GetInt32("ForceChangeUid");
        if (uid == null) return RedirectToPage("Login");

        if (!ModelState.IsValid) return Page();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Uid == uid.Value);
        if (user == null) return RedirectToPage("Login");

        // 1. Ensure the new password is NOT the same as the old password
        bool isSameAsOld = false;
        try { isSameAsOld = BCrypt.Net.BCrypt.Verify(Input.NewPassword, user.PasswordHash); }
        catch { isSameAsOld = false; }

        if (isSameAsOld)
        {
            ErrorMessage = "Your new password cannot be the same as your current password.";
            return Page();
        }

        // 2. Hash and save the new password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Input.NewPassword);

        // ==========================================
        // 3. INSERT AUDIT LOG SUCCESS
        // ==========================================
        var auditLog = new UsersLoginAudit
        {
            Uid = user.Uid,
            LoginTime = DateTime.Now,
            Status = "Success",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            Device = Request.Headers["User-Agent"].ToString(),
            Location = "Unknown"
        };
        _context.Add(auditLog);
        await _context.SaveChangesAsync();

        // ==========================================
        // 4. SIGN THEM IN DIRECTLY & CLEAR SESSION
        // ==========================================
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.FullName ?? ""),
            new Claim("Uid", user.Uid.ToString()),
            new Claim("RoleId", user.UsersRoleId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "StaffAuth");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("StaffAuth", principal);
        HttpContext.Session.Remove("ForceChangeUid");

        if (user.UsersRoleId == 1) return RedirectToPage("/Admin/Dashboard");
        return RedirectToPage("/Staff/Dashboard");
    }
}