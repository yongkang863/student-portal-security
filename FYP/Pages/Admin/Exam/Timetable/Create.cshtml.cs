using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;

namespace FYP.Pages.Admin.Exam.Timetable
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
    public class CreateModel : PageModel
    {
        private readonly DB _context;

        public CreateModel(DB context)
        {
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<SelectListItem> CohortOptions { get; set; } = new();

        [TempData] public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Please select a valid Programme and Session cohort.")]
            public string SelectedCohort { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            await LoadDropdownsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || string.IsNullOrEmpty(Input.SelectedCohort))
            {
                await LoadDropdownsAsync();
                return Page();
            }

            // Parse the selected string (Format: "ProgrammeId_SessionJoined")
            var parts = Input.SelectedCohort.Split('_');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int programmeId) || !int.TryParse(parts[1], out int sessionJoined))
            {
                ErrorMessage = "Invalid cohort selection.";
                await LoadDropdownsAsync();
                return Page();
            }

            // Create the new Schedule
            var schedule = new Models.ExamSchedule
            {
                ProgrammeId = programmeId,
                SessionJoined = sessionJoined,
                Status = "Draft",
                LastUpdated = DateTime.Now
            };

            _context.ExamSchedules.Add(schedule);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Exam schedule created successfully! You can now add exam slots below.";
            return RedirectToPage("/Admin/Exam/Timetable/Manage", new { id = schedule.Id });
        }

        private async Task LoadDropdownsAsync()
        {
            // 1. Get all Active students, grouped by their Programme and Session
            var activeCohorts = await _context.Users
                .Where(u => u.UsersRoleId == 3 && u.StudyStatus == "Active" && u.ProgrammeId != null && u.SessionJoined != null)
                .Select(u => new
                {
                    ProgrammeId = u.ProgrammeId!.Value,
                    SessionJoined = u.SessionJoined!.Value,
                    Code = u.Programme!.Code,
                    Name = u.Programme!.Name
                })
                .Distinct()
                .ToListAsync();

            // 2. Get existing schedules to avoid duplicates
            var existingSchedules = await _context.ExamSchedules
                .Select(s => new { s.ProgrammeId, s.SessionJoined })
                .ToListAsync();

            // 3. Filter out cohorts that already have a schedule
            var availableCohorts = activeCohorts
                .Where(ac => !existingSchedules.Any(es => es.ProgrammeId == ac.ProgrammeId && es.SessionJoined == ac.SessionJoined))
                .OrderBy(ac => ac.Code).ThenByDescending(ac => ac.SessionJoined)
                .ToList();

            // 4. Populate Dropdown (Value stores BOTH IDs)
            CohortOptions = availableCohorts.Select(c => new SelectListItem
            {
                Value = $"{c.ProgrammeId}_{c.SessionJoined}",
                Text = $"{c.Code} - {c.Name} (Session: {c.SessionJoined})"
            }).ToList();
        }
    }
}