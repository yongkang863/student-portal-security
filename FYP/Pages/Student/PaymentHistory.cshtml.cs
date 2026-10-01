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

public class PaymentHistoryModel : PageModel
{
    private readonly DB _context;

    public PaymentHistoryModel(DB context)
    {
        _context = context;
    }

    public IList<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
    public decimal TotalAmountPaid { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // Get the logged-in student's ID from the session/claims
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdStr, out int userId))
        {
            return RedirectToPage("/Login");
        }

        // Fetch successful transactions (StatusId = 6 is Paid) for bills owned by this student
        Transactions = await _context.Set<PaymentTransaction>()
            .Include(p => p.Bill)
            .Where(p => p.Bill.UserId == userId && p.StatusId == 6)
            .OrderByDescending(p => p.PaymentDate) // Show newest payments first
            .ToListAsync();

        // Calculate total historical amount paid for the stats card
        TotalAmountPaid = Transactions.Sum(t => t.Amount);

        return Page();
    }
}