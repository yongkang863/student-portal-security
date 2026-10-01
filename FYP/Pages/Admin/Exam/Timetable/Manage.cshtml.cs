using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FYP.Database;
using FYP.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FYP.Pages.Admin.Exam.Timetable
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
    public class ManageModel : PageModel
    {
        private readonly DB _context;

        public ManageModel(DB context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public ExamSchedule? Schedule { get; set; }
        public List<ExamSlot> Slots { get; set; } = new();
        public List<ExamSeat> AssignedSeats { get; set; } = new();

        public int TotalStudents { get; set; }

        public List<SelectListItem> CourseOptions { get; set; } = new();
        public List<SelectListItem> VenueOptions { get; set; } = new();
        public List<SelectListItem> TimeOptions { get; set; } = new();

        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }

        [BindProperty] public SlotInputModel Input { get; set; } = new();

        public class SlotInputModel
        {
            public int? SlotId { get; set; }

            [Required(ErrorMessage = "Course is required")]
            public int CourseId { get; set; }

            [Required(ErrorMessage = "Exam Date is required")]
            public DateOnly ExamDate { get; set; }

            [Required(ErrorMessage = "Start Time is required")]
            public TimeOnly StartTime { get; set; }

            [Required(ErrorMessage = "End Time is required")]
            public TimeOnly EndTime { get; set; }

            [Required(ErrorMessage = "Venue is required")]
            public int? VenueId { get; set; }

            [Required(ErrorMessage = "Capacity is required")]
            public int MaxCapacity { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadPageDataAsync();
            if (Schedule == null) return NotFound();
            return Page();
        }

        private async Task LoadPageDataAsync()
        {
            Schedule = await _context.ExamSchedules
                .Include(s => s.Programme)
                .FirstOrDefaultAsync(s => s.Id == Id);

            if (Schedule != null)
            {
                Slots = await _context.ExamSlots
                    .Include(s => s.Course)
                    .Include(s => s.Venue)
                    .Where(s => s.ScheduleId == Id)
                    .OrderBy(s => s.ExamDate).ThenBy(s => s.StartTime)
                    .ToListAsync();

                AssignedSeats = await _context.ExamSeats
                    .Include(s => s.Student)
                    .Include(s => s.ExamSlot)
                    .ThenInclude(es => es.Course)
                    .Where(s => s.ExamSlot!.ScheduleId == Id)
                    .OrderBy(s => s.ExamSlot!.ExamDate).ThenBy(s => s.SeatNumber)
                    .ToListAsync();

                TotalStudents = await _context.Users
                    .CountAsync(u => u.ProgrammeId == Schedule.ProgrammeId
                                  && u.SessionJoined == Schedule.SessionJoined
                                  && u.UsersRoleId == 3
                                  && u.StudyStatus == "Active");

                var validCourseIds = await _context.Set<ProgrammeStructure>()
                    .Where(ps => ps.ProgrammeId == Schedule.ProgrammeId && ps.SessionJoined == Schedule.SessionJoined)
                    .Select(ps => ps.CourseId)
                    .ToListAsync();

                CourseOptions = await _context.Set<Course>()
                    .Where(c => validCourseIds.Contains(c.Id))
                    .OrderBy(c => c.Code)
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = $"{c.Code} - {c.Title}" })
                    .ToListAsync();

                VenueOptions = await _context.Set<Venue>()
                    .Select(v => new SelectListItem { Value = v.Id.ToString(), Text = v.Name })
                    .ToListAsync();

                // RESTRICTED: Generate 30-minute intervals ONLY between 8:00 AM and 8:00 PM
                TimeOptions = new List<SelectListItem>();
                for (int h = 8; h <= 20; h++)
                {
                    TimeOptions.Add(new SelectListItem { Value = $"{h:D2}:00", Text = new TimeOnly(h, 0).ToString("hh:mm tt") });
                    if (h < 20) // Don't add 8:30 PM
                    {
                        TimeOptions.Add(new SelectListItem { Value = $"{h:D2}:30", Text = new TimeOnly(h, 30).ToString("hh:mm tt") });
                    }
                }
            }
        }

        // --- SLOT CRUD ---
        public async Task<IActionResult> OnPostAddSlotAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadPageDataAsync();
                ViewData["KeepModalOpen"] = true;
                ViewData["ModalMode"] = "add";
                return Page();
            }

            // 1. Validation: Prevent Past Dates
            var examDateTime = Input.ExamDate.ToDateTime(Input.StartTime);
            if (examDateTime < DateTime.Now)
            {
                ModelState.AddModelError("Input.ExamDate", "Exam Date and Time cannot be in the past.");
                ModelState.AddModelError("Input.StartTime", "Exam Date and Time cannot be in the past.");
                await LoadPageDataAsync();
                ViewData["KeepModalOpen"] = true;
                ViewData["ModalMode"] = "add";
                return Page();
            }

            // 2. Validation: Ensure Start Time is before End Time
            if (Input.StartTime >= Input.EndTime)
            {
                ModelState.AddModelError("Input.EndTime", "End Time must be after the Start Time.");
                await LoadPageDataAsync();
                ViewData["KeepModalOpen"] = true;
                ViewData["ModalMode"] = "add";
                return Page();
            }

            // 3. Validation: Ensure Time is within 8AM - 8PM boundary
            if (Input.StartTime < new TimeOnly(8, 0) || Input.EndTime > new TimeOnly(20, 0))
            {
                ModelState.AddModelError("Input.StartTime", "Exam times must be between 8:00 AM and 8:00 PM.");
                await LoadPageDataAsync();
                ViewData["KeepModalOpen"] = true;
                ViewData["ModalMode"] = "add";
                return Page();
            }

            // 4. Validation: Prevent Cohort Time Clash (Student cannot take two exams at once)
            bool isCohortClashing = await _context.ExamSlots.AnyAsync(s =>
                s.ScheduleId == Id &&
                s.ExamDate == Input.ExamDate &&
                s.StartTime < Input.EndTime &&
                s.EndTime > Input.StartTime);

            if (isCohortClashing)
            {
                ModelState.AddModelError("Input.ExamDate", "This student cohort already has an exam scheduled during this time.");
                await LoadPageDataAsync();
                ViewData["KeepModalOpen"] = true;
                ViewData["ModalMode"] = "add";
                return Page();
            }

            // 5. Validation: Prevent Venue Double-Booking (Time Clash)
            if (Input.VenueId.HasValue)
            {
                bool isVenueClashing = await _context.ExamSlots.AnyAsync(s =>
                    s.VenueId == Input.VenueId &&
                    s.ExamDate == Input.ExamDate &&
                    s.StartTime < Input.EndTime &&
                    s.EndTime > Input.StartTime);

                if (isVenueClashing)
                {
                    ModelState.AddModelError("Input.VenueId", "This venue is already booked for another exam during this time.");
                    await LoadPageDataAsync();
                    ViewData["KeepModalOpen"] = true;
                    ViewData["ModalMode"] = "add";
                    return Page();
                }
            }

            try
            {
                var newSlot = new ExamSlot
                {
                    ScheduleId = Id,
                    CourseId = Input.CourseId,
                    ExamDate = Input.ExamDate,
                    StartTime = Input.StartTime,
                    EndTime = Input.EndTime,
                    VenueId = Input.VenueId,
                    MaxCapacity = Input.MaxCapacity,
                    SeatAvailable = Input.MaxCapacity
                };

                _context.ExamSlots.Add(newSlot);
                await _context.SaveChangesAsync();
                SuccessMessage = "Exam slot added successfully.";
            }
            catch (Exception)
            {
                ErrorMessage = $"Database Error: Could not add the exam slot.";
            }

            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostEditSlotAsync()
        {
            if (!ModelState.IsValid || Input.SlotId == null)
            {
                await LoadPageDataAsync();
                ViewData["KeepModalOpen"] = true;
                ViewData["ModalMode"] = "edit";
                return Page();
            }

            // 1. Validation: Prevent Past Dates
            var examDateTime = Input.ExamDate.ToDateTime(Input.StartTime);
            if (examDateTime < DateTime.Now)
            {
                ModelState.AddModelError("Input.ExamDate", "Exam Date and Time cannot be in the past.");
                ModelState.AddModelError("Input.StartTime", "Exam Date and Time cannot be in the past.");
                await LoadPageDataAsync();
                ViewData["KeepModalOpen"] = true;
                ViewData["ModalMode"] = "edit";
                return Page();
            }

            // 2. Validation: Ensure Start Time is before End Time
            if (Input.StartTime >= Input.EndTime)
            {
                ModelState.AddModelError("Input.EndTime", "End Time must be after the Start Time.");
                await LoadPageDataAsync();
                ViewData["KeepModalOpen"] = true;
                ViewData["ModalMode"] = "edit";
                return Page();
            }

            // 3. Validation: Ensure Time is within 8AM - 8PM boundary
            if (Input.StartTime < new TimeOnly(8, 0) || Input.EndTime > new TimeOnly(20, 0))
            {
                ModelState.AddModelError("Input.StartTime", "Exam times must be between 8:00 AM and 8:00 PM.");
                await LoadPageDataAsync();
                ViewData["KeepModalOpen"] = true;
                ViewData["ModalMode"] = "edit";
                return Page();
            }

            // 4. Validation: Prevent Cohort Time Clash (Student cannot take two exams at once)
            bool isCohortClashing = await _context.ExamSlots.AnyAsync(s =>
                s.Id != Input.SlotId && // Exclude itself
                s.ScheduleId == Id &&
                s.ExamDate == Input.ExamDate &&
                s.StartTime < Input.EndTime &&
                s.EndTime > Input.StartTime);

            if (isCohortClashing)
            {
                ModelState.AddModelError("Input.ExamDate", "This student cohort already has an exam scheduled during this time.");
                await LoadPageDataAsync();
                ViewData["KeepModalOpen"] = true;
                ViewData["ModalMode"] = "edit";
                return Page();
            }

            // 5. Validation: Prevent Venue Double-Booking (Time Clash, excluding itself)
            if (Input.VenueId.HasValue)
            {
                bool isVenueClashing = await _context.ExamSlots.AnyAsync(s =>
                    s.Id != Input.SlotId && // Exclude itself
                    s.VenueId == Input.VenueId &&
                    s.ExamDate == Input.ExamDate &&
                    s.StartTime < Input.EndTime &&
                    s.EndTime > Input.StartTime);

                if (isVenueClashing)
                {
                    ModelState.AddModelError("Input.VenueId", "This venue is already booked for another exam during this time.");
                    await LoadPageDataAsync();
                    ViewData["KeepModalOpen"] = true;
                    ViewData["ModalMode"] = "edit";
                    return Page();
                }
            }

            try
            {
                var slot = await _context.ExamSlots.FindAsync(Input.SlotId);
                if (slot == null) return NotFound();

                slot.CourseId = Input.CourseId;
                slot.ExamDate = Input.ExamDate;
                slot.StartTime = Input.StartTime;
                slot.EndTime = Input.EndTime;
                slot.VenueId = Input.VenueId;

                int capacityDifference = Input.MaxCapacity - (slot.MaxCapacity ?? 0);
                slot.MaxCapacity = Input.MaxCapacity;
                slot.SeatAvailable = (slot.SeatAvailable ?? 0) + capacityDifference;

                await _context.SaveChangesAsync();
                SuccessMessage = "Exam slot updated successfully.";
            }
            catch (Exception)
            {
                ErrorMessage = "Database Error: Could not update the exam slot.";
            }

            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostDeleteSlotAsync(int slotId)
        {
            try
            {
                var slot = await _context.ExamSlots.FindAsync(slotId);
                if (slot != null)
                {
                    var seats = await _context.ExamSeats.Where(s => s.ExamSlotId == slotId).ToListAsync();
                    _context.ExamSeats.RemoveRange(seats);
                    _context.ExamSlots.Remove(slot);
                    await _context.SaveChangesAsync();
                    SuccessMessage = "Exam slot deleted successfully.";
                }
            }
            catch (Exception)
            {
                ErrorMessage = "Database Error: Could not delete the exam slot.";
            }

            return RedirectToPage(new { id = Id });
        }

        // --- AUTO ASSIGN SEATS ---
        public async Task<IActionResult> OnPostAutoAssignSeatsAsync()
        {
            var schedule = await _context.ExamSchedules.FindAsync(Id);
            if (schedule == null) return NotFound();

            var students = await _context.Users
                .Where(u => u.ProgrammeId == schedule.ProgrammeId && u.SessionJoined == schedule.SessionJoined && u.UsersRoleId == 3 && u.StudyStatus == "Active")
                .OrderBy(u => u.FullName).ToListAsync();

            if (!students.Any())
            {
                ErrorMessage = "No active students found for this Programme and Session.";
                return RedirectToPage(new { id = Id });
            }

            var slots = await _context.ExamSlots.Where(s => s.ScheduleId == Id).ToListAsync();
            if (!slots.Any())
            {
                ErrorMessage = "Please add at least one Exam Slot before assigning seats.";
                return RedirectToPage(new { id = Id });
            }

            var existingSeats = await _context.ExamSeats.Where(s => slots.Select(sl => sl.Id).Contains(s.ExamSlotId)).ToListAsync();
            _context.ExamSeats.RemoveRange(existingSeats);

            foreach (var slot in slots)
            {
                int seatCounter = 1;
                foreach (var student in students)
                {
                    _context.ExamSeats.Add(new ExamSeat { ExamSlotId = slot.Id, StudentId = student.Uid, SeatNumber = $"S-{seatCounter:D3}" });
                    seatCounter++;
                }
                if (slot.MaxCapacity.HasValue) slot.SeatAvailable = slot.MaxCapacity.Value - students.Count;
            }

            schedule.LastUpdated = DateTime.Now;
            await _context.SaveChangesAsync();
            SuccessMessage = $"Successfully auto-assigned seats for {students.Count} students across {slots.Count} slots.";
            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostPublishScheduleAsync()
        {
            var schedule = await _context.ExamSchedules.FindAsync(Id);
            if (schedule != null)
            {
                schedule.Status = "Published"; schedule.LastUpdated = DateTime.Now;
                await _context.SaveChangesAsync();
                SuccessMessage = "Schedule successfully published to students.";
            }
            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostUnpublishScheduleAsync()
        {
            var schedule = await _context.ExamSchedules.FindAsync(Id);
            if (schedule != null)
            {
                schedule.Status = "Draft"; schedule.LastUpdated = DateTime.Now;
                await _context.SaveChangesAsync();
                SuccessMessage = "Schedule successfully unpublished. It is no longer visible to students.";
            }
            return RedirectToPage(new { id = Id });
        }
    }
}