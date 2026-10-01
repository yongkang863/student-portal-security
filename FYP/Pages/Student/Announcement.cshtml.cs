using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FYP.Database;
using FYP.Models;
using System.Linq;

namespace FYP.Pages.Student
{
    [Authorize(Policy = "StudentOnly", AuthenticationSchemes = "StudentCookie")]
    public class AnnouncementModel : PageModel
    {
        private readonly DB _context;

        public AnnouncementModel(DB context)
        {
            _context = context;
        }

        public List<Announcement> AnnouncementList { get; set; } = new List<Announcement>();

        // Bind these properties to the URL parameters (e.g., ?SearchTerm=exam&FilterType=week)
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterType { get; set; } // "all", "week", "month"

        public void OnGet()
        {
            // 1. Start with the base query: Target "All" or "Student"
            var query = _context.Announcements
                .Where(a => a.TargetRole == "All" || a.TargetRole == "Student");

            // 2. Apply Search Filter
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                // SQL Server 'LIKE' is case-insensitive by default, so this works fine.
                query = query.Where(a => a.Title.Contains(SearchTerm) ||
                                         a.Description.Contains(SearchTerm));
            }

            // 3. Apply Time Filter (Week / Month)
            if (!string.IsNullOrEmpty(FilterType))
            {
                var today = DateTime.Now;
                if (FilterType == "week")
                {
                    var lastWeek = today.AddDays(-7);
                    query = query.Where(a => a.PublishDate >= lastWeek);
                }
                else if (FilterType == "month")
                {
                    var lastMonth = today.AddDays(-30);
                    query = query.Where(a => a.PublishDate >= lastMonth);
                }
            }

            // 4. Execute Query
            AnnouncementList = query.OrderByDescending(a => a.PublishDate).ToList();
        }
    }
}