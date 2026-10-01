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
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Bill;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class GenerateBillModel : PageModel
{
    private readonly DB _context;
        private readonly IConfiguration _configuration;

    public GenerateBillModel(DB context, IConfiguration configuration)
    {
        _context = context;
            _configuration = configuration;
    }

    [BindProperty]
    public BillInputModel Input { get; set; } = new();

    public int NextBillId { get; set; }

    public List<SelectListItem> DepartmentOptions { get; set; } = new();
    public List<SelectListItem> ProgrammeOptions { get; set; } = new();

    public class BillInputModel
    {
        public string TargetAudience { get; set; } = "Student";
        public string Title { get; set; }

        public int? DepartmentId { get; set; }
        public int? ProgrammeId { get; set; }
        public string? StudyLevel { get; set; }

        public string GenerationType { get; set; } = "Bulk";

        // Differentiate between the Radio Button and the actual Specific Type
        public string BillCategory { get; set; } = "School Fee";
        public string? SpecificBillType { get; set; }

        [RegularExpression(@"^\d{2}[a-zA-Z]{3}\d{5}$", ErrorMessage = "Invalid Student ID format. Expected format: 00AAA00000")]
        public string? StudentId { get; set; }

        public int? Year { get; set; }
        public int? Semester { get; set; }

        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public DateOnly DueDate { get; set; }
    }

    public async Task OnGetAsync()
    {
        int maxId = await _context.Bills.AnyAsync() ? await _context.Bills.MaxAsync(b => b.Id) : 0;
        NextBillId = maxId + 1;

        await LoadDropdownsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync();
            return Page();
        }

        if (Input.BillCategory == "Other" && string.IsNullOrEmpty(Input.SpecificBillType))
        {
            ModelState.AddModelError("Input.SpecificBillType", "Please select a specific fee type for the 'Other' category.");
            await LoadDropdownsAsync();
            return Page();
        }

        if (Input.Amount > 10000)
        {
            ModelState.AddModelError("Input.Amount", "Amount cannot exceed RM 10,000.");
            await LoadDropdownsAsync();
            return Page();
        }

        if (Input.DueDate < DateOnly.FromDateTime(DateTime.Now))
        {
            ModelState.AddModelError("Input.DueDate", "Due date cannot be in the past.");
            await LoadDropdownsAsync();
            return Page();
        }

        List<FYP.Models.Bill> billsToGenerate = new List<FYP.Models.Bill>();
        List<(FYP.Models.Bill Bill, string UserEmail)> notificationsToSend = new();

        int unpaidStatusId = 1;

        if (Input.GenerationType == "Individual")
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == Input.StudentId && u.UsersRoleId == 3);

            if (user == null)
            {
                ModelState.AddModelError("Input.StudentId", "Student ID not found in the database.");
                await LoadDropdownsAsync();
                return Page();
            }

            var newBill = CreateNewBillObject(user.Uid, unpaidStatusId);
            billsToGenerate.Add(newBill);
            notificationsToSend.Add((newBill, user.Email));
        }
        else if (Input.GenerationType == "Bulk")
        {
            var query = _context.Users.Where(u => u.UsersRoleId == 3);

            if (Input.BillCategory == "School Fee")
            {
                if (!Input.Year.HasValue || !Input.Semester.HasValue)
                {
                    ModelState.AddModelError(string.Empty, "Year and Semester are required when generating Bulk School Fees.");
                    await LoadDropdownsAsync();
                    return Page();
                }

                int currentYear = DateTime.Now.Year;
                int targetIntakeYear = currentYear - Input.Year.Value + 1;
                query = query.Where(u => u.IntakeYear == targetIntakeYear && u.Semester == Input.Semester.Value);
            }

            if (Input.DepartmentId.HasValue)
                query = query.Where(u => u.DepartmentId == Input.DepartmentId.Value);

            if (Input.ProgrammeId.HasValue)
                query = query.Where(u => u.ProgrammeId == Input.ProgrammeId.Value);

            if (!string.IsNullOrEmpty(Input.StudyLevel))
                query = query.Where(u => u.Programme.Level == Input.StudyLevel);

            var targetUsers = await query.ToListAsync();

            if (!targetUsers.Any())
            {
                string errorMsg = Input.BillCategory == "School Fee"
                    ? $"No students found matching this criteria for the selected Year and Semester."
                    : "No students found matching the selected Department, Programme, or Level.";

                ModelState.AddModelError(string.Empty, errorMsg);
                await LoadDropdownsAsync();
                return Page();
            }

            foreach (var user in targetUsers)
            {
                var newBill = CreateNewBillObject(user.Uid, unpaidStatusId);
                billsToGenerate.Add(newBill);
                notificationsToSend.Add((newBill, user.Email));
            }
        }

        if (billsToGenerate.Any())
        {
            _context.Bills.AddRange(billsToGenerate);
            await _context.SaveChangesAsync();

            string programmeName = "N/A";
            if (Input.ProgrammeId.HasValue)
            {
                var programme = await _context.Programmes.FindAsync(Input.ProgrammeId.Value);
                if (programme != null) programmeName = programme.Name;
            }

            string finalBillTypeDisplay = Input.BillCategory == "School Fee" ? "School Fee" : Input.SpecificBillType;

            foreach (var notification in notificationsToSend)
            {
                if (!string.IsNullOrEmpty(notification.UserEmail))
                {
                    string subject = $"New Bill Issued: {notification.Bill.Title}";
                    string body = $@"
                        <div style='font-family: Arial, sans-serif; color: #333;'>
                            <h3>New Bill Generated</h3>
                            <p>Dear Student,</p>
                            <p>A new bill has been generated for your account. Please find the details below:</p>
                            <ul>
                                <li><strong>Title:</strong> {notification.Bill.Title}</li>
                                <li><strong>Category:</strong> {finalBillTypeDisplay}</li>
                                <li><strong>Study Level:</strong> {Input.StudyLevel ?? "N/A"}</li>
                                <li><strong>Programme:</strong> {programmeName}</li>
                                <li><strong>Description:</strong> {notification.Bill.Description ?? "N/A"}</li>
                                <li><strong>Amount:</strong> RM {notification.Bill.Amount.ToString("0.00")}</li>
                                <li><strong>Due Date:</strong> {notification.Bill.DueDate.ToString("dd MMM yyyy")}</li>
                            </ul>
                            <p>Please ensure payment is made before the due date.</p>
                        </div>";

                    SendEmail(notification.UserEmail, subject, body);
                }
            }

            return RedirectToPage("/Admin/Dashboard");
        }

        await LoadDropdownsAsync();
        return Page();
    }

    private async Task LoadDropdownsAsync()
    {
        DepartmentOptions = await _context.Departments
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.DepartmentName })
            .ToListAsync();

        ProgrammeOptions = await _context.Programmes
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = $"{p.Code} - {p.Name}" })
            .ToListAsync();
    }

    private FYP.Models.Bill CreateNewBillObject(int targetUserId, int statusId)
    {
        return new FYP.Models.Bill
        {
            Title = Input.Title,
            Description = Input.Description,
            Amount = Input.Amount,
            // Assign actual text to the database model
            BillType = Input.BillCategory == "School Fee" ? "School Fee" : Input.SpecificBillType,
            DueDate = Input.DueDate,
            ReferenceNo = $"BILL-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}",
            StatusId = statusId,
            UserId = targetUserId
        };
    }

    private void SendEmail(string toEmail, string subject, string body)
    {
        try
        {
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? throw new InvalidOperationException("Configure EmailSettings:SenderEmail outside source control.");
            var appPassword = _configuration["EmailSettings:AppPassword"] ?? throw new InvalidOperationException("Configure EmailSettings:AppPassword outside source control.");

            var client = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, appPassword)
            };

            var mailMessage = new MailMessage(senderEmail, toEmail, subject, body) { IsBodyHtml = true };
            client.Send(mailMessage);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Email Failed to send: {ex.Message}");
        }
    }
}