using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace FYP.Pages.Student
{
    [Authorize(Policy = "StudentOnly", AuthenticationSchemes = "StudentCookie")]
    public class ProfileModel : PageModel
    {
        private readonly DB _context;

        public ProfileModel(DB context)
        {
            _context = context;
        }

        public User CurrentUser { get; set; }
        public string? ProfilePhotoBase64 { get; set; }
        public string? AdvisorPhotoBase64 { get; set; }

        // --- NEW: BindProperty for File Upload ---
        [BindProperty]
        public IFormFile? UploadedPhoto { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // 1. Get the Logged in user's UID
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(uidString) || !int.TryParse(uidString, out int uid))
            {
                return RedirectToPage("/Student/Login");
            }

            // 2. Fetch user with ALL related data, including Bank and Advisor
            CurrentUser = await _context.Users
                .Include(u => u.Programme)
                .Include(u => u.Department)
                .Include(u => u.Role)
                .Include(u => u.Bank)
                .Include(u => u.Advisory)
                    .ThenInclude(a => a.Staff)
                .FirstOrDefaultAsync(u => u.Uid == uid);

            if (CurrentUser == null)
            {
                return NotFound("Student record not found.");
            }

            if (!string.IsNullOrEmpty(CurrentUser.BankAccNo))
            {
                // If the data looks like a BCrypt hash (starts with $2a$), we can't show it.
                // If it's the new AES format (Base64), this will work.
                if (!CurrentUser.BankAccNo.StartsWith("$2a$"))
                {
                    CurrentUser.BankAccNo = FYP.Helpers.SecurityHelper.Decrypt(CurrentUser.BankAccNo);
                }
                else
                {
                    // Optional: Handle legacy data that is still BCrypt hashed
                    CurrentUser.BankAccNo = "Reset Required";
                }
            }

            // 3. Convert Student Photo
            if (CurrentUser.Photo != null && CurrentUser.Photo.Length > 0)
            {
                string base64 = Convert.ToBase64String(CurrentUser.Photo);
                ProfilePhotoBase64 = $"data:image/jpg;base64,{base64}";
            }

            // 4. Convert Advisor Photo (if exists)
            if (CurrentUser.Advisory?.Staff?.Photo != null && CurrentUser.Advisory.Staff.Photo.Length > 0)
            {
                string advisorBase64 = Convert.ToBase64String(CurrentUser.Advisory.Staff.Photo);
                AdvisorPhotoBase64 = $"data:image/jpg;base64,{advisorBase64}";
            }

            return Page();
        }

        // --- SECURITY ADDITION: Magic Number Validation ---
        private bool IsValidImageSignature(IFormFile file)
        {
            if (file == null || file.Length == 0) return false;

            using var reader = new BinaryReader(file.OpenReadStream());
            var signatures = new List<byte[]>
            {
                new byte[] { 0xFF, 0xD8, 0xFF }, // JPEG/JPG Magic Numbers
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } // PNG Magic Numbers
            };

            var headerBytes = reader.ReadBytes(8);
            return signatures.Any(signature => headerBytes.Take(signature.Length).SequenceEqual(signature));
        }

        // --- NEW: Handler for Uploading Photo ---
        public async Task<IActionResult> OnPostUploadPhotoAsync()
        {
            // A. Get Logged in User ID
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidString) || !int.TryParse(uidString, out int uid))
            {
                return RedirectToPage("/Student/Login");
            }

            // B. Validate the File Presence
            if (UploadedPhoto == null || UploadedPhoto.Length == 0)
            {
                // Retrieve user data again to redisplay page with error
                await OnGetAsync();
                ModelState.AddModelError("UploadedPhoto", "Please select a file.");
                return Page();
            }

            // C. Validate File Size (Limit: 2MB)
            const long maxFileSize = 2 * 1024 * 1024;
            if (UploadedPhoto.Length > maxFileSize)
            {
                await OnGetAsync();
                ModelState.AddModelError("UploadedPhoto", "File size cannot exceed 2MB.");
                return Page();
            }

            // D. Validate File Extension
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(UploadedPhoto.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                await OnGetAsync();
                ModelState.AddModelError("UploadedPhoto", "Only .jpg, .jpeg, and .png files are allowed.");
                return Page();
            }

            // --- SECURITY FIX: Validate File Signature ---
            if (!IsValidImageSignature(UploadedPhoto))
            {
                await OnGetAsync();
                ModelState.AddModelError("UploadedPhoto", "The uploaded file is corrupt or is not a valid image file.");
                return Page();
            }

            // E. Fetch User (Optimized: Only fetch what we need to update)
            var userToUpdate = await _context.Users.FindAsync(uid);
            if (userToUpdate == null) return NotFound();

            // F. Convert and Save
            using (var memoryStream = new MemoryStream())
            {
                await UploadedPhoto.CopyToAsync(memoryStream);
                userToUpdate.Photo = memoryStream.ToArray();
            }

            _context.Users.Update(userToUpdate);
            await _context.SaveChangesAsync();

            // G. Refresh page to show new image
            return RedirectToPage();
        }
    }
}