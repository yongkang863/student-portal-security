using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FYP.Database;
using FYP.Models;

namespace FYP.Pages.Student;

public class CurrentBillingModel : PageModel
{
    private readonly DB _context;

    public CurrentBillingModel(DB context)
    {
        _context = context;
    }

    public IList<FYP.Models.Bill> Bills { get; set; } = new List<FYP.Models.Bill>();

    public async Task<IActionResult> OnGetAsync()
    {
        // Get the logged-in student's ID from the session/claims
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdStr, out int userId))
        {
            return RedirectToPage("/Login"); // Redirect if not properly logged in
        }

        // Fetch bills for this specific student that are NOT Paid (StatusId 6 = Paid)
        Bills = await _context.Bills
            .Include(b => b.Status)
            .Where(b => b.UserId == userId && b.StatusId != 6)
            .OrderBy(b => b.DueDate) // Sort by closest due date first
            .ToListAsync();

        return Page();
    }
}