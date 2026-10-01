using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Announcements;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class CreateModel : PageModel
{
    private readonly DB _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(DB context, IWebHostEnvironment environment, IConfiguration configuration, ILogger<CreateModel> logger)
    {
        _context = context;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public bool IsPreview { get; set; } = false;
    public string SanitizedDescription { get; set; }

    public List<SelectListItem> TargetOptions { get; } = new List<SelectListItem>
    {
        new SelectListItem { Value = "All", Text = "All Users (Staff & Students)" },
        new SelectListItem { Value = "Staff", Text = "Staff Only" },
        new SelectListItem { Value = "Student", Text = "Students Only" }
    };

    public class InputModel
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Target Audience")]
        public string TargetRole { get; set; }

        public IFormFile? UploadedFile { get; set; }
        public string? SavedFilePath { get; set; }
    }

    public void OnGet() { }

    // SECURITY FIX: Magic Number checking for PDFs and Images
    private bool IsValidFileSignature(IFormFile file, string extension)
    {
        if (file == null || file.Length == 0) return false;

        using var reader = new BinaryReader(file.OpenReadStream());
        var headerBytes = reader.ReadBytes(8);

        if (extension == ".pdf")
            return headerBytes.Take(4).SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        if (extension == ".png")
            return headerBytes.Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        if (extension == ".jpg" || extension == ".jpeg")
            return headerBytes.Take(3).SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF });

        return false;
    }

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        if (Input.UploadedFile != null && Input.UploadedFile.Length > 0)
        {
            var fileName = Path.GetFileName(Input.UploadedFile.FileName);

            if (fileName.Count(c => c == '.') > 1)
            {
                ModelState.AddModelError("Input.UploadedFile", "Invalid file name. Double extensions are not allowed.");
                return Page();
            }

            var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg" };
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("Input.UploadedFile", "Only PDF, PNG, JPG, and JPEG files are allowed.");
                return Page();
            }

            // SECURITY FIX: File Size Validation (Max 5MB)
            if (Input.UploadedFile.Length > 5242880)
            {
                ModelState.AddModelError("Input.UploadedFile", "File size cannot exceed 5MB.");
                return Page();
            }

            // SECURITY FIX: Enforce magic numbers
            if (!IsValidFileSignature(Input.UploadedFile, extension))
            {
                ModelState.AddModelError("Input.UploadedFile", "The uploaded file is corrupt or invalid.");
                return Page();
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "announcements");
            Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + fileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await Input.UploadedFile.CopyToAsync(fileStream);
            }
            Input.SavedFilePath = "/uploads/announcements/" + uniqueFileName;
        }

        if (!ModelState.IsValid) return Page();

        SanitizedDescription = WebUtility.HtmlEncode(Input.Description);
        IsPreview = true;

        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync()
    {
        var adminIdClaim = User.FindFirst("UserId")?.Value;
        int? adminId = int.TryParse(adminIdClaim, out int parsedId) ? parsedId : null;

        var announcement = new Announcement
        {
            Title = Input.Title,
            Description = Input.Description,
            Category = "General",
            TargetRole = Input.TargetRole,
            PublishDate = DateTime.Now,
            AdminId = adminId,
            FilePath = Input.SavedFilePath
        };

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync();

        // FIX: Pass the SavedFilePath to the email method so it can be attached
        await SendBulkEmails(Input.TargetRole, Input.Title, Input.Description, Input.SavedFilePath);

        return RedirectToPage("/Admin/Announcements/Index");
    }

    private async Task SendBulkEmails(string targetRole, string title, string content, string? savedFilePath)
    {
        var query = _context.Users.AsQueryable();
        if (targetRole == "Staff") query = query.Where(u => u.UsersRoleId == 1 || u.UsersRoleId == 2);
        else if (targetRole == "Student") query = query.Where(u => u.UsersRoleId == 3);

        var emails = await query.Where(u => !string.IsNullOrEmpty(u.Email)).Select(u => u.Email).ToListAsync();
        if (!emails.Any()) return;

        string safeContent = WebUtility.HtmlEncode(content).Replace("\n", "<br/>");
        string safeTitle = WebUtility.HtmlEncode(title);

        string emailBody = $@"
        <html>
        <head>
            <style>
                body {{ font-family: Arial, sans-serif; background-color: #f8fafc; margin: 0; padding: 0; }}
                .container {{ max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }}
                .header {{ background-color: #1e3a8a; color: #ffffff; padding: 20px; text-align: center; }}
                .content {{ padding: 30px; color: #000000; line-height: 1.6; }}
                .content h2 {{ color: #1e3a8a; margin-top: 0; }}
                .badge {{ background-color: #dbeafe; color: #1e40af; padding: 5px 10px; border-radius: 4px; font-size: 12px; font-weight: bold; text-transform: uppercase; }}
                .footer {{ background-color: #f1f5f9; padding: 20px; text-align: center; font-size: 12px; color: #000000; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <h1>Ronnie University</h1>
                </div>
                <div class='content'>
                    <span class='badge'>Announcement</span>
                    <h2>{safeTitle}</h2>
                    <hr style='border: 0; border-top: 1px solid #e2e8f0; margin: 20px 0;'>
                    <div style='font-size: 16px;'>{safeContent}</div>
                </div>
                <div class='footer'>
                    <p>&copy; {DateTime.Now.Year} Ronnie University Administration.</p>
                    <p>This is an automated message. Please do not reply.</p>
                </div>
            </div>
        </body>
        </html>";

        // SECURITY FIX: Removed hardcoded secrets, pull from IConfiguration
        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var appPassword = _configuration["EmailSettings:AppPassword"];

        using var client = new SmtpClient("smtp.gmail.com", 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(senderEmail, appPassword)
        };

        foreach (var email in emails)
        {
            try
            {
                // FIX: Use 'using' to safely dispose of the MailMessage and any file locks
                using var mailMessage = new MailMessage(senderEmail, email, safeTitle, emailBody) { IsBodyHtml = true };

                // FIX: Attach the file if one was uploaded
                if (!string.IsNullOrEmpty(savedFilePath))
                {
                    // Convert the web path ("/uploads/...") to a physical server path ("C:\...\wwwroot\uploads\...")
                    string relativePath = savedFilePath.TrimStart('/');
                    string physicalPath = Path.Combine(_environment.WebRootPath, relativePath);

                    if (System.IO.File.Exists(physicalPath))
                    {
                        mailMessage.Attachments.Add(new Attachment(physicalPath));
                    }
                }

                await client.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send announcement email to {Email}", email);
            }
        }
    }
}