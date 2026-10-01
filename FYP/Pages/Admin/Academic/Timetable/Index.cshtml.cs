using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Academic.Timetable;

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
    [BindProperty(SupportsGet = true)] public string? TutorialGroup { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? SelectedDate { get; set; }

    [BindProperty] public AddSlotModel Input { get; set; } = new();
    [BindProperty] public EditSlotModel EditInput { get; set; } = new();

    public Models.Programme? CurrentProgramme { get; set; }
    public List<TimetableSlot> Slots { get; set; } = new();
    public Dictionary<int, string> StaffNames { get; set; } = new();

    public DateOnly StartOfWeek { get; set; }
    public DateOnly EndOfWeek { get; set; }
    public bool HasSearched => ProgId.HasValue && SessionJoined.HasValue && !string.IsNullOrEmpty(TutorialGroup);

    [TempData] public string SuccessMessage { get; set; }
    [TempData] public string ErrorMessage { get; set; }

    // Dropdowns & Dynamic Data
    public List<SelectListItem> ProgrammeOptions { get; set; } = new();
    public List<SelectListItem> SessionOptions { get; set; } = new();
    public List<SelectListItem> CourseOptions { get; set; } = new();
    public List<SelectListItem> VenueOptions { get; set; } = new();
    public List<SelectListItem> StaffOptions { get; set; } = new();
    public string CohortGroupsJson { get; set; } = "{}";

    public class AddSlotModel
    {
        [Required] public DateTime Date { get; set; }
        [Required] public TimeSpan StartTime { get; set; }
        [Required] public TimeSpan EndTime { get; set; }
        [Required] public int CourseId { get; set; }
        [Required] public string ClassType { get; set; }
        public int? VenueId { get; set; }
        public int? StaffId { get; set; }

        // New field for automatic weekly assignment
        [Required]
        [Range(1, 14, ErrorMessage = "Recurrence must be between 1 and 14 weeks.")]
        public int NumberOfWeeks { get; set; } = 1;
    }

    public class EditSlotModel : AddSlotModel
    {
        [Required] public int SlotId { get; set; }
        // Edit is usually for a single slot adjustment
    }

    public async Task OnGetAsync()
    {
        await SetupDateRange();
        await LoadDropdownsAsync();

        if (HasSearched)
        {
            CurrentProgramme = await _context.Set<Models.Programme>().FindAsync(ProgId.Value);

            Slots = await _context.TimetableSlots
                .Include(t => t.Course)
                .Include(t => t.Venue)
                .Where(t => t.ProgrammeId == ProgId.Value
                         && t.SessionJoined == SessionJoined.Value
                         && t.TutorialGroup == TutorialGroup
                         && t.Date >= StartOfWeek
                         && t.Date <= EndOfWeek)
                .ToListAsync();

            var staffIds = Slots.Where(s => s.StaffId.HasValue).Select(s => s.StaffId.Value).Distinct().ToList();
            if (staffIds.Any())
            {
                StaffNames = await _context.Users
                    .Where(u => staffIds.Contains(u.Uid))
                    .ToDictionaryAsync(u => u.Uid, u => u.FullName);
            }
        }
    }

    public async Task<IActionResult> OnPostAddSlotAsync()
    {
        if (!ProgId.HasValue || !SessionJoined.HasValue || string.IsNullOrEmpty(TutorialGroup))
            return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate });

        TimeOnly start = TimeOnly.FromTimeSpan(Input.StartTime);
        TimeOnly end = TimeOnly.FromTimeSpan(Input.EndTime);
        DateOnly baseDate = DateOnly.FromDateTime(Input.Date);

        if (!ValidateTimeBounds(start, end)) return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate });

        var newSlots = new List<TimetableSlot>();

        // Loop through the requested number of weeks
        for (int i = 0; i < Input.NumberOfWeeks; i++)
        {
            DateOnly currentDate = baseDate.AddDays(i * 7);
            string dateStr = currentDate.ToString("dd MMM yyyy");

            // Overlaps for this specific consecutive week
            var overlaps = await _context.TimetableSlots.Where(s => s.Date == currentDate && s.StartTime < end && s.EndTime > start).ToListAsync();

            // Check Cohort
            if (overlaps.Any(s => s.ProgrammeId == ProgId.Value && s.SessionJoined == SessionJoined.Value && s.TutorialGroup == TutorialGroup))
            {
                TempData["ErrorMessage"] = $"Cohort Clash on {dateStr}: This group already has a class scheduled at this time.";
                return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate });
            }

            // Check Staff
            if (Input.StaffId.HasValue && overlaps.Any(s => s.StaffId == Input.StaffId.Value))
            {
                TempData["ErrorMessage"] = $"Staff Clash on {dateStr}: The selected lecturer is teaching another class at this time.";
                return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate });
            }

            // Check Venue
            if (Input.VenueId.HasValue && overlaps.Any(s => s.VenueId == Input.VenueId.Value))
            {
                TempData["ErrorMessage"] = $"Venue Clash on {dateStr}: The selected venue is already booked at this time.";
                return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate });
            }

            newSlots.Add(new TimetableSlot
            {
                ProgrammeId = ProgId.Value,
                SessionJoined = SessionJoined.Value,
                TutorialGroup = TutorialGroup,
                Date = currentDate,
                StartTime = start,
                EndTime = end,
                CourseId = Input.CourseId,
                ClassType = Input.ClassType,
                VenueId = Input.VenueId,
                StaffId = Input.StaffId
            });
        }

        // Save all weeks
        _context.TimetableSlots.AddRange(newSlots);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Successfully scheduled {Input.NumberOfWeeks} class slot(s)!";

        return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate = Input.Date });
    }

    public async Task<IActionResult> OnPostEditSlotAsync()
    {
        if (!ProgId.HasValue || !SessionJoined.HasValue || string.IsNullOrEmpty(TutorialGroup))
            return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate });

        TimeOnly start = TimeOnly.FromTimeSpan(EditInput.StartTime);
        TimeOnly end = TimeOnly.FromTimeSpan(EditInput.EndTime);
        DateOnly date = DateOnly.FromDateTime(EditInput.Date);
        string dateStr = date.ToString("dd MMM yyyy");

        if (!ValidateTimeBounds(start, end)) return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate });

        // Check overlaps excluding the current slot being edited
        var overlaps = await _context.TimetableSlots.Where(s => s.Id != EditInput.SlotId && s.Date == date && s.StartTime < end && s.EndTime > start).ToListAsync();

        if (overlaps.Any(s => s.ProgrammeId == ProgId.Value && s.SessionJoined == SessionJoined.Value && s.TutorialGroup == TutorialGroup))
        { TempData["ErrorMessage"] = $"Cohort Clash on {dateStr}: This group already has a class scheduled at this time."; return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate }); }

        if (EditInput.StaffId.HasValue && overlaps.Any(s => s.StaffId == EditInput.StaffId.Value))
        { TempData["ErrorMessage"] = $"Staff Clash on {dateStr}: The selected lecturer is teaching another class at this time."; return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate }); }

        if (EditInput.VenueId.HasValue && overlaps.Any(s => s.VenueId == EditInput.VenueId.Value))
        { TempData["ErrorMessage"] = $"Venue Clash on {dateStr}: The selected venue is already booked at this time."; return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate }); }

        var slot = await _context.TimetableSlots.FindAsync(EditInput.SlotId);
        if (slot != null)
        {
            slot.Date = date;
            slot.StartTime = start;
            slot.EndTime = end;
            slot.CourseId = EditInput.CourseId;
            slot.ClassType = EditInput.ClassType;
            slot.VenueId = EditInput.VenueId;
            slot.StaffId = EditInput.StaffId;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Class slot updated successfully!";
        }

        return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate = EditInput.Date });
    }

    public async Task<IActionResult> OnPostDeleteSlotAsync(int deleteSlotId)
    {
        var slot = await _context.TimetableSlots.FindAsync(deleteSlotId);
        if (slot != null)
        {
            _context.TimetableSlots.Remove(slot);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Class slot removed successfully.";
        }
        return RedirectToPage(new { ProgId, SessionJoined, TutorialGroup, SelectedDate });
    }

    // --- HELPER METHODS ---

    private bool ValidateTimeBounds(TimeOnly start, TimeOnly end)
    {
        TimeOnly minTime = new TimeOnly(8, 0);
        TimeOnly maxTime = new TimeOnly(20, 0);

        if (start < minTime || end > maxTime || start >= end)
        {
            TempData["ErrorMessage"] = "Invalid Time: Classes must be between 08:00 AM and 08:00 PM, and Start Time must be before End Time.";
            return false;
        }
        return true;
    }

    private async Task SetupDateRange()
    {
        DateTime currentDate = SelectedDate ?? DateTime.Today;
        int diff = (7 + (currentDate.DayOfWeek - DayOfWeek.Sunday)) % 7;
        StartOfWeek = DateOnly.FromDateTime(currentDate.AddDays(-1 * diff).Date);
        EndOfWeek = DateOnly.FromDateTime(currentDate.AddDays(-1 * diff + 6).Date);
    }

    private async Task LoadDropdownsAsync()
    {
        ProgrammeOptions = await _context.Set<Models.Programme>()
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = $"{p.Code} - {p.Name}" }).ToListAsync();

        var sessions = await _context.Users.Where(u => u.SessionJoined != null).Select(u => u.SessionJoined.Value).Distinct().ToListAsync();
        SessionOptions = sessions.OrderByDescending(s => s).Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString() }).ToList();

        // 1. Dynamic Tutorial Groups (JSON structure for JavaScript cascade)
        var cohortGroups = await _context.Users
            .Where(u => u.UsersRoleId == 3 && u.ProgrammeId != null && u.SessionJoined != null && !string.IsNullOrEmpty(u.TutorialGroup))
            .GroupBy(u => new { u.ProgrammeId, u.SessionJoined })
            .ToDictionaryAsync(
                g => $"{g.Key.ProgrammeId}_{g.Key.SessionJoined}",
                g => g.Select(u => u.TutorialGroup).Distinct().OrderBy(x => x).ToList()
            );
        CohortGroupsJson = JsonSerializer.Serialize(cohortGroups);

        // 2. Load relevant courses if Programme and Session are selected
        if (ProgId.HasValue && SessionJoined.HasValue)
        {
            var validCourseIds = await _context.ProgrammeStructures
                .Where(ps => ps.ProgrammeId == ProgId && ps.SessionJoined == SessionJoined)
                .Select(ps => ps.CourseId).ToListAsync();

            CourseOptions = await _context.Set<Course>().Where(c => validCourseIds.Contains(c.Id))
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = $"{c.Code} - {c.Title}" }).ToListAsync();
        }

        VenueOptions = await _context.Set<Venue>()
            .Select(v => new SelectListItem { Value = v.Id.ToString(), Text = $"{v.Name} ({v.Classroom})" }).ToListAsync();

        StaffOptions = await _context.Users.Where(u => u.UsersRoleId == 2)
            .Select(s => new SelectListItem { Value = s.Uid.ToString(), Text = s.FullName }).ToListAsync();
    }
}