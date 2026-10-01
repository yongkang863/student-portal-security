using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Academic.Structure;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class ManageModel : PageModel
{
    private readonly DB _context;

    public ManageModel(DB context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)] public int ProgId { get; set; }
    [BindProperty(SupportsGet = true)] public int SessionJoined { get; set; }

    public Models.Programme? CurrentProgramme { get; set; }
    public Dictionary<int, Dictionary<int, List<ProgrammeStructure>>> OrganizedStructure { get; set; } = new();
    public List<SelectListItem> CourseOptions { get; set; } = new();

    [TempData] public string SuccessMessage { get; set; }
    [TempData] public string ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (ProgId == 0 || SessionJoined == 0) return RedirectToPage("/Admin/Academic/Structure/Index");

        CurrentProgramme = await _context.Set<Models.Programme>().FindAsync(ProgId);
        if (CurrentProgramme == null) return NotFound();

        await LoadCourseOptionsAsync();

        var flatStructure = await _context.ProgrammeStructures
            .Include(ps => ps.Course)
            .Where(ps => ps.ProgrammeId == ProgId && ps.SessionJoined == SessionJoined)
            .ToListAsync();

        int totalYears = CurrentProgramme.YearToComplete ?? 1;

        for (int y = 1; y <= totalYears; y++)
        {
            OrganizedStructure[y] = new Dictionary<int, List<ProgrammeStructure>>();
            for (int s = 1; s <= 3; s++)
            {
                OrganizedStructure[y][s] = flatStructure
                    .Where(x => x.YearLevel == y && x.Semester == s)
                    .OrderBy(x => x.Course?.Code)
                    .ToList();
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAddCourseAsync(int year, int sem, int courseId)
    {
        if (ProgId == 0 || SessionJoined == 0 || courseId == 0)
            return RedirectToPage(new { ProgId, SessionJoined });

        bool exists = await _context.ProgrammeStructures.AnyAsync(ps =>
            ps.ProgrammeId == ProgId && ps.SessionJoined == SessionJoined &&
            ps.YearLevel == year && ps.Semester == sem && ps.CourseId == courseId);

        if (!exists)
        {
            _context.ProgrammeStructures.Add(new ProgrammeStructure
            {
                ProgrammeId = ProgId,
                SessionJoined = SessionJoined,
                YearLevel = year,
                Semester = sem,
                CourseId = courseId
            });
            await _context.SaveChangesAsync();
            SuccessMessage = "Course added successfully.";
        }
        else
        {
            ErrorMessage = "This course is already assigned to this semester.";
        }

        return RedirectToPage(new { ProgId, SessionJoined });
    }

    public async Task<IActionResult> OnPostRemoveCourseAsync(int structureId)
    {
        var item = await _context.ProgrammeStructures.FindAsync(structureId);
        if (item != null)
        {
            _context.ProgrammeStructures.Remove(item);
            await _context.SaveChangesAsync();
            SuccessMessage = "Course removed successfully.";
        }
        return RedirectToPage(new { ProgId, SessionJoined });
    }

    private async Task LoadCourseOptionsAsync()
    {
        CourseOptions = await _context.Set<Course>()
            .OrderBy(c => c.Code)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = $"{c.Code} - {c.Title} ({c.CreditHours} Cr)" })
            .ToListAsync();
    }
}