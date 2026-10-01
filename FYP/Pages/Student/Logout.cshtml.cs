using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace FYP.Pages.Student
{
    public class LogoutModel : PageModel
    {
        // Called by the Javascript Timer
        public async Task<IActionResult> OnGetAsync(string? reason)
        {
            await HttpContext.SignOutAsync("StudentCookie");

            if (reason == "timeout")
            {
                return RedirectToPage("/Student/Login", new { error = "timeout" });
            }

            return RedirectToPage("/Student/Login");
        }

        // Called by the Logout Button in the sidebar
        public async Task<IActionResult> OnPostAsync()
        {
            await HttpContext.SignOutAsync("StudentCookie");
            return RedirectToPage("/Student/Login");
        }
    }
}