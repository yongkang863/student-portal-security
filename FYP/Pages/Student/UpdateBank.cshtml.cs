using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FYP.Pages.Student
{
    [Authorize(Policy = "StudentOnly", AuthenticationSchemes = "StudentCookie")]
    public class UpdateBankModel : PageModel
    {
        private readonly DB _context;

        public UpdateBankModel(DB context)
        {
            _context = context;
        }

        [BindProperty]
        public BankInputModel Input { get; set; } = new();

        // Dropdown List
        public List<SelectListItem> BankOptions { get; set; } = new();

        public class BankInputModel
        {
            [Required(ErrorMessage = "Please select a bank.")]
            [Display(Name = "Bank Name")]
            public int? BankId { get; set; }

            [Required(ErrorMessage = "Account number is required.")]
            [StringLength(10, MinimumLength = 10, ErrorMessage = "Account number must be exactly 10 digits.")]
            [RegularExpression(@"^\d{10}$", ErrorMessage = "Account number must contain exactly 10 digits.")]
            [Display(Name = "Account Number")]
            public string BankAccNo { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uidString, out int uid)) return RedirectToPage("/Student/Login");

            // Load Banks for Dropdown
            BankOptions = await _context.Banks
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BankName })
                .ToListAsync();

            var user = await _context.Users.FindAsync(uid);
            if (user != null)
            {
                Input.BankId = user.BankId;
                // Note: We cannot decrypt the existing hash, so we leave the text box empty 
                // or user has to re-enter it to update.
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Reload Dropdown if validation fails
                BankOptions = await _context.Banks
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BankName })
                    .ToListAsync();
                return Page();
            }

            var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uidString, out int uid)) return RedirectToPage("/Student/Login");

            var user = await _context.Users.FindAsync(uid);
            if (user == null) return NotFound();

            // 1. Update Bank ID
            user.BankId = Input.BankId;

            // 2. Encrypt (Hash) Bank Account Number using BCrypt
            // Note: DB Column 'bank_acc_no' must be large enough (e.g., VARCHAR(100)) to store the hash.
            string encryptedAccNo = FYP.Helpers.SecurityHelper.Encrypt(Input.BankAccNo);
            user.BankAccNo = encryptedAccNo;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bank details updated securely!";
            return RedirectToPage("/Student/Profile");
        }
    }
}