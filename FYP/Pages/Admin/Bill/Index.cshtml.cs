using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Bill;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class IndexModel : PageModel
{
    private readonly DB _context;

    public IndexModel(DB context)
    {
        _context = context;
    }

    // New Data Transfer Object to hold our grouped bill data
    public class GroupedBillDto
    {
        public int FirstBillId { get; set; } // Used as a reference to fetch details/delete
        public string Title { get; set; }
        public string BillType { get; set; }
        public decimal Amount { get; set; }
        public DateOnly DueDate { get; set; }
        public int TotalStudents { get; set; }
        public int TotalPaid { get; set; }
        public int TotalPending { get; set; }
    }

    public IList<GroupedBillDto> GroupedBills { get; set; } = new List<GroupedBillDto>();

    [TempData]
    public string SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        // Fetch all bills into memory to group them
        var allBills = await _context.Set<FYP.Models.Bill>().ToListAsync();

        // Group by Title, Amount, and Due Date (which identifies a specific "batch")
        GroupedBills = allBills
            .GroupBy(b => new { b.Title, b.Amount, b.DueDate, b.BillType })
            .Select(g => new GroupedBillDto
            {
                FirstBillId = g.First().Id, // Grab the ID of the first bill in this group
                Title = g.Key.Title,
                BillType = g.Key.BillType,
                Amount = g.Key.Amount,
                DueDate = g.Key.DueDate,
                TotalStudents = g.Count(),
                TotalPaid = g.Count(b => b.StatusId == 6), // 6 = Paid
                TotalPending = g.Count(b => b.StatusId == 1) // 1 = Pending
            })
            .OrderByDescending(g => g.DueDate)
            .ToList();
    }

    // --- DELETE GROUP HANDLER ---
    public async Task<IActionResult> OnPostDeleteGroupAsync(int id)
    {
        // Find the reference bill to know what batch criteria to look for
        var referenceBill = await _context.Set<FYP.Models.Bill>().FindAsync(id);

        if (referenceBill != null)
        {
            // Find ALL bills that match this batch's criteria
            var billsToDelete = await _context.Set<FYP.Models.Bill>()
                .Where(b => b.Title == referenceBill.Title
                         && b.Amount == referenceBill.Amount
                         && b.DueDate == referenceBill.DueDate)
                .ToListAsync();

            int deleteCount = billsToDelete.Count;

            _context.Set<FYP.Models.Bill>().RemoveRange(billsToDelete);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Successfully deleted the bill batch '{referenceBill.Title}' ({deleteCount} student records removed).";
        }

        return RedirectToPage();
    }
}