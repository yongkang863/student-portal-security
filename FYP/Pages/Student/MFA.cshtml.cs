using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace FYP.Pages.Student
{
    public class MFAModel : PageModel
    {
        private readonly DB _context;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;

        private static ConcurrentDictionary<int, int> _failedAttempts = new ConcurrentDictionary<int, int>();

        public MFAModel(DB context, IMemoryCache cache, IConfiguration configuration)
        {
            _context = context;
            _cache = cache;
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public int Uid { get; set; }

        [BindProperty]
        public string OtpCode { get; set; }

        public string MaskedEmail { get; set; }

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Uid == Uid);
            if (user == null)
            {
                return RedirectToPage("/Student/Login");
            }

            if (!string.IsNullOrEmpty(user.Email))
            {
                MaskedEmail = GetMaskedEmail(user.Email);

                string currentIp = GetCurrentIp();
                string currentLocation = await GetLocationFromIpAsync(currentIp);
                string currentDevice = ParseUserAgent(Request.Headers["User-Agent"].ToString());

                string newOtp = GenerateComplexOTP();
                user.MfaSecret = BCrypt.Net.BCrypt.HashPassword(newOtp);

                _failedAttempts.TryRemove(Uid, out _);

                await _context.SaveChangesAsync();

                bool isSent = await SendOtpEmailAsync(user.Email, newOtp, currentDevice, currentLocation);

                if (isSent)
                {
                    SuccessMessage = "A verification code has been sent to your email.";
                }
                else
                {
                    ErrorMessage = "Failed to send the verification email. Please check your SMTP configuration.";
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Uid == Uid);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            MaskedEmail = GetMaskedEmail(user.Email);

            string currentIp = GetCurrentIp();
            string currentLocation = await GetLocationFromIpAsync(currentIp);
            string currentDevice = ParseUserAgent(Request.Headers["User-Agent"].ToString());

            int failures = _failedAttempts.GetOrAdd(Uid, 0);

            if (failures >= 3)
            {
                ErrorMessage = "Too many incorrect attempts. This code is now invalid. Please click 'Resend Email' to get a new one.";
                return Page();
            }

            bool isOtpValid = false;
            if (!string.IsNullOrEmpty(user.MfaSecret) && !string.IsNullOrEmpty(OtpCode))
            {
                try { isOtpValid = BCrypt.Net.BCrypt.Verify(OtpCode, user.MfaSecret); }
                catch { isOtpValid = false; }
            }

            if (isOtpValid)
            {
                _failedAttempts.TryRemove(Uid, out _);
                user.MfaSecret = null;
                user.LockedUntil = null;
                await _context.SaveChangesAsync();

                // --- NEW: First Time Login Check ---
                bool isFirstLogin = !await _context.UsersLoginAudits
                    .AnyAsync(a => a.Uid == user.Uid && a.Status == "Success");

                if (isFirstLogin)
                {
                    // Redirect to force change password. Cookie is NOT issued yet.
                    return RedirectToPage("/Student/ForceChangePassword", new { uid = user.Uid });
                }
                // -----------------------------------

                // Existing flow for normal, returning users
                var claims = new System.Collections.Generic.List<System.Security.Claims.Claim>
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Uid.ToString()),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.Email ?? ""),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.FullName ?? "Student"),
                    new System.Security.Claims.Claim("RoleId", "3")
                };

                var identity = new System.Security.Claims.ClaimsIdentity(claims, "StudentCookie");
                var principal = new System.Security.Claims.ClaimsPrincipal(identity);

                var authProperties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                {
                    IsPersistent = false
                };

                await HttpContext.SignInAsync("StudentCookie", principal, authProperties);

                await LogAuditAsync(user.Uid, currentIp, currentDevice, currentLocation, "Success");
                await _context.SaveChangesAsync();

                return RedirectToPage("/Student/Homepage");
            }
            else
            {
                failures++;
                _failedAttempts.AddOrUpdate(Uid, failures, (key, oldValue) => failures);

                if (failures >= 3)
                {
                    user.MfaSecret = null;
                    await LogAuditAsync(user.Uid, currentIp, currentDevice, currentLocation, "Failed: MFA 3-Attempt Invalidation");
                    await _context.SaveChangesAsync();

                    ErrorMessage = "Too many incorrect attempts. This code is now invalid. Please click 'Resend Email' to get a new one.";
                    return Page();
                }
                else
                {
                    await LogAuditAsync(user.Uid, currentIp, currentDevice, currentLocation, "Failed: MFA Invalid Code");
                    await _context.SaveChangesAsync();

                    ErrorMessage = $"Invalid code. You have {3 - failures} attempts remaining.";
                    return Page();
                }
            }
        }

        public async Task<IActionResult> OnPostResendAsync()
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Uid == Uid);
            if (user == null) return RedirectToPage("/Student/Login");

            MaskedEmail = GetMaskedEmail(user.Email);

            string currentIp = GetCurrentIp();
            string currentLocation = await GetLocationFromIpAsync(currentIp);
            string currentDevice = ParseUserAgent(Request.Headers["User-Agent"].ToString());

            _failedAttempts.TryRemove(Uid, out _);

            string newOtp = GenerateComplexOTP();
            user.MfaSecret = BCrypt.Net.BCrypt.HashPassword(newOtp);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(user.Email))
            {
                bool isSent = await SendOtpEmailAsync(user.Email, newOtp, currentDevice, currentLocation);

                if (isSent)
                {
                    SuccessMessage = "A new verification code has been sent to your email.";
                }
                else
                {
                    ErrorMessage = "Failed to send the verification email. Please check your SMTP configuration.";
                }
            }
            else
            {
                ErrorMessage = "Could not find an email address associated with this account.";
            }

            return Page();
        }

        private string GetCurrentIp()
        {
            string? currentIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                var forwardedHeader = Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedHeader))
                {
                    currentIp = forwardedHeader.Split(',')[0].Trim();
                }
            }

            if (currentIp == "::1") currentIp = "127.0.0.1";
            if (string.IsNullOrEmpty(currentIp)) currentIp = "127.0.0.1";

            return currentIp;
        }

        private async Task<string> GetLocationFromIpAsync(string ip)
        {
            if (ip == "127.0.0.1" || ip.StartsWith("192.168") || ip.StartsWith("10."))
                return "Local Development";

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var response = await client.GetFromJsonAsync<JsonElement>($"http://ip-api.com/json/{ip}");

                if (response.TryGetProperty("status", out var status) && status.GetString() == "success")
                {
                    string city = response.TryGetProperty("city", out var c) ? c.GetString() : "Unknown City";
                    string country = response.TryGetProperty("country", out var cn) ? cn.GetString() : "Unknown Country";
                    return $"{city}, {country}";
                }
            }
            catch { }
            return "Unknown Location";
        }

        private string ParseUserAgent(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "Unknown Device";

            string os = "Unknown OS";
            if (userAgent.Contains("Windows NT 10.0")) os = "Windows 10/11";
            else if (userAgent.Contains("Windows NT 6.3")) os = "Windows 8.1";
            else if (userAgent.Contains("Windows NT 6.2")) os = "Windows 8";
            else if (userAgent.Contains("Windows NT 6.1")) os = "Windows 7";
            else if (userAgent.Contains("Mac OS X")) os = "macOS";
            else if (userAgent.Contains("Android")) os = "Android";
            else if (userAgent.Contains("iPhone")) os = "iPhone";
            else if (userAgent.Contains("iPad")) os = "iPad";
            else if (userAgent.Contains("Linux")) os = "Linux";

            string browser = "Unknown Browser";
            if (userAgent.Contains("Edg")) browser = "Edge";
            else if (userAgent.Contains("OPR") || userAgent.Contains("Opera")) browser = "Opera";
            else if (userAgent.Contains("Chrome") && !userAgent.Contains("Edg") && !userAgent.Contains("OPR")) browser = "Chrome";
            else if (userAgent.Contains("Firefox")) browser = "Firefox";
            else if (userAgent.Contains("Safari") && !userAgent.Contains("Chrome")) browser = "Safari";
            else if (userAgent.Contains("Trident") || userAgent.Contains("MSIE")) browser = "Internet Explorer";

            return $"{browser} on {os}";
        }

        private string GetMaskedEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return "Unknown Email";

            var parts = email.Split('@');
            if (parts.Length != 2) return email;

            var localPart = parts[0];
            var domain = parts[1];

            if (localPart.Length <= 2)
            {
                return $"{localPart}***@{domain}";
            }

            string visible = localPart.Substring(0, 2);
            return $"{visible}****@{domain}";
        }

        // SECURITY FIX: Replaced System.Random with secure RandomNumberGenerator
        private string GenerateComplexOTP()
        {
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
            return new string(rawCodeChars);
        }

        private async Task<bool> SendOtpEmailAsync(string toEmail, string otp, string device, string location)
        {
            try
            {
                // SECURITY FIX: Use configuration for sensitive email credentials
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
                    <h2 style='color: #1b6ec2; text-align: center;'>Verification Code</h2>
                    <p>You requested a new verification code.</p>
                    <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0; font-size: 14px; color: #555;'>
                        <p style='margin: 5px 0;'><strong>Device:</strong> {device}</p>
                        <p style='margin: 5px 0;'><strong>Location:</strong> {location}</p>
                    </div>
                    <div style='text-align: center; margin: 30px 0;'>
                        <span style='background-color: #e8f0fe; color: #1b6ec2; padding: 15px 30px; font-size: 24px; font-weight: bold; letter-spacing: 5px; border-radius: 5px; border: 1px solid #b3d7ff;'>
                            {otp}
                        </span>
                    </div>
                    <p style='color: #d9534f; font-size: 13px;'><strong>Security Notice:</strong> If you did not request this, please contact support immediately.</p>
                </div>";

                using var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = "Your Verification Code",
                    Body = body,
                    IsBodyHtml = true
                };

                await smtp.SendMailAsync(message);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task LogAuditAsync(int uid, string ip, string device, string location, string status)
        {
            if (!string.IsNullOrEmpty(device) && device.Length > 50) device = device.Substring(0, 50);

            var auditLog = new UsersLoginAudit
            {
                Uid = uid,
                LoginTime = DateTime.Now,
                Status = status,
                IpAddress = ip,
                Device = device,
                Location = location
            };

            _context.UsersLoginAudits.Add(auditLog);
        }
    }
}