using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace FYP.Pages.Admin;

// Prevent token errors and caching on the logout action
[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        return await ExecuteLogout();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        return await ExecuteLogout();
    }

    private async Task<IActionResult> ExecuteLogout()
    {
        // 1. Destroy the Admin/Staff authentication cookie
        await HttpContext.SignOutAsync("StaffAuth");

        // Destroy Student cookie too, just to be absolutely safe
        await HttpContext.SignOutAsync("StudentCookie");

        // 2. Wipe all server-side session memory for this user
        HttpContext.Session.Clear();

        // 3. Set a success message and redirect back to the login portal
        TempData["SuccessMessage"] = "Admin session terminated securely.";
        return RedirectToPage("/Staff/Login");
    }
}