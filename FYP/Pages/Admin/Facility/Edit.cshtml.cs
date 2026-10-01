using FYP.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace FYP.Pages.Admin.Facilities
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
    public class EditModel : PageModel
    {
        private readonly DB _context;

        public EditModel(DB context)
        {
            _context = context;
        }

        [BindProperty]
        public FYP.Models.Facility EditFacility { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var facility = await _context.Facilities.FirstOrDefaultAsync(m => m.Id == id);
            if (facility == null) return NotFound();

            EditFacility = facility;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var safeInputRegex = new Regex(@"^[\p{L}\p{N}\s\-_.,()!?'""&]+$");

            if (!string.IsNullOrWhiteSpace(EditFacility.Name) && !safeInputRegex.IsMatch(EditFacility.Name))
                ModelState.AddModelError("EditFacility.Name", "Facility Name contains emojis or invalid characters.");

            if (!string.IsNullOrWhiteSpace(EditFacility.Remark) && !safeInputRegex.IsMatch(EditFacility.Remark))
                ModelState.AddModelError("EditFacility.Remark", "Remark contains emojis or invalid characters.");

            if (!ModelState.IsValid) return Page();

            var facilityToUpdate = await _context.Facilities.FindAsync(EditFacility.Id);
            if (facilityToUpdate == null) return NotFound();

            facilityToUpdate.Name = EditFacility.Name;
            facilityToUpdate.Remark = EditFacility.Remark;
            facilityToUpdate.FacilityType = EditFacility.FacilityType;
            facilityToUpdate.StatusId = EditFacility.StatusId;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Facility updated successfully!";
            return RedirectToPage("./Index");
        }
    }
}