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
public class DetailsModel : PageModel
{
    private readonly DB _context;

    public DetailsModel(DB context)
    {
        _context = context;
    }

    public FYP.Models.Bill MainBill { get; set; }
    public List<FYP.Models.Bill> TargetedStudents { get; set; } = new();

    public int TotalStudents { get; set; }
    public int TotalPaid { get; set; }
    public int TotalPending { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        MainBill = await _context.Set<FYP.Models.Bill>().FirstOrDefaultAsync(b => b.Id == id);

        if (MainBill == null)
        {
            return RedirectToPage("/Admin/Bill/Index");
        }

        TargetedStudents = await _context.Set<FYP.Models.Bill>()
            .Include(b => b.User)
            .Where(b => b.Title == MainBill.Title
                     && b.Amount == MainBill.Amount
                     && b.DueDate == MainBill.DueDate)
            .OrderBy(b => b.User.UserId)
            .ToListAsync();

        TotalStudents = TargetedStudents.Count;
        TotalPaid = TargetedStudents.Count(b => b.StatusId == 6);
        TotalPending = TargetedStudents.Count(b => b.StatusId == 1 || b.StatusId == 7);

        return Page();
    }

    // UPDATED: Only updates status to Late Payment (7)
    public async Task<IActionResult> OnPostToggleStatusAsync(int targetBillId, int mainBillId)
    {
        var targetBill = await _context.Set<FYP.Models.Bill>().FindAsync(targetBillId);

        if (targetBill != null)
        {
            // Set status to Late Payment (7)
            targetBill.StatusId = 7;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Status updated to Late Payment.";
        }

        return RedirectToPage(new { id = mainBillId });
    }
}