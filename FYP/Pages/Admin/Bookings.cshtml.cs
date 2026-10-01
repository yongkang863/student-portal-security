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
    public class BookingsModel : PageModel
    {
        private readonly DB _context;
        private readonly IConfiguration _configuration;

        public BookingsModel(DB context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public IList<Booking> PendingBookings { get; set; } = default!;

        public async Task OnGetAsync()
        {
            // Fetch all bookings where Status is Pending (Assuming ID 3 is Pending)
            PendingBookings = await _context.Bookings
                .Include(b => b.Facility)
                .Include(b => b.Student)
                .Where(b => b.StatusId == 3)
                .OrderBy(b => b.BookingDate)
                .ThenBy(b => b.TimeSlot)
                .ToListAsync();
        }

        // --- APPROVE HANDLER ---
        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            return await ProcessBookingAsync(id, 1, "Approved"); // Assuming 1 = Approved
        }

        // --- REJECT HANDLER ---
        public async Task<IActionResult> OnPostRejectAsync(int id, string remark)
        {
            return await ProcessBookingAsync(id, 2, "Rejected", remark); // Assuming 2 = Rejected
        }

        // --- CORE PROCESSING & EMAIL LOGIC ---
        private async Task<IActionResult> ProcessBookingAsync(int bookingId, int newStatusId, string resultText, string remark = null)
        {
            var booking = await _context.Bookings
                .Include(b => b.Facility)
                .Include(b => b.Student)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return NotFound();

            // 1. Update the database
            booking.StatusId = newStatusId;

            // Log which admin handled this (Getting from claims)
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(uidString, out int adminId))
            {
                booking.AdminId = adminId;
            }

            await _context.SaveChangesAsync();

            // 2. Send the Email
            // WARNING: Ensure your User model actually has an Email property!
            if (booking.Student != null && !string.IsNullOrEmpty(booking.Student.Email))
            {
                SendEmailNotification(
                    booking.Student.Email,
                    booking.Student.UserId.ToString(), // Student ID
                    booking.Student.FullName,      // Student Name
                    booking.Facility?.Name ?? "Unknown Facility",
                    $"{booking.BookingDate} at {booking.TimeSlot.ToString("hh:mm tt")}",
                    resultText,
                    remark
                );
            }

            TempData["SuccessMessage"] = $"Booking request has been {resultText.ToLower()} and the student has been notified.";
            return RedirectToPage();
        }

        private void SendEmailNotification(string toEmail, string studentId, string studentName, string facility, string dateTime, string result, string remark = null)
        {
            try
            {
                // Credentials are supplied through local configuration or environment variables.
                string fromEmail = _configuration["EmailSettings:SenderEmail"] ?? throw new InvalidOperationException("Configure EmailSettings:SenderEmail outside source control.");
                string appPassword = _configuration["EmailSettings:AppPassword"] ?? throw new InvalidOperationException("Configure EmailSettings:AppPassword outside source control.");

                var message = new MailMessage();
                message.From = new MailAddress(fromEmail, "Ronnie University Facilities");
                message.To.Add(new MailAddress(toEmail));
                message.Subject = $"Facility Booking {result}";

                // Conditionally format the remark HTML if a remark was provided
                string remarkHtml = string.IsNullOrEmpty(remark)
                    ? ""
                    : $"<li style='margin-bottom: 10px; color: #dc2626;'><strong>Admin Remark:</strong> {remark}</li>";

                // Constructing the email format you requested
                message.Body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 10px;'>
                        <h2 style='color: #1e293b; border-bottom: 2px solid #e2e8f0; padding-bottom: 10px;'>Facility Booking Update</h2>
                        <p style='color: #475569; font-size: 16px;'>Dear <strong>{studentName}</strong>,</p>
                        <p style='color: #475569; font-size: 16px;'>Your facility booking request has been processed. Here are the details:</p>
                        
                        <ul style='list-style: none; padding: 0; margin: 20px 0; background-color: #f8fafc; padding: 15px; border-radius: 8px;'>
                            <li style='margin-bottom: 10px; color: #334155;'><strong>Student ID:</strong> {studentId}</li>
                            <li style='margin-bottom: 10px; color: #334155;'><strong>Student Name:</strong> {studentName}</li>
                            <li style='margin-bottom: 10px; color: #334155;'><strong>Facility:</strong> {facility}</li>
                            <li style='margin-bottom: 10px; color: #334155;'><strong>Booking Time:</strong> {dateTime}</li>
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
                // In a real app, log this error. For now, we swallow it so the app doesn't crash if the email fails.
                Console.WriteLine($"Email Failed: {ex.Message}");
            }
        }
    }
}