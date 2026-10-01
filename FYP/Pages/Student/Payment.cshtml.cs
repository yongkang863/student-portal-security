using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using FYP.Database;
using FYP.Models;

namespace FYP.Pages.Student;

// Custom Validation Attribute for Expiry Date
public class ValidCardExpiryAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success;
        }

        string expiry = value.ToString();
        var parts = expiry.Split('/');

        if (parts.Length != 2 || !int.TryParse(parts[0], out int month) || !int.TryParse(parts[1], out int year))
        {
            return new ValidationResult("Invalid expiry date format.");
        }

        int currentYear = DateTime.Now.Year % 100;
        int currentMonth = DateTime.Now.Month;
        int maxYear = currentYear + 5;

        // 1. Month validation (1-12)
        if (month < 1 || month > 12)
        {
            return new ValidationResult("Month must be between 01 and 12.");
        }

        // 2. Check if the card is already expired
        if (year < currentYear || (year == currentYear && month < currentMonth))
        {
            return new ValidationResult("This card has expired.");
        }

        // 3. Check if the year exceeds the 5-year maximum limit
        if (year > maxYear)
        {
            return new ValidationResult($"Expiry year cannot exceed 20{maxYear}.");
        }

        return ValidationResult.Success;
    }
}

public class PaymentModel : PageModel
{
    private readonly DB _context;

    public PaymentModel(DB context)
    {
        _context = context;
    }

    public FYP.Models.Bill PaymentBill { get; set; }

    [BindProperty]
    public int BillId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Please select a bank.")]
    public string SelectedBank { get; set; }
    public List<SelectListItem> BankOptions { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "First Name is required.")]
    [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "First Name can only contain alphabets.")]
    public string FirstName { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Last Name is required.")]
    [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Last Name can only contain alphabets.")]
    public string LastName { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Card number is required.")]
    [RegularExpression(@"^\d{16}$", ErrorMessage = "Invalid card number. Must be exactly 16 digits.")]
    public string CardNumber { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Expiry date is required.")]
    [RegularExpression(@"^(0[1-9]|1[0-2])\/?([0-9]{2})$", ErrorMessage = "Invalid expiry date format (MM/YY).")]
    [ValidCardExpiry] // Applied the custom validation here
    public string ExpiryDate { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "CVV is required.")]
    [RegularExpression(@"^\d{3}$", ErrorMessage = "Invalid CVV. Must be exactly 3 digits.")]
    public string CVV { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int studentId = string.IsNullOrEmpty(uidString) ? 0 : int.Parse(uidString);

        PaymentBill = await _context.Set<FYP.Models.Bill>()
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == studentId);

        if (PaymentBill == null || PaymentBill.StatusId == 6)
        {
            return RedirectToPage("/Student/CurrentBilling");
        }

        BillId = PaymentBill.Id;
        await LoadBanksAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var uidString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int studentId = string.IsNullOrEmpty(uidString) ? 0 : int.Parse(uidString);

        if (!ModelState.IsValid)
        {
            PaymentBill = await _context.Set<FYP.Models.Bill>()
                .FirstOrDefaultAsync(b => b.Id == BillId && b.UserId == studentId);
            await LoadBanksAsync();
            return Page();
        }

        var bill = await _context.Set<FYP.Models.Bill>()
            .FirstOrDefaultAsync(b => b.Id == BillId && b.UserId == studentId);

        if (bill != null && bill.StatusId != 6)
        {
            int paidStatusId = 6;
            bill.StatusId = paidStatusId;

            string last4Digits = CardNumber.Substring(CardNumber.Length - 4);
            string shortBankName = SelectedBank.Length > 15 ? SelectedBank.Substring(0, 15) : SelectedBank;

            string simulatedPaymentIntent = $"pi_sim_{Guid.NewGuid().ToString("N").Substring(0, 20)}";

            var stripePayload = new
            {
                stripe_payment_intent = simulatedPaymentIntent,
                payment_card = SelectedBank
            };

            string jsonPayloadString = JsonSerializer.Serialize(stripePayload);

            var transaction = new PaymentTransaction
            {
                BillId = bill.Id,
                Amount = bill.Amount,
                PaymentDate = DateTime.Now,
                Method = $"{shortBankName} (*{last4Digits})",
                StatusId = paidStatusId,
                StripeSessionId = jsonPayloadString
            };

            _context.Set<PaymentTransaction>().Add(transaction);
            await _context.SaveChangesAsync();

            return RedirectToPage("/Student/PaymentSuccess", new { txnId = transaction.Id });
        }

        return RedirectToPage("/Student/CurrentBilling");
    }

    private async Task LoadBanksAsync()
    {
        BankOptions = await _context.Set<Bank>()
            .Select(b => new SelectListItem { Value = b.BankName, Text = b.BankName })
            .ToListAsync();
    }
}