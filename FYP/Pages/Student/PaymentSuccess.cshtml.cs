using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Text.Json; // Added for JSON parsing
using FYP.Database;
using FYP.Models;

namespace FYP.Pages.Student;

public class PaymentSuccessModel : PageModel
{
    private readonly DB _context;

    public PaymentSuccessModel(DB context)
    {
        _context = context;
    }

    public PaymentTransaction Transaction { get; set; }

    // New property to pass the exact Stripe ID to the view
    public string ExtractedStripeIntentId { get; set; }

    public async Task<IActionResult> OnGetAsync(int txnId)
    {
        // Fetch the transaction and include the associated Bill
        Transaction = await _context.Set<PaymentTransaction>()
            .Include(p => p.Bill)
            .FirstOrDefaultAsync(p => p.Id == txnId);

        if (Transaction == null)
        {
            return RedirectToPage("/Student/CurrentBilling");
        }

        // Safely extract the stripe_payment_intent from the JSON string
        ExtractedStripeIntentId = "N/A";

        if (!string.IsNullOrEmpty(Transaction.StripeSessionId))
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(Transaction.StripeSessionId))
                {
                    if (doc.RootElement.TryGetProperty("stripe_payment_intent", out JsonElement intentElement))
                    {
                        ExtractedStripeIntentId = intentElement.GetString();
                    }
                }
            }
            catch
            {
                // Fallback: If it's an old record or plain text, just print the raw string
                ExtractedStripeIntentId = Transaction.StripeSessionId;
            }
        }

        return Page();
    }
}