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

namespace FYP.Pages.Admin.Academic.CoCurriculum;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class IndexModel : PageModel
{
    private readonly DB _context;

    public IndexModel(DB context)
    {
        _context = context;
    }

    public List<Cocurriculum> Activities { get; set; } = new();

    [BindProperty] public DateTime? GlobalStartDate { get; set; }
    [BindProperty] public DateTime? GlobalEndDate { get; set; }

    [TempData] public string SuccessMessage { get; set; }
    [TempData] public string ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Activities = await _context.Cocurriculums
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Name)
            .ToListAsync();

        // Get the global dates from the first record (since they share the same dates)
        var first = Activities.FirstOrDefault();
        if (first != null)
        {
            GlobalStartDate = first.StartDate;
            GlobalEndDate = first.EndDate;
        }
    }

    public async Task<IActionResult> OnPostSetDatesAsync()
    {
        var allActivities = await _context.Cocurriculums.ToListAsync();

        if (!allActivities.Any())
        {
            ErrorMessage = "Please add at least one co-curriculum activity before setting registration dates.";
            return RedirectToPage();
        }

        if (GlobalStartDate.HasValue && GlobalEndDate.HasValue && GlobalStartDate >= GlobalEndDate)
        {
            ErrorMessage = "End date must be later than the start date.";
            return RedirectToPage();
        }

        foreach (var activity in allActivities)
        {
            activity.StartDate = GlobalStartDate;
            activity.EndDate = GlobalEndDate;
        }

        await _context.SaveChangesAsync();
        SuccessMessage = "Global registration dates updated successfully for all activities.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var activity = await _context.Cocurriculums.FindAsync(id);
        if (activity != null)
        {
            // Remove associated enrollments first
            var enrollments = await _context.Enrollments.Where(e => e.CocurriculumId == id).ToListAsync();
            _context.Enrollments.RemoveRange(enrollments);

            _context.Cocurriculums.Remove(activity);
            await _context.SaveChangesAsync();
            SuccessMessage = "Activity deleted successfully.";
        }
        return RedirectToPage();
    }
}