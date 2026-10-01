using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FYP.Pages.Admin.LoginActivity
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
    public class _AuditTableModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}