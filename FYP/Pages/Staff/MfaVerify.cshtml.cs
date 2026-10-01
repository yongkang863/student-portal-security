using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace FYP.Pages.Staff;

public class MfaVerifyModel : PageModel
{
    private readonly DB _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MfaVerifyModel> _logger;

    public MfaVerifyModel(DB context, IConfiguration configuration, ILogger<MfaVerifyModel> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty]
    public string Code { get; set; }

    public string ErrorMessage { get; set; }
    public string MaskedEmail { get; set; }
    public bool IsFatalError { get; set; } = false;

    private string GetMaskedEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains("@")) return "your email";
        var parts = email.Split('@');
        var local = parts[0];
        var domain = parts[1];

        string maskedLocal = local.Length > 0 ? local.Substring(0, 1) + "****" : "****";
        return $"{maskedLocal}@{domain}";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var pendingUid = HttpContext.Session.GetInt32("PendingMfaUid");
        if (pendingUid == null) return RedirectToPage("Login");

        var user = await _context.Users.FindAsync(pendingUid.Value);
        if (user == null) return RedirectToPage("Login");

        MaskedEmail = GetMaskedEmail(user.Email);
        return Page();
    }

    public async Task<IActionResult> OnPostVerifyAsync()
    {
        var pendingUid = HttpContext.Session.GetInt32("PendingMfaUid");
        if (pendingUid == null) return RedirectToPage("Login");

        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Uid == pendingUid.Value);
        if (user == null) return RedirectToPage("Login");

        MaskedEmail = GetMaskedEmail(user.Email);

        bool isCodeValid = false;
        try { isCodeValid = BCrypt.Net.BCrypt.Verify(Code, user.MfaSecret); }
        catch { isCodeValid = false; }

        if (!isCodeValid)
        {
            int attempts = HttpContext.Session.GetInt32("MfaFailedAttempts") ?? 0;
            attempts++;
            HttpContext.Session.SetInt32("MfaFailedAttempts", attempts);

            if (attempts >= 3)
            {
                user.MfaSecret = null;
                await _context.SaveChangesAsync();

                HttpContext.Session.Remove("PendingMfaUid");
                HttpContext.Session.Remove("MfaFailedAttempts");

                IsFatalError = true;
                ErrorMessage = "Maximum attempts reached. For your security, the session has been terminated.";
            }
            else
            {
                IsFatalError = false;
                ErrorMessage = $"Invalid OTP entered. Attempt {attempts} of 3.";
            }

            return Page();
        }

        HttpContext.Session.Remove("MfaFailedAttempts");
        user.MfaSecret = null;
        user.FailedLoginAttempts = 0;
        await _context.SaveChangesAsync();

        bool hasLoggedInBefore = await _context.Set<UsersLoginAudit>()
            .AnyAsync(a => a.Uid == user.Uid && a.Status == "Success");

        if (!hasLoggedInBefore)
        {
            HttpContext.Session.Remove("PendingMfaUid");
            HttpContext.Session.SetInt32("ForceChangeUid", user.Uid);
            return RedirectToPage("/Staff/ForceChangePassword");
        }

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

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.FullName ?? ""),
            new Claim("Uid", user.Uid.ToString()),
            new Claim("RoleId", user.UsersRoleId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "StaffAuth");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("StaffAuth", principal);
        HttpContext.Session.Remove("PendingMfaUid");

        if (user.UsersRoleId == 1) return RedirectToPage("/Admin/Dashboard");
        return RedirectToPage("/Staff/Dashboard");
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        var pendingUid = HttpContext.Session.GetInt32("PendingMfaUid");
        if (pendingUid == null) return RedirectToPage("Login");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Uid == pendingUid.Value);
        if (user == null) return RedirectToPage("Login");

        MaskedEmail = GetMaskedEmail(user.Email);
        HttpContext.Session.Remove("MfaFailedAttempts");

        // SECURITY FIX: Cryptographically secure OTP generation
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var rawCodeChars = new char[6];
        using (var rng = RandomNumberGenerator.Create())
        {
            var randomBytes = new byte[6];
            rng.GetBytes(randomBytes);
            for (int i = 0; i < 6; i++)
            {
                rawCodeChars[i] = chars[randomBytes[i] % chars.Length];
            }
        }
        string rawCode = new string(rawCodeChars);

        user.MfaSecret = BCrypt.Net.BCrypt.HashPassword(rawCode);
        await _context.SaveChangesAsync();

        string subject = "Staff Portal Login Code (Resent)";
        string body = $"<h3>Security Verification</h3><p>Your new OTP is:</p><h1 style='color:#2563EB; letter-spacing: 5px;'>{rawCode}</h1>";

        try
        {
            SendEmail(user.Email, subject, body);
            TempData["SuccessMessage"] = "A new OTP has been sent to your email address.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend MFA email to {Email}", user.Email);
            ErrorMessage = "Failed to resend email. Please contact IT support.";
        }

        return Page();
    }

    private void SendEmail(string toEmail, string subject, string body)
    {
        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var appPassword = _configuration["EmailSettings:AppPassword"];

        using var client = new SmtpClient("smtp.gmail.com", 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(senderEmail, appPassword)
        };

        using var mailMessage = new MailMessage(senderEmail, toEmail, subject, body) { IsBodyHtml = true };
        client.Send(mailMessage);
    }
}