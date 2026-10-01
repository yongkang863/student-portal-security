using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Staff;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class IndexModel : PageModel
{
    private readonly DB _context;

    public IndexModel(DB context)
    {
        _context = context;
    }

    public List<User> StaffList { get; set; } = new();

    // 1. Bind the Search Term from the URL (e.g. ?SearchTerm=John)
    // SECURITY: The Regex prevents basic XSS payloads by restricting allowed characters.
    [BindProperty(SupportsGet = true)]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_@.]*$", ErrorMessage = "Invalid characters in search.")]
    public string? SearchTerm { get; set; }

    public async Task OnGetAsync()
    {
        // 2. Start the base query
        var query = _context.Users
            .Include(u => u.Role)
            .Include(u => u.Department)
            .Where(u => u.UsersRoleId == 1 || u.UsersRoleId == 2);

        // 3. Apply the Search Filter if the user typed something
        if (!string.IsNullOrEmpty(SearchTerm))
        {
            // SECURITY: EF Core automatically turns this into a parameterized query, preventing SQL Injection.
            query = query.Where(u => u.FullName.Contains(SearchTerm) ||
                                     u.UserId.Contains(SearchTerm) ||
                                     u.Email.Contains(SearchTerm));
        }

        // 4. Execute the query
        StaffList = await query.OrderBy(u => u.UserId).ToListAsync();
    }

    // --- DELETE HANDLER ---
    public async Task<IActionResult> OnPostDeleteAsync(int uid)
    {
        var user = await _context.Users.FindAsync(uid);

        if (user != null)
        {
            var userAudits = await _context.UsersLoginAudits.Where(a => a.Uid == uid).ToListAsync();
            if (userAudits.Any())
            {
                _context.UsersLoginAudits.RemoveRange(userAudits);
            }

            var userCourses = await _context.StaffCourses.Where(sc => sc.Uid == uid).ToListAsync();
            if (userCourses.Any())
            {
                _context.StaffCourses.RemoveRange(userCourses);
            }

            await _context.SaveChangesAsync();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}