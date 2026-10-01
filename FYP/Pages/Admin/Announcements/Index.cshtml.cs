using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FYP.Pages.Admin.Announcements;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class IndexModel : PageModel
{
    private readonly DB _context;

    public IndexModel(DB context)
    {
        _context = context;
    }

    public List<Announcement> AnnouncementList { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Fetch announcements (Newest First)
        // We Include 'Admin' so we can show who posted it (optional)
        AnnouncementList = await _context.Announcements
            .Include(a => a.Admin)
            .OrderByDescending(a => a.PublishDate)
            .ToListAsync();
    }

    // --- DELETE HANDLER ---
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var announcement = await _context.Announcements.FindAsync(id);

        if (announcement != null)
        {
            _context.Announcements.Remove(announcement);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}