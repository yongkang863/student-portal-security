using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FYP.Pages.Student
{
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
        public string Email { get; set; }

        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(Email))
            {
                ErrorMessage = "Please enter your email address.";
                return Page();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == Email);

            if (user == null)
            {
                ErrorMessage = "User not found. Please check your email address.";
                return Page();
            }

            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

            var resetRecord = new UsersPasswordReset
            {
                Uid = user.Uid,
                Token = token,
                ExpiryTime = DateTime.Now.AddMinutes(20),
                IsUsed = false
            };

            _context.UsersPasswordResets.Add(resetRecord);
            await _context.SaveChangesAsync();

            string resetLink = Url.Page("/Student/ResetPassword",
                pageHandler: null,
                values: new { token = token },
                protocol: Request.Scheme);

            try
            {
                await SendResetEmailAsync(user.Email, resetLink);
                Message = "A password reset link has been sent to your email. It will expire in 20 minutes.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reset email to {Email}", user.Email);
                ErrorMessage = "Failed to send email. Please try again later.";
            }

            return Page();
        }

        private async Task SendResetEmailAsync(string toEmail, string link)
        {
            // SECURITY FIX: Secure Configuration
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var appPassword = _configuration["EmailSettings:AppPassword"];

            var fromAddress = new MailAddress(senderEmail, "FYP University Portal");
            var toAddress = new MailAddress(toEmail);

            using var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, appPassword)
            };

            string body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                <h2 style='color: #1b6ec2;'>Password Reset Request</h2>
                <p>We received a request to reset the password for your Student Portal account.</p>
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{link}' style='background-color: #1b6ec2; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Reset Password</a>
                </div>
            </div>";

            using var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = "Action Required: Reset Your Password",
                Body = body,
                IsBodyHtml = true
            };

            await smtp.SendMailAsync(message);
        }
    }
}