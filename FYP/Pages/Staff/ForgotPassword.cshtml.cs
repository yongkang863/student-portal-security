using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using BCrypt.Net;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FYP.Pages.Staff;

public class ForgotPasswordModel : PageModel
{
    private readonly DB _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(DB context, IConfiguration configuration, ILogger<ForgotPasswordModel> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty]
    [Required, EmailAddress]
    public string Email { get; set; }

    public bool EmailSent { get; set; } = false;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == Email);

        if (user != null && (user.UsersRoleId == 1 || user.UsersRoleId == 2))
        {
            string rawToken = Guid.NewGuid().ToString();

            user.MfaSecret = BCrypt.Net.BCrypt.HashPassword(rawToken);
            await _context.SaveChangesAsync();

            var emailBytes = Encoding.UTF8.GetBytes(user.Email);
            var safeEmailString = WebEncoders.Base64UrlEncode(emailBytes);
            var combinedSecureToken = $"{safeEmailString}.{rawToken}";

            var resetLink = Url.Page(
                "/Staff/PasswordReset",
                pageHandler: null,
                values: new { token = combinedSecureToken },
                protocol: Request.Scheme);

            string subject = "Reset Your Password";
            string body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h2 style='color: #333;'>Password Reset Request</h2>
                    <p>You requested to reset your password for the Staff Portal.</p>
                    <p>Please click the button below to proceed:</p>
                    <p>
                        <a href='{resetLink}' style='background-color: #2563EB; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                            Reset Password
                        </a>
                    </p>
                </div>";

            try
            {
                SendEmail(user.Email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
                ModelState.AddModelError("", "System error sending email. Please try again later.");
            }
        }

        EmailSent = true;
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