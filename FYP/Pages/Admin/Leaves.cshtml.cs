using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;

namespace FYP.Pages.Admin
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
    public class LeavesModel : PageModel
    {
        private readonly DB _context;
        private readonly IConfiguration _configuration;

        public LeavesModel(DB context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public IList<StudentLeave> PendingStudentLeaves { get; set; } = default!;
        public IList<StaffLeave> PendingStaffLeaves { get; set; } = default!;

        public async Task OnGetAsync()
        {
            PendingStudentLeaves = await _context.StudentLeaves
                .Include(l => l.Student)
                .Where(l => l.StatusId == 3)
                .OrderBy(l => l.StartDate)
                .ToListAsync();

            PendingStaffLeaves = await _context.StaffLeaves
                .Include(l => l.Staff)
                .Where(l => l.StatusId == 3)
                .OrderBy(l => l.StartDate)
                .ToListAsync();
        }

        // ==========================================
        // STUDENT LEAVE HANDLERS
        // ==========================================
        public async Task<IActionResult> OnPostApproveStudentAsync(int id)
        {
            return await ProcessStudentLeaveAsync(id, 1, "Approved");
        }

        public async Task<IActionResult> OnPostRejectStudentAsync(int id, string remark)
        {
            return await ProcessStudentLeaveAsync(id, 2, "Rejected", remark);
        }

        private async Task<IActionResult> ProcessStudentLeaveAsync(int leaveId, int newStatusId, string resultText, string remark = null)
        {
            var leaveRequest = await _context.StudentLeaves
                .Include(l => l.Student)
                .FirstOrDefaultAsync(l => l.Id == leaveId);

            if (leaveRequest == null) return NotFound();

            leaveRequest.StatusId = newStatusId;
            SetAdminId(leaveRequest);
            await _context.SaveChangesAsync();

            if (leaveRequest.Student != null && !string.IsNullOrEmpty(leaveRequest.Student.Email))
            {
                string dateRange = $"{leaveRequest.StartDate.ToString("dd MMM yyyy")} to {leaveRequest.EndDate.ToString("dd MMM yyyy")}";
                SendLeaveEmailNotification(
                    leaveRequest.Student.Email,
                    leaveRequest.Student.FullName,
                    leaveRequest.LeaveType,
                    dateRange,
                    resultText,
                    remark
                );
            }

            TempData["SuccessMessage"] = $"Student leave request has been {resultText.ToLower()} successfully.";
            return RedirectToPage();
        }

        // ==========================================
        // STAFF LEAVE HANDLERS
        // ==========================================
        public async Task<IActionResult> OnPostApproveStaffAsync(int id)
        {
            return await ProcessStaffLeaveAsync(id, 1, "Approved");
        }

        public async Task<IActionResult> OnPostRejectStaffAsync(int id, string remark)
        {
            return await ProcessStaffLeaveAsync(id, 2, "Rejected", remark);
        }

        private async Task<IActionResult> ProcessStaffLeaveAsync(int leaveId, int newStatusId, string resultText, string remark = null)
        {
            var leaveRequest = await _context.StaffLeaves
                .Include(l => l.Staff)
                .FirstOrDefaultAsync(l => l.Id == leaveId);

            if (leaveRequest == null) return NotFound();

            leaveRequest.StatusId = newStatusId;
            SetAdminId(leaveRequest);
            await _context.SaveChangesAsync();

            if (leaveRequest.Staff != null && !string.IsNullOrEmpty(leaveRequest.Staff.Email))
            {
                string dateRange = $"{leaveRequest.StartDate.ToString("dd MMM yyyy")} to {leaveRequest.EndDate.ToString("dd MMM yyyy")}";
                SendLeaveEmailNotification(
                    leaveRequest.Staff.Email,
                    leaveRequest.Staff.FullName,
                    leaveRequest.LeaveType,
                    dateRange,
                    resultText,
                    remark
                );
            }

            TempData["SuccessMessage"] = $"Staff leave request has been {resultText.ToLower()} successfully.";
            return RedirectToPage();
        }

        // ==========================================
        // HELPER METHOD (Extracts Admin ID)
        // ==========================================
        private void SetAdminId(dynamic request)
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("Uid")?.Value;
            if (int.TryParse(uidString, out int adminId))
            {
                request.AdminId = adminId;
            }
        }

        // ==========================================
        // EMAIL SENDER METHOD
        // ==========================================
        private void SendLeaveEmailNotification(string toEmail, string userName, string leaveType, string dates, string result, string remark = null)
        {
            try
            {
                string fromEmail = _configuration["EmailSettings:SenderEmail"] ?? throw new InvalidOperationException("Configure EmailSettings:SenderEmail outside source control.");
                string appPassword = _configuration["EmailSettings:AppPassword"] ?? throw new InvalidOperationException("Configure EmailSettings:AppPassword outside source control.");

                var message = new MailMessage();
                message.From = new MailAddress(fromEmail, "Ronnie University Administration");
                message.To.Add(new MailAddress(toEmail));
                message.Subject = $"Leave Request {result}";

                string remarkHtml = string.IsNullOrEmpty(remark)
                    ? ""
                    : $"<li style='margin-bottom: 10px; color: #dc2626;'><strong>Admin Remark:</strong> {remark}</li>";

                message.Body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 10px;'>
                        <h2 style='color: #1e293b; border-bottom: 2px solid #e2e8f0; padding-bottom: 10px;'>Leave Request Update</h2>
                        <p style='color: #475569; font-size: 16px;'>Dear <strong>{userName}</strong>,</p>
                        <p style='color: #475569; font-size: 16px;'>Your leave request has been processed. Here are the details:</p>
                        
                        <ul style='list-style: none; padding: 0; margin: 20px 0; background-color: #f8fafc; padding: 15px; border-radius: 8px;'>
                            <li style='margin-bottom: 10px; color: #334155;'><strong>Leave Type:</strong> {leaveType}</li>
                            <li style='margin-bottom: 10px; color: #334155;'><strong>Dates:</strong> {dates}</li>
                            <li style='margin-bottom: 10px; color: #334155;'>
                                <strong>Result:</strong> 
                                <strong style='color: {(result == "Approved" ? "#16a34a" : "#dc2626")}'>{result}</strong>
                            </li>
                            {remarkHtml}
                        </ul>
                        
                        <p style='color: #475569; font-size: 14px; margin-top: 30px;'>Thank you,<br><strong>Ronnie University Administration</strong></p>
                    </div>";

                message.IsBodyHtml = true;

                using var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(fromEmail, appPassword),
                    EnableSsl = true,
                };

                smtpClient.Send(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email Failed: {ex.Message}");
            }
        }
    }
}