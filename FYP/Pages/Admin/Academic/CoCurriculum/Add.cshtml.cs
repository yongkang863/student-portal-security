using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Academic.CoCurriculum;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class AddModel : PageModel
{
    private readonly DB _context;

    public AddModel(DB context) { _context = context; }

    [BindProperty] public InputModel Input { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Code is required.")]
        [StringLength(10, ErrorMessage = "Code cannot exceed 10 characters.")]
        public string Code { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(30, ErrorMessage = "Name cannot exceed 30 characters.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Category is required.")]
        [StringLength(30, ErrorMessage = "Category cannot exceed 30 characters.")]
        public string Category { get; set; }

        [Required(ErrorMessage = "Schedule is required.")]
        [StringLength(40, ErrorMessage = "Schedule cannot exceed 40 characters.")]
        public string DayAndTimeRange { get; set; }

        [Required(ErrorMessage = "Venue is required.")]
        [StringLength(30, ErrorMessage = "Venue cannot exceed 30 characters.")]
        public string Venue { get; set; }

        [Required(ErrorMessage = "Max Capacity is required.")]
        [Range(1, 1000, ErrorMessage = "Capacity must be at least 1.")]
        public int MaxCapacity { get; set; }
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        // Prevent exact Code duplicates
        if (await _context.Cocurriculums.AnyAsync(c => c.Code == Input.Code))
        {
            ModelState.AddModelError("Input.Code", "This Activity Code already exists.");
            return Page();
        }

        // Sync dates with existing global dates if they exist
        DateTime? gStart = null;
        DateTime? gEnd = null;
        var existing = await _context.Cocurriculums.FirstOrDefaultAsync();
        if (existing != null)
        {
            gStart = existing.StartDate;
            gEnd = existing.EndDate;
        }

        var newActivity = new Cocurriculum
        {
            Code = Input.Code,
            Name = Input.Name,
            Category = Input.Category,
            DayAndTimeRange = Input.DayAndTimeRange,
            Venue = Input.Venue,
            MaxCapacity = Input.MaxCapacity,
            SeatAvailable = Input.MaxCapacity, // Initially all seats are available
            StartDate = gStart,
            EndDate = gEnd
        };

        _context.Cocurriculums.Add(newActivity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "New activity created successfully.";
        return RedirectToPage("/Admin/Academic/CoCurriculum/Index");
    }
}