using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using FYP.Models;

namespace FYP.Pages.Student
{
    public class FacilityModel : PageModel
    {
        private readonly DB _context;

        public FacilityModel(DB context)
        {
            _context = context;
        }

        public IList<FYP.Models.Facility> FacilityList { get; set; } = default!;

        public async Task OnGetAsync()
        {
            FacilityList = await _context.Facilities
                .Include(f => f.Photos)
                .Include(f => f.Status)
                .ToListAsync();
        }
    }
}