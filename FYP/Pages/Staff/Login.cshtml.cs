using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using FYP.Models; // Added to access UsersLoginAudit
using FYP.Helpers; // Added to access your new AuditHelper
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace FYP.Pages.Staff;

public class LoginModel : PageModel
{
    private readonly DB _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(DB context, IConfiguration configuration, ILogger<LoginModel> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public string ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [Display(Name = "Staff ID")]
        public string StaffId { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == Input.StaffId);

        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid Staff ID or Password.");
            return Page();
        }

        if (user.UsersRoleId != 1 && user.UsersRoleId != 2)
        {
            await LogLoginAttemptAsync(user.Uid, "Failed");
            ModelState.AddModelError(string.Empty, "Access Denied: Staff & Admin Only.");
            return Page();
        }

        if (user.UsersRoleId == 2 && user.LockedUntil.HasValue)
        {
            if (user.LockedUntil > DateTime.Now)
            {
                ErrorMessage = $"Account locked. Try again after {user.LockedUntil.Value:HH:mm}.";
                await LogLoginAttemptAsync(user.Uid, "Lockout");
                return Page();
            }
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
            // Note: Changes will be saved either in the success or fail block below
        }

        bool isPasswordValid = false;
        try { isPasswordValid = BCrypt.Net.BCrypt.Verify(Input.Password, user.PasswordHash); }
        catch { isPasswordValid = false; }

        if (!isPasswordValid)
        {
            user.FailedLoginAttempts++;
            if (user.UsersRoleId == 2 && user.FailedLoginAttempts >= 5)
            {
                user.LockedUntil = DateTime.Now.AddMinutes(15);
                ErrorMessage = "Too many failed attempts. Account locked for 15 minutes.";
                await LogLoginAttemptAsync(user.Uid, "Lockout");
            }
            else
            {
                ErrorMessage = "Invalid Staff ID or Password.";
                await LogLoginAttemptAsync(user.Uid, "Failed");
            }
            return Page();
        }

        // --- SUCCESSFUL LOGIN PATH ---
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

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
        
        // Save the audit log and the user updates all at once
        await LogLoginAttemptAsync(user.Uid, "Success");

        string subject = "Staff Portal Login Code";
        string body = $@"<h3>Security Verification</h3><p>Your One-Time Password (OTP) is:</p><h1 style='color:#2563EB; letter-spacing: 5px;'>{rawCode}</h1>";

        try
        {
            SendEmail(user.Email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send MFA email to Staff {StaffId}", user.UserId);
            ModelState.AddModelError("", "Error sending email. Please contact IT.");
            return Page();
        }

        HttpContext.Session.SetInt32("PendingMfaUid", user.Uid);
        return RedirectToPage("/Staff/MfaVerify");
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

    // --- NEW HELPER METHOD FOR AUDITING ---
    private async Task LogLoginAttemptAsync(int uid, string status)
    {
        // 1. Capture the IP
        string rawIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        if (rawIp == "::1") rawIp = "127.0.0.1";

        // 2. Capture the Device Info
        string rawUserAgent = Request.Headers["User-Agent"].ToString();

        // 3. Translate raw data using the helper we discussed earlier
        string friendlyDevice = AuditHelper.GetFriendlyDeviceName(rawUserAgent);
        string actualLocation = await AuditHelper.GetLocationFromIpAsync(rawIp);

        // 4. Create the Audit Entity
        var auditLog = new UsersLoginAudit
        {
            Uid = uid,
            LoginTime = DateTime.Now,
            Status = status,
            IpAddress = rawIp,
            Device = friendlyDevice,
            Location = actualLocation
        };

        _context.UsersLoginAudits.Add(auditLog);
        
        // This single SaveChangesAsync call will save both the audit log AND 
        // any updates made to the User object (like MFA codes or failed attempt counts)
        await _context.SaveChangesAsync();
    }
}