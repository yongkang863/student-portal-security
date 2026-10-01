using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.RegularExpressions;

namespace FYP.Pages.Admin.Facilities
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
    public class CreateModel : PageModel
    {
        private readonly DB _context;
        private readonly IWebHostEnvironment _environment;

        public CreateModel(DB context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [BindProperty]
        public FYP.Models.Facility NewFacility { get; set; } = default!;

        [BindProperty]
        public List<IFormFile> UploadedPhotos { get; set; } = new();

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var safeInputRegex = new Regex(@"^[\p{L}\p{N}\s\-_.,()!?'""&]+$");

            if (!string.IsNullOrWhiteSpace(NewFacility.Name) && !safeInputRegex.IsMatch(NewFacility.Name))
            {
                ModelState.AddModelError("NewFacility.Name", "Facility Name contains emojis or invalid characters. Only letters, numbers, and basic punctuation are allowed.");
            }

            if (!string.IsNullOrWhiteSpace(NewFacility.Remark) && !safeInputRegex.IsMatch(NewFacility.Remark))
            {
                ModelState.AddModelError("NewFacility.Remark", "Remark contains emojis or invalid characters.");
            }

            if (!string.IsNullOrWhiteSpace(NewFacility.FacilityType) && !safeInputRegex.IsMatch(NewFacility.FacilityType))
            {
                ModelState.AddModelError("NewFacility.FacilityType", "Invalid Facility Type selected.");
            }

            // SECURITY FEATURE: Validate photos BEFORE processing saving
            if (UploadedPhotos != null && UploadedPhotos.Count > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                foreach (var file in UploadedPhotos)
                {
                    if (file.Length > 0)
                    {
                        var fileName = Path.GetFileName(file.FileName);

                        // Block Double Extensions
                        if (fileName.Count(c => c == '.') > 1)
                        {
                            // UPDATED: Custom error message
                            ModelState.AddModelError("UploadedPhotos", "Incorrect File Type, Please Follow The Requirement.");
                        }
                        else
                        {
                            var extension = Path.GetExtension(fileName).ToLowerInvariant();
                            if (!allowedExtensions.Contains(extension))
                            {
                                // UPDATED: Custom error message
                                ModelState.AddModelError("UploadedPhotos", "Incorrect File Type, Please Follow The Requirement.");
                            }
                        }
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Facilities.Add(NewFacility);
            await _context.SaveChangesAsync();

            if (UploadedPhotos != null && UploadedPhotos.Count > 0)
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "facilities");
                Directory.CreateDirectory(uploadsFolder);

                foreach (var file in UploadedPhotos)
                {
                    if (file.Length > 0)
                    {
                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }

                        var photoRecord = new FacilityPhoto
                        {
                            FacilityId = NewFacility.Id,
                            FilePath = "/uploads/facilities/" + uniqueFileName
                        };
                        _context.FacilityPhotos.Add(photoRecord);
                    }
                }
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Facility created successfully!";
            return RedirectToPage("/Admin/Facility/Index");
        }
    }
}