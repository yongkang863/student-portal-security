using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FYP.Pages.Student
{
    public class ForceChangePasswordModel : PageModel
    {
        private readonly DB _context;

        public ForceChangePasswordModel(DB context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public int Uid { get; set; }

        [BindProperty]
        public string CurrentPassword { get; set; }

        [BindProperty]
        public string NewPassword { get; set; }

        [BindProperty]
        public string ConfirmNewPassword { get; set; }

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _context.Users.FindAsync(Uid);
            if (user == null)
            {
                return RedirectToPage("/Student/Login");
            }

            // 1. Verify Current Password
            bool isCurrentPasswordCorrect = BCrypt.Net.BCrypt.Verify(CurrentPassword, user.PasswordHash);
            if (!isCurrentPasswordCorrect)
            {
                ErrorMessage = "The current password you entered is incorrect.";
                return Page();
            }

            // 2. Check if New Password matches Confirmation
            if (NewPassword != ConfirmNewPassword)
            {
                ErrorMessage = "New password and confirmation do not match.";
                return Page();
            }

            // 3. Strict Password Policy Validation
            if (NewPassword.Length < 8)
            {
                ErrorMessage = "New password must be at least 8 characters long.";
                return Page();
            }

            var strictPolicy = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$");
            if (!strictPolicy.IsMatch(NewPassword))
            {
                ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one symbol (e.g. !@#$).";
                return Page();
            }

            // 4. Prevent reusing the exact same password
            if (BCrypt.Net.BCrypt.Verify(NewPassword, user.PasswordHash))
            {
                ErrorMessage = "New password cannot be the same as your current password.";
                return Page();
            }

            // 5. Update and Save
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // 6. Issue Authentication Cookie (Sign them in!)
            var claims = new System.Collections.Generic.List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Uid.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Name, user.FullName ?? "Student"),
                new Claim("RoleId", "3")
            };

            var identity = new ClaimsIdentity(claims, "StudentCookie");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("StudentCookie", principal, new AuthenticationProperties { IsPersistent = false });

            // 7. Extract Real Device & IP Info (Matching MFA & Login formatting)
            string currentIp = GetCurrentIp();
            string currentLocation = await GetLocationFromIpAsync(currentIp);
            string currentDevice = ParseUserAgent(Request.Headers["User-Agent"].ToString());

            // 8. Log Audit as Success
            await LogAuditAsync(user.Uid, currentIp, currentDevice, currentLocation, "Success");

            // 9. Welcome to the portal
            return RedirectToPage("/Student/Homepage");
        }

        // --- HELPER: CONNECTION INFO ---
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

        // --- HELPER: HIGHLY SPECIFIC DEVICE DETECTION ---
        private string ParseUserAgent(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "Unknown Device";

            // Determine Operating System
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

            // Determine Browser
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