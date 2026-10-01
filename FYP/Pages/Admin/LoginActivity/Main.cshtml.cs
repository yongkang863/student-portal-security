using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.LoginActivity;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class MainModel : PageModel
{
    private readonly DB _context;

    public MainModel(DB context)
    {
        _context = context;
    }

    public class AuditViewModel
    {
        public int LogId { get; set; }
        public DateTime Time { get; set; }
        public string UserName { get; set; }
        public string UserId { get; set; }
        public string IpAddress { get; set; }
        public string Device { get; set; }
        public string Location { get; set; }
        public string Status { get; set; }
    }

    public List<AuditViewModel> AdminLogs { get; set; } = new();
    public List<AuditViewModel> StaffLogs { get; set; } = new();
    public List<AuditViewModel> StudentLogs { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Fetch ALL logs joined with User info
        var allLogs = await _context.UsersLoginAudits
            .OrderByDescending(a => a.LoginTime)
            .Take(300)
            .Join(_context.Users,
                audit => audit.Uid,
                user => user.Uid,
                (audit, user) => new
                {
                    Log = audit,
                    User = user,
                    RoleId = user.UsersRoleId
                })
            .ToListAsync();

        foreach (var item in allLogs)
        {
            var vm = new AuditViewModel
            {
                LogId = item.Log.Id,
                Time = item.Log.LoginTime,
                UserName = item.User.FullName,
                UserId = item.User.UserId,
                // Safely handle nulls from older logs that were saved before we added the IP Helper
                IpAddress = string.IsNullOrEmpty(item.Log.IpAddress) ? "Unknown" : item.Log.IpAddress,
                Device = string.IsNullOrEmpty(item.Log.Device) ? "Unknown" : item.Log.Device,
                Location = string.IsNullOrEmpty(item.Log.Location) ? "Unknown" : item.Log.Location,
                Status = item.Log.Status
            };

            if (item.RoleId == 1) AdminLogs.Add(vm);
            else if (item.RoleId == 2) StaffLogs.Add(vm);
            else if (item.RoleId == 3) StudentLogs.Add(vm);
        }
    }
}