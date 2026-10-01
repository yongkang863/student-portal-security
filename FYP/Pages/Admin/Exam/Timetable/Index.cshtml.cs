using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace FYP.Pages.Admin.Exam.Timetable
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
    public class IndexModel : PageModel
    {
        private readonly DB _context;

        public IndexModel(DB context)
        {
            _context = context;
        }

        public List<ExamSchedule> Schedules { get; set; } = new();

        [BindProperty(SupportsGet = true)] public int? SearchProgId { get; set; }
        [BindProperty(SupportsGet = true)] public int? SearchSession { get; set; }

        // Property to catch the selected checkboxes for bulk delete
        [BindProperty] public List<int> SelectedSchedules { get; set; } = new();

        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }

        public List<SelectListItem> ProgrammeOptions { get; set; } = new();
        public List<SelectListItem> SessionOptions { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadPageDataAsync();
        }

        private async Task LoadPageDataAsync()
        {
            var programmes = await _context.ExamSchedules
                .Include(s => s.Programme)
                .Select(s => s.Programme)
                .Distinct()
                .OrderBy(p => p!.Code)
                .ToListAsync();

            ProgrammeOptions = programmes.Select(p => new SelectListItem
            {
                Value = p!.Id.ToString(),
                Text = $"{p.Code} - {p.Name}"
            }).ToList();

            var sessions = await _context.ExamSchedules
                .Select(s => s.SessionJoined)
                .Distinct()
                .OrderByDescending(s => s)
                .ToListAsync();

            SessionOptions = sessions.Select(s => new SelectListItem
            {
                Value = s.ToString(),
                Text = s.ToString()
            }).ToList();

            var query = _context.ExamSchedules
                .Include(s => s.Programme)
                .AsQueryable();

            if (SearchProgId.HasValue) query = query.Where(s => s.ProgrammeId == SearchProgId.Value);
            if (SearchSession.HasValue) query = query.Where(s => s.SessionJoined == SearchSession.Value);

            Schedules = await query
                .OrderByDescending(s => s.SessionJoined)
                .ThenBy(s => s.Programme!.Code)
                .ToListAsync();
        }

        // --- BULK DELETE FUNCTION ---
        public async Task<IActionResult> OnPostDeleteSelectedAsync()
        {
            if (SelectedSchedules == null || !SelectedSchedules.Any())
            {
                ErrorMessage = "No schedules were selected for deletion.";
                return RedirectToPage();
            }

            try
            {
                // 1. Fetch the schedules to be deleted
                var schedulesToDelete = await _context.ExamSchedules
                    .Where(s => SelectedSchedules.Contains(s.Id))
                    .ToListAsync();

                if (schedulesToDelete.Any())
                {
                    // 2. Fetch all dependent Exam Slots for these schedules
                    // FIX: Removed the "?? 0" because ScheduleId is not nullable
                    var slotsToDelete = await _context.ExamSlots
                        .Where(slot => SelectedSchedules.Contains(slot.ScheduleId))
                        .ToListAsync();

                    var slotIds = slotsToDelete.Select(s => s.Id).ToList();

                    // 3. Fetch all dependent Exam Seats assigned to these slots
                    var seatsToDelete = await _context.ExamSeats
                        .Where(seat => slotIds.Contains(seat.ExamSlotId))
                        .ToListAsync();

                    // 4. Safely remove from bottom up (Seats -> Slots -> Schedules)
                    _context.ExamSeats.RemoveRange(seatsToDelete);
                    _context.ExamSlots.RemoveRange(slotsToDelete);
                    _context.ExamSchedules.RemoveRange(schedulesToDelete);

                    await _context.SaveChangesAsync();
                    SuccessMessage = $"Successfully deleted {schedulesToDelete.Count} exam schedule(s) along with their slots and seats.";
                }
            }
            catch (Exception)
            {
                ErrorMessage = "A database error occurred while trying to delete the schedules. Please try again.";
            }

            return RedirectToPage();
        }
    }
}