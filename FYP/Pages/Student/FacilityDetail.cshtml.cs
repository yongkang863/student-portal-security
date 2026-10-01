using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using FYP.Models;

namespace FYP.Pages.Student
{
    public class FacilityDetailModel : PageModel
    {
        private readonly DB _context;

        public FacilityDetailModel(DB context)
        {
            _context = context;
        }

        public FYP.Models.Facility DisplayFacility { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var facility = await _context.Facilities
                .Include(f => f.Photos)
                .Include(f => f.Status)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (facility == null) return NotFound();

            DisplayFacility = facility;
            return Page();
        }
    }
}