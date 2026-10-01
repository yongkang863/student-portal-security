using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using FYP.Database;
using FYP.Models;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Pages.Staff
{
    public class ApplyLeaveModel : PageModel
    {
        private readonly DB _context;
        private readonly IWebHostEnvironment _environment;

        public ApplyLeaveModel(DB context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [BindProperty]
        public StaffLeave NewLeave { get; set; } = default!;

        [BindProperty]
        public IFormFile? UploadedDocument { get; set; }

        public User CurrentStaff { get; set; } = default!;
        public int LeavesTakenThisYear { get; set; }
        public bool IsQuotaExceeded { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadStaffDataAndQuotaAsync();
            return Page();
        }

        // SECURITY FIX: Magic number validation method
        private bool IsValidFileSignature(IFormFile file, string extension)
        {
            if (file == null || file.Length == 0) return false;
            using var reader = new BinaryReader(file.OpenReadStream());
            var headerBytes = reader.ReadBytes(8);

            if (extension == ".pdf") return headerBytes.Take(4).SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46 });
            if (extension == ".png") return headerBytes.Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            if (extension == ".jpg" || extension == ".jpeg") return headerBytes.Take(3).SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF });

            return false;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadStaffDataAndQuotaAsync();

            if (IsQuotaExceeded)
            {
                ModelState.AddModelError("", "You have exceeded your annual quota of 12 leave requests.");
                return Page();
            }

            var safeInputRegex = new Regex(@"^[\p{L}\p{N}\s\-_.,()!?'""&]+$");
            if (!string.IsNullOrWhiteSpace(NewLeave.Reason) && !safeInputRegex.IsMatch(NewLeave.Reason))
            {
                ModelState.AddModelError("NewLeave.Reason", "Invalid characters detected. Emojis and special symbols are not allowed.");
            }

            // Document Upload & Validation
            if (UploadedDocument != null && UploadedDocument.Length > 0)
            {
                var fileName = Path.GetFileName(UploadedDocument.FileName);
                var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg" };
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                if (fileName.Count(c => c == '.') > 1)
                {
                    ModelState.AddModelError("UploadedDocument", "Invalid file name. Double extensions are not allowed.");
                }
                else if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("UploadedDocument", "Only PDF, PNG, and JPG files are allowed.");
                }
                // SECURITY FIX: Added 5MB file size limit
                else if (UploadedDocument.Length > 5242880)
                {
                    ModelState.AddModelError("UploadedDocument", "File size cannot exceed 5MB.");
                }
                // SECURITY FIX: Apply Magic Number checking
                else if (!IsValidFileSignature(UploadedDocument, extension))
                {
                    ModelState.AddModelError("UploadedDocument", "The uploaded file is corrupt or invalid.");
                }
                else if (ModelState.IsValid)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "staff_leaves");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + fileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await UploadedDocument.CopyToAsync(fileStream);
                    }
                    NewLeave.Document = "/uploads/staff_leaves/" + uniqueFileName;
                }
            }

            if (!ModelState.IsValid) return Page();

            NewLeave.LeaveTotal = NewLeave.EndDate.DayNumber - NewLeave.StartDate.DayNumber + 1;
            if (NewLeave.LeaveTotal <= 0)
            {
                ModelState.AddModelError("NewLeave.EndDate", "End date must be the same or after the start date.");
                return Page();
            }

            // NEW FIX: Prevent Same Day / Overlapping Leaves for Staff
            var hasOverlappingLeave = await _context.StaffLeaves
                .AnyAsync(l => l.StaffId == CurrentStaff.Uid
                            && l.StartDate <= NewLeave.EndDate
                            && l.EndDate >= NewLeave.StartDate
                            && l.StatusId != 2); // StatusId 2 = Rejected. Adjust if needed.

            if (hasOverlappingLeave)
            {
                ModelState.AddModelError("NewLeave.StartDate", "You have already applied for leave during these dates.");
                return Page();
            }

            NewLeave.StaffId = CurrentStaff.Uid;
            NewLeave.StatusId = 3;

            _context.StaffLeaves.Add(NewLeave);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Leave request submitted successfully and is pending admin approval.";
            return RedirectToPage("/Staff/ApplyLeave");
        }

        private async Task LoadStaffDataAndQuotaAsync()
        {
            var uidClaim = User.FindFirst("Uid")?.Value;
            int staffId = !string.IsNullOrEmpty(uidClaim) ? int.Parse(uidClaim) : 0;

            CurrentStaff = await _context.Users.FirstOrDefaultAsync(u => u.Uid == staffId);

            if (CurrentStaff != null)
            {
                DateTime accountCreation = DateTime.Now.AddYears(-1);
                DateTime currentAnniversary = new DateTime(DateTime.Now.Year, accountCreation.Month, accountCreation.Day);
                if (currentAnniversary > DateTime.Now) currentAnniversary = currentAnniversary.AddYears(-1);

                DateOnly anniversaryDateOnly = DateOnly.FromDateTime(currentAnniversary);

                LeavesTakenThisYear = await _context.StaffLeaves
                    .Where(l => l.StaffId == CurrentStaff.Uid
                             && l.StartDate >= anniversaryDateOnly
                             && l.StatusId == 1)
                    .CountAsync();

                IsQuotaExceeded = LeavesTakenThisYear >= 12;
            }
        }
    }
}