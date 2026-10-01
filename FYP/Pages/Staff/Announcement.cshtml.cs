using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FYP.Database;
using FYP.Models;
using System.Linq;

namespace FYP.Pages.Staff
{
    // Ensuring it uses your Staff Authentication Scheme
    [Authorize(AuthenticationSchemes = "StaffAuth")]
    public class AnnouncementModel : PageModel
    {
        private readonly DB _context;

        public AnnouncementModel(DB context)
        {
            _context = context;
        }

        public List<Announcement> AnnouncementList { get; set; } = new List<Announcement>();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterType { get; set; }

        public void OnGet()
        {
            // Filter: Show "All" announcements or announcements specifically for "Staff"
            var query = _context.Announcements
                .Where(a => a.TargetRole == "All" || a.TargetRole == "Staff");

            if (!string.IsNullOrEmpty(SearchTerm))
            {
                query = query.Where(a => a.Title.Contains(SearchTerm) ||
                                         a.Description.Contains(SearchTerm));
            }

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

            AnnouncementList = query.OrderByDescending(a => a.PublishDate).ToList();
        }
    }
}