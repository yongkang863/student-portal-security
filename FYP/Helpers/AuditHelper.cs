using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FYP.Helpers // Change this namespace to match your project if needed
{
    public static class AuditHelper
    {
        // 1. Converts raw User-Agent string into "Chrome on Windows 10/11"
        public static string GetFriendlyDeviceName(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "Unknown Device";

            string browser = "Unknown Browser";
            if (userAgent.Contains("Edg")) browser = "Edge";
            else if (userAgent.Contains("Chrome")) browser = "Chrome";
            else if (userAgent.Contains("Firefox")) browser = "Firefox";
            else if (userAgent.Contains("Safari") && !userAgent.Contains("Chrome")) browser = "Safari";

            string os = "Unknown OS";
            if (userAgent.Contains("Windows NT 10.0")) os = "Windows 10/11";
            else if (userAgent.Contains("Windows NT")) os = "Windows";
            else if (userAgent.Contains("Mac OS X")) os = "macOS";
            else if (userAgent.Contains("Android")) os = "Android";
            else if (userAgent.Contains("iPhone") || userAgent.Contains("iPad")) os = "iOS";
            else if (userAgent.Contains("Linux")) os = "Linux";

            return $"{browser} on {os}";
        }

        // 2. Converts IP to Location using a free API
        public static async Task<string> GetLocationFromIpAsync(string ipAddress)
        {
           
            if (ipAddress == "127.0.0.1" || ipAddress == "::1" || ipAddress == "localhost")
            {
                return "Local Development";
            }

            try
            {
                using var client = new HttpClient();
                // Using ip-api.com (Free, no API key required for low volume)
                var response = await client.GetAsync($"http://ip-api.com/json/{ipAddress}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.GetProperty("status").GetString() == "success")
                    {
                        string city = doc.RootElement.GetProperty("city").GetString();
                        string country = doc.RootElement.GetProperty("country").GetString();
                        return $"{city}, {country}"; // Example: "Petaling Jaya, Malaysia"
                    }
                }
            }
            catch
            {
                // Silently fail if the API is down, so the user can still log in
            }

            return "Unknown Location";
        }
    }
}