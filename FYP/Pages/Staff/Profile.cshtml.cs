using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FYP.Pages.Staff;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly DB _context;

    public ProfileModel(DB context)
    {
        _context = context;
    }

    public User CurrentStaff { get; set; }
    public string ProfileImageSrc { get; set; } = "/images/default-avatar.png"; // Fallback image path

    public List<Course> TeachingCourses { get; set; } = new();
    public async Task<IActionResult> OnGetAsync()
    {
        var uidClaim = User.FindFirst("Uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int currentUid = 0;

        if (!int.TryParse(uidClaim, out currentUid))
        {
            currentUid = HttpContext.Session.GetInt32("Uid") ?? 0;
        }

        if (currentUid == 0) return RedirectToPage("/Staff/Login");

        // Fetch User and include the new StaffCourses mapping
        CurrentStaff = await _context.Users
            .Include(u => u.Department)
            .Include(u => u.Role)
            .Include(u => u.Bank)
            .Include(u => u.StaffCourses)          // Include the mapping table
                .ThenInclude(sc => sc.Course)      // Include the actual Course details
            .FirstOrDefaultAsync(u => u.Uid == currentUid);

        if (CurrentStaff == null) return NotFound();

        // Extract just the courses into a clean list for the HTML to use
        if (CurrentStaff.StaffCourses != null)
        {
            TeachingCourses = CurrentStaff.StaffCourses.Select(sc => sc.Course).ToList();
        }

        if (CurrentStaff.Photo != null && CurrentStaff.Photo.Length > 0)
        {
            string base64Data = Convert.ToBase64String(CurrentStaff.Photo);
            ProfileImageSrc = $"data:image/jpeg;base64,{base64Data}";
        }

        return Page();
    }
}