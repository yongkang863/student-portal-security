using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.IO;

namespace FYP.Pages.Admin.Announcements;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class EditModel : PageModel
{
    private readonly DB _context;
        private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public EditModel(DB context, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _context = context;
            _configuration = configuration;
        _environment = environment;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    [BindProperty]
    public int AnnouncementId { get; set; }

    public List<SelectListItem> TargetOptions { get; } = new List<SelectListItem>
    {
        new SelectListItem { Value = "All", Text = "All Users" },
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
        public string TargetRole { get; set; }

        public IFormFile? UploadedFile { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var announcement = await _context.Announcements.FindAsync(id);
        if (announcement == null) return NotFound();

        AnnouncementId = announcement.Id;
        Input = new InputModel
        {
            Title = announcement.Title,
            Description = announcement.Description,
            TargetRole = announcement.TargetRole
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // 1. FILE UPLOAD SECURITY
        if (Input.UploadedFile != null && Input.UploadedFile.Length > 0)
        {
            var fileName = Path.GetFileName(Input.UploadedFile.FileName);

            // Block Double Extensions
            if (fileName.Count(c => c == '.') > 1)
            {
                ModelState.AddModelError("Input.UploadedFile", "Invalid file name. Double extensions are not allowed.");
                return Page();
            }

            var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg" };
            if (!allowedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
            {
                ModelState.AddModelError("Input.UploadedFile", "Only PDF, PNG, and JPG files are allowed.");
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
            // If you have an Attachment column:
            // announcement.AttachmentPath = "/uploads/announcements/" + uniqueFileName;
        }

        if (!ModelState.IsValid) return Page();

        var announcement = await _context.Announcements.FindAsync(AnnouncementId);
        if (announcement == null) return NotFound();

        // 2. Update Database (Store raw, encode on display/email)
        announcement.Title = Input.Title;
        announcement.TargetRole = Input.TargetRole;
        announcement.Description = Input.Description;
        announcement.PublishDate = DateTime.Now;

        await _context.SaveChangesAsync();

        // 3. SEND NOTIFICATION EMAIL 
        await SendBulkEmails(Input.TargetRole, Input.Title, Input.Description);

        return RedirectToPage("/Admin/Announcements/Index");
    }

    // --- EMAIL LOGIC ---
    private async Task SendBulkEmails(string targetRole, string title, string content)
    {
        var query = _context.Users.AsQueryable();
        if (targetRole == "Staff") query = query.Where(u => u.UsersRoleId == 1 || u.UsersRoleId == 2);
        else if (targetRole == "Student") query = query.Where(u => u.UsersRoleId == 3);

        var emails = await query.Where(u => !string.IsNullOrEmpty(u.Email)).Select(u => u.Email).ToListAsync();
        if (!emails.Any()) return;

        // XSS PREVENTION: Safely encode HTML tags before injecting into email
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
                    <span class='badge'>Announcement Updated</span>
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

        var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? throw new InvalidOperationException("Configure EmailSettings:SenderEmail outside source control.");
        var appPassword = _configuration["EmailSettings:AppPassword"] ?? throw new InvalidOperationException("Configure EmailSettings:AppPassword outside source control.");

        using var client = new SmtpClient("smtp.gmail.com", 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(senderEmail, appPassword)
        };

        foreach (var email in emails)
        {
            try
            {
                var mailMessage = new MailMessage(senderEmail, email, safeTitle, emailBody) { IsBodyHtml = true };
                await client.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send to {email}: {ex.Message}");
            }
        }
    }
}