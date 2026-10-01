using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FYP.Pages.Student
{
    public class LoginModel : PageModel
    {
        private readonly DB _context;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;

        public LoginModel(DB context, IMemoryCache cache, IConfiguration configuration)
        {
            _context = context;
            _cache = cache;
            _configuration = configuration;
        }

        [BindProperty]
        public string? StudentId { get; set; }

        [BindProperty]
        public string? Password { get; set; }

        // Tracks if the user has completed the two-step verification
        [BindProperty]
        public bool IsCaptchaVerified { get; set; } = false;

        public string? ErrorMessage { get; set; }

        public void OnGet(string? error, string? ReturnUrl = null)
        {
            if (!string.IsNullOrEmpty(ReturnUrl) && string.IsNullOrEmpty(error))
                ErrorMessage = "Please log in first to access this page.";
            else if (error == "mfa_fail")
                ErrorMessage = "Verification failed. Code invalidated. Please log in again.";
            else if (error == "mfa_expired")
                ErrorMessage = "Your verification session expired. Please log in again.";
            else if (error == "timeout")
                ErrorMessage = "Due to inactivity, please log in again.";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // --- STEP 1: IF CAPTCHA IS NOT VERIFIED YET ---
            if (!IsCaptchaVerified)
            {
                string recaptchaResponse = Request.Form["g-recaptcha-response"];
                bool isCaptchaValid = await VerifyReCaptchaV2Async(recaptchaResponse);

                if (!isCaptchaValid)
                {
                    ErrorMessage = "Please complete the CAPTCHA verification to proceed.";
                    return Page();
                }

                // Captcha passed! Change state to show login fields and clear model errors
                IsCaptchaVerified = true;
                ModelState.Clear();
                ErrorMessage = null;
                return Page();
            }

            // --- STEP 2: CAPTCHA IS VERIFIED, PROCEED WITH NORMAL LOGIN ---
            if (string.IsNullOrEmpty(StudentId) || string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "Please enter both Student ID and Password.";
                return Page();
            }

            // 1. IP & Device Detection
            string? currentIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
                currentIp = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();

            if (string.IsNullOrEmpty(currentIp) || currentIp == "::1") currentIp = "127.0.0.1";

            string currentLocation = await GetLocationFromIpAsync(currentIp);
            string currentDevice = ParseUserAgent(Request.Headers["User-Agent"].ToString());

            // 2. Fetch User & INCLUDE PROGRAMME
            var user = await _context.Users
                .Include(u => u.Programme)
                .FirstOrDefaultAsync(u => u.UserId == StudentId);

            if (user == null)
            {
                ErrorMessage = "Invalid Student ID or Password.";
                return Page();
            }

            // 3. Lockout Check
            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.Now)
            {
                await LogAuditAsync(user.Uid, currentIp, currentDevice, currentLocation, "Failed: Account Locked");
                ErrorMessage = "Account locked. Try again later.";
                return Page();
            }

            // 4. Verify Password
            bool isPasswordCorrect = false;
            try { isPasswordCorrect = BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash); } catch { }

            if (!isPasswordCorrect || user.UsersRoleId != 3)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 5) user.LockedUntil = DateTime.Now.AddMinutes(5);
                await _context.SaveChangesAsync();

                await LogAuditAsync(user.Uid, currentIp, currentDevice, currentLocation, "Failed: Invalid Credentials");
                ErrorMessage = "Invalid Student ID or Password.";
                return Page();
            }

            // 5. Success Logic & New Device MFA Trigger
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            await _context.SaveChangesAsync();

            bool isKnownDevice = await _context.UsersLoginAudits
                .AnyAsync(a => a.Uid == user.Uid && a.Status == "Success" && a.Device == currentDevice);

            if (!isKnownDevice)
            {
                return RedirectToPage("/Student/MFA", new { uid = user.Uid });
            }
            else
            {
                await LogAuditAsync(user.Uid, currentIp, currentDevice, currentLocation, "Success");

                var claims = new System.Collections.Generic.List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Uid.ToString()),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim(ClaimTypes.Name, user.FullName ?? "Student"),
                    new Claim("RoleId", "3"),
                    new Claim("ProgrammeLevel", user.Programme?.Level ?? "Student")
                };

                var identity = new ClaimsIdentity(claims, "StudentCookie");
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync("StudentCookie", principal, new AuthenticationProperties { IsPersistent = false });

                return RedirectToPage("/Student/Homepage");
            }
        }

        // --- HELPER: VERIFY RECAPTCHA v2 ---
        private async Task<bool> VerifyReCaptchaV2Async(string recaptchaResponse)
        {
            if (string.IsNullOrEmpty(recaptchaResponse)) return false;

            // Securely read the secret key from appsettings.json dynamically
            var secretKey = _configuration["RecaptchaSettings:SecretKey"];

            try
            {
                using var client = new HttpClient();
                var response = await client.PostAsync($"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={recaptchaResponse}", null);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    // In v2, we only care if success is true
                    return root.GetProperty("success").GetBoolean();
                }
            }
            catch { }
            return false;
        }

        // --- HELPER: CONNECTION INFO ---
        private async Task<string> GetLocationFromIpAsync(string ip)
        {
            if (string.IsNullOrEmpty(ip) || ip == "127.0.0.1" || ip == "::1" || ip.StartsWith("192.168") || ip.StartsWith("10."))
                return "Local Development";

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var response = await client.GetAsync($"http://ip-api.com/json/{ip}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
                    {
                        string city = root.TryGetProperty("city", out var c) && !string.IsNullOrEmpty(c.GetString()) ? c.GetString() : "Unknown City";
                        string country = root.TryGetProperty("country", out var cn) && !string.IsNullOrEmpty(cn.GetString()) ? cn.GetString() : "Unknown Country";
                        return $"{city}, {country}";
                    }
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

        // --- HELPER: LOG AUDIT ---
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
            await _context.SaveChangesAsync();
        }
    }
}