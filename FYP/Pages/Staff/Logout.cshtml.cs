using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FYP.Pages.Staff
{
    public class LogoutModel : PageModel
    {
        public async Task<IActionResult> OnGetAsync()
        {
            // 1. Destroy the Authentication Cookie
            await HttpContext.SignOutAsync("StaffAuth");

            // 2. Completely wipe all Session variables 
            HttpContext.Session.Clear();

            // 3. Add a success message to show on the login screen
            TempData["SuccessMessage"] = "You have been successfully logged out.";

            // 4. Redirect to the Staff Login page
            return RedirectToPage("/Staff/Login");
        }

        // Catch POST requests just in case a form submits to this page
        public async Task<IActionResult> OnPostAsync()
        {
            return await OnGetAsync();
        }
    }
}