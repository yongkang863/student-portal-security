using FYP.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FYP.Pages.Admin.Facilities
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
    public class DetailsModel : PageModel
    {
        private readonly DB _context;
        public DetailsModel(DB context) { _context = context; }

        public FYP.Models.Facility DisplayFacility { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var facility = await _context.Facilities
                .Include(f => f.Photos)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (facility == null) return NotFound();
            DisplayFacility = facility;
            return Page();
        }
    }
}