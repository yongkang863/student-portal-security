using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FYP.Database;
using FYP.Models;

namespace FYP.Pages.Staff
{
    public class ViewLeaveModel : PageModel
    {
        private readonly DB _context;

        public ViewLeaveModel(DB context)
        {
            _context = context;
        }

        public IList<StaffLeave> LeaveHistory { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {
            // Extract Staff Uid from Claims
            var uidClaim = User.FindFirst("Uid")?.Value;
            int staffId = !string.IsNullOrEmpty(uidClaim) ? int.Parse(uidClaim) : 0;

            // Fetch Staff leave history ordered by the most recent start date
            LeaveHistory = await _context.StaffLeaves
                .Where(l => l.StaffId == staffId)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();

            return Page();
        }
    }
}