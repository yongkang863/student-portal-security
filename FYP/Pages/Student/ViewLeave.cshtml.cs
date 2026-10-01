using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FYP.Database;
using FYP.Models;

namespace FYP.Pages.Student
{
    public class ViewLeaveModel : PageModel
    {
        private readonly DB _context;

        public ViewLeaveModel(DB context)
        {
            _context = context;
        }

        public IList<StudentLeave> LeaveHistory { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {
         
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int studentId = string.IsNullOrEmpty(uidString) ? 1 : int.Parse(uidString);


            LeaveHistory = await _context.StudentLeaves
                .Where(l => l.StudentId == studentId)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();

            return Page();
        }
    }
}