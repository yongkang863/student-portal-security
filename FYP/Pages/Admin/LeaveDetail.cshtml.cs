using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FYP.Pages.Admin
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
    public class LeaveDetailModel : PageModel
    {
        private readonly DB _context;

        public LeaveDetailModel(DB context)
        {
            _context = context;
        }

        public StudentLeave LeaveRequest { get; set; } = default!;
        public string StudentPhotoBase64 { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            // Fetch specific leave with related Student and Status navigation
            var leave = await _context.StudentLeaves
                .Include(l => l.Student)
                .Include(l => l.Status)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (leave == null) return NotFound();

            LeaveRequest = leave;

            // Prepare student photo for display
            if (LeaveRequest.Student?.Photo != null && LeaveRequest.Student.Photo.Length > 0)
            {
                StudentPhotoBase64 = $"data:image/jpg;base64,{Convert.ToBase64String(LeaveRequest.Student.Photo)}";
            }

            return Page();
        }

        // --- APPROVE HANDLER FROM DETAIL PAGE ---
        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            return await ProcessLeaveDetailAsync(id, 1, "Approved"); // 1 = Approved
        }

        // --- REJECT HANDLER FROM DETAIL PAGE ---
        public async Task<IActionResult> OnPostRejectAsync(int id)
        {
            return await ProcessLeaveDetailAsync(id, 2, "Rejected"); // 2 = Rejected
        }

        // --- CORE PROCESSING LOGIC (Duplicated from Leaves.cshtml.cs for small project simplicity) ---
        private async Task<IActionResult> ProcessLeaveDetailAsync(int leaveId, int newStatusId, string resultText)
        {
            var request = await _context.StudentLeaves.FindAsync(leaveId);
            if (request == null) return NotFound();

            // 1. Update status
            request.StatusId = newStatusId;

            // 2. Log current Admin ID from claims
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(uidString, out int adminId))
            {
                request.AdminId = adminId;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Leave request has been {resultText.ToLower()} successfully.";

            // Redirect back to the main list after processing
            return RedirectToPage("/Admin/Leaves");
        }
    }
}