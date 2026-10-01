using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;

namespace FYP.Pages.Admin.Exam.SemesterResult
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
    public class IndexModel : PageModel
    {
        private readonly DB _context;

        public IndexModel(DB context)
        {
            _context = context;
        }

        public List<User> Students { get; set; } = new();

        [BindProperty(SupportsGet = true)] public int? SearchProgId { get; set; }
        [BindProperty(SupportsGet = true)] public int? SearchSession { get; set; }
        [BindProperty(SupportsGet = true)] public string? SearchGroup { get; set; }
        [BindProperty(SupportsGet = true)] public string? SearchName { get; set; }

        public List<SelectListItem> ProgrammeOptions { get; set; } = new();
        public List<SelectListItem> SessionOptions { get; set; } = new();
        public List<SelectListItem> GroupOptions { get; set; } = new();

        public async Task OnGetAsync()
        {
            // 1. Populate Dropdowns dynamically from Active Students
            var activeStudents = _context.Users.Where(u => u.UsersRoleId == 3 && u.StudyStatus == "Active");

            var programmes = await activeStudents.Include(u => u.Programme)
                .Where(u => u.Programme != null).Select(u => u.Programme).Distinct().OrderBy(p => p!.Code).ToListAsync();
            ProgrammeOptions = programmes.Select(p => new SelectListItem { Value = p!.Id.ToString(), Text = $"{p.Code} - {p.Name}" }).ToList();

            var sessions = await activeStudents.Where(u => u.SessionJoined != null)
                .Select(u => u.SessionJoined).Distinct().OrderByDescending(s => s).ToListAsync();
            SessionOptions = sessions.Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString() }).ToList();

            var groups = await activeStudents.Where(u => !string.IsNullOrEmpty(u.TutorialGroup))
                .Select(u => u.TutorialGroup).Distinct().OrderBy(g => g).ToListAsync();
            GroupOptions = groups.Select(g => new SelectListItem { Value = g, Text = g }).ToList();

            // 2. Build Query
            var query = _context.Users
                .Include(u => u.Programme)
                .Where(u => u.UsersRoleId == 3 && u.StudyStatus == "Active")
                .AsQueryable();

            if (SearchProgId.HasValue) query = query.Where(u => u.ProgrammeId == SearchProgId.Value);
            if (SearchSession.HasValue) query = query.Where(u => u.SessionJoined == SearchSession.Value);
            if (!string.IsNullOrEmpty(SearchGroup)) query = query.Where(u => u.TutorialGroup == SearchGroup);
            if (!string.IsNullOrEmpty(SearchName)) query = query.Where(u => u.FullName.Contains(SearchName) || u.UserId.Contains(SearchName));

            Students = await query
                .OrderBy(u => u.Programme!.Code)
                .ThenBy(u => u.SessionJoined)
                .ThenBy(u => u.FullName)
                .ToListAsync();
        }
    }
}