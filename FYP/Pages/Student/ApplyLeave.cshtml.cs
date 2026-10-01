using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.Text.RegularExpressions;
using System.Security.Claims;
using System.IO;
using System.Linq;
using System;
using System.Threading.Tasks;
using FYP.Database;
using FYP.Models;

namespace FYP.Pages.Student
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
        public StudentLeave NewLeave { get; set; } = default!;

        [BindProperty]
        public IFormFile? UploadedDocument { get; set; }

        public User CurrentStudent { get; set; } = default!;
        public int LeavesTakenThisYear { get; set; }
        public bool IsQuotaExceeded { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadStudentDataAndQuotaAsync();
            return Page();
        }

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
            await LoadStudentDataAndQuotaAsync();

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
              
                else if (UploadedDocument.Length > 5242880)
                {
                    ModelState.AddModelError("UploadedDocument", "File size cannot exceed 5MB.");
                }
                
                else if (!IsValidFileSignature(UploadedDocument, extension))
                {
                    ModelState.AddModelError("UploadedDocument", "The uploaded file is corrupt or invalid.");
                }
                else if (ModelState.IsValid)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "leaves");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + fileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await UploadedDocument.CopyToAsync(fileStream);
                    }
                    NewLeave.Document = "/uploads/leaves/" + uniqueFileName;
                }
            }

            if (!ModelState.IsValid) return Page();

            NewLeave.LeaveTotal = NewLeave.EndDate.DayNumber - NewLeave.StartDate.DayNumber + 1;

            if (NewLeave.LeaveTotal <= 0)
            {
                ModelState.AddModelError("NewLeave.EndDate", "End date must be the same or after the start date.");
                return Page();
            }

            
            var hasOverlappingLeave = await _context.StudentLeaves
                .AnyAsync(l => l.StudentId == CurrentStudent.Uid
                            && l.StartDate <= NewLeave.EndDate
                            && l.EndDate >= NewLeave.StartDate
                            && l.StatusId != 2); // StatusId 2 = Rejected. If rejected, they can reapply.

            if (hasOverlappingLeave)
            {
                ModelState.AddModelError("NewLeave.StartDate", "You have already applied for leave during these dates.");
                return Page();
            }

            NewLeave.StudentId = CurrentStudent.Uid;
            NewLeave.StatusId = 3;

            _context.StudentLeaves.Add(NewLeave);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Leave request submitted successfully and is pending admin approval.";
            return RedirectToPage("/Student/ApplyLeave");
        }

        private async Task LoadStudentDataAndQuotaAsync()
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int studentId = string.IsNullOrEmpty(uidString) ? 1 : int.Parse(uidString);

            CurrentStudent = await _context.Users.FirstOrDefaultAsync(u => u.Uid == studentId);

            if (CurrentStudent != null)
            {
                DateTime accountCreation = DateTime.Now.AddYears(-1);
                DateTime currentAnniversary = new DateTime(DateTime.Now.Year, accountCreation.Month, accountCreation.Day);
                if (currentAnniversary > DateTime.Now)
                {
                    currentAnniversary = currentAnniversary.AddYears(-1);
                }

                DateOnly anniversaryDateOnly = DateOnly.FromDateTime(currentAnniversary);

                LeavesTakenThisYear = await _context.StudentLeaves
                    .Where(l => l.StudentId == CurrentStudent.Uid
                             && l.StartDate >= anniversaryDateOnly
                             && l.StatusId == 1)
                    .CountAsync();

                IsQuotaExceeded = LeavesTakenThisYear >= 12;
            }
        }
    }
}