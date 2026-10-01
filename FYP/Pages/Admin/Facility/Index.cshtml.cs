using FYP.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FYP.Pages.Admin.Facilities
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
    public class IndexModel : PageModel
    {
        private readonly DB _context;

        public IndexModel(DB context)
        {
            _context = context;
        }

        public IList<FYP.Models.Facility> FacilityList { get; set; } = default!;

        public async Task OnGetAsync()
        {
            // The .Include(f => f.Status) is crucial here so we can read the word "Available" instead of just the number 1
            FacilityList = await _context.Facilities
                .Include(f => f.Photos)
                .Include(f => f.Status)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var facility = await _context.Facilities.FindAsync(id);
            if (facility != null)
            {
                _context.Facilities.Remove(facility);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Facility removed successfully!";
            }
            return RedirectToPage();
        }
    }
}