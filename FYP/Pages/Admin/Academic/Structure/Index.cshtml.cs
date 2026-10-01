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
public class IndexModel : PageModel
{
    private readonly DB _context;

    public IndexModel(DB context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)] public int? ProgId { get; set; }
    [BindProperty(SupportsGet = true)] public int? SessionJoined { get; set; }

    public Models.Programme? CurrentProgramme { get; set; }
    public Dictionary<int, Dictionary<int, List<ProgrammeStructure>>> OrganizedStructure { get; set; } = new();

    public bool IsConfigured { get; set; }
    public bool HasSearched => ProgId.HasValue && SessionJoined.HasValue;

    public List<SelectListItem> ProgrammeOptions { get; set; } = new();
    public List<SelectListItem> SessionOptions { get; set; } = new();
    public List<UnsetCohortDto> UnsetCohorts { get; set; } = new();

    [TempData] public string SuccessMessage { get; set; }

    public class UnsetCohortDto
    {
        public int ProgrammeId { get; set; }
        public string ProgrammeCode { get; set; }
        public string ProgrammeName { get; set; }
        public int SessionJoined { get; set; }
        public int StudentCount { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadDropdownsAndUnsetCohortsAsync();

        if (HasSearched)
        {
            CurrentProgramme = await _context.Set<Models.Programme>().FindAsync(ProgId.Value);

            if (CurrentProgramme != null)
            {
                var flatStructure = await _context.ProgrammeStructures
                    .Include(ps => ps.Course)
                    .Where(ps => ps.ProgrammeId == ProgId.Value && ps.SessionJoined == SessionJoined.Value)
                    .ToListAsync();

                IsConfigured = flatStructure.Any();
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
            }
        }
    }

    private async Task LoadDropdownsAndUnsetCohortsAsync()
    {
        // Populate standard dropdowns
        ProgrammeOptions = await _context.Set<Models.Programme>()
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = $"{p.Code} - {p.Name}" })
            .ToListAsync();

        var studentSessions = await _context.Users.Where(u => u.SessionJoined != null).Select(u => u.SessionJoined.Value).Distinct().ToListAsync();
        var structureSessions = await _context.ProgrammeStructures.Select(ps => ps.SessionJoined).Distinct().ToListAsync();

        SessionOptions = studentSessions.Union(structureSessions)
            .OrderByDescending(s => s)
            .Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString() })
            .ToList();

        // Calculate Unset Cohorts
        var studentCohorts = await _context.Users
            .Where(u => u.UsersRoleId == 3 && u.ProgrammeId != null && u.SessionJoined != null)
            .GroupBy(u => new { u.ProgrammeId, u.SessionJoined })
            .Select(g => new {
                ProgId = g.Key.ProgrammeId.Value,
                Session = g.Key.SessionJoined.Value,
                Count = g.Count()
            }).ToListAsync();

        var configuredCohorts = await _context.ProgrammeStructures
            .Select(ps => new { ps.ProgrammeId, ps.SessionJoined })
            .Distinct().ToListAsync();

        var programmes = await _context.Set<Models.Programme>().ToListAsync();

        foreach (var sc in studentCohorts)
        {
            if (!configuredCohorts.Any(c => c.ProgrammeId == sc.ProgId && c.SessionJoined == sc.Session))
            {
                var prog = programmes.FirstOrDefault(p => p.Id == sc.ProgId);
                if (prog != null)
                {
                    UnsetCohorts.Add(new UnsetCohortDto
                    {
                        ProgrammeId = prog.Id,
                        ProgrammeCode = prog.Code,
                        ProgrammeName = prog.Name,
                        SessionJoined = sc.Session,
                        StudentCount = sc.Count
                    });
                }
            }
        }

        UnsetCohorts = UnsetCohorts.OrderBy(u => u.SessionJoined).ThenBy(u => u.ProgrammeCode).ToList();
    }
}