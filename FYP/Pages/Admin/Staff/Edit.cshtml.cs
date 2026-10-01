using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace FYP.Pages.Admin.Staff;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class EditModel : PageModel
{
    private readonly DB _context;

    public EditModel(DB context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Uid { get; set; }

    public List<SelectListItem> RoleOptions { get; set; }
    public List<SelectListItem> DepartmentOptions { get; set; }
    public List<SelectListItem> BankOptions { get; set; }
    public List<SelectListItem> CourseOptions { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Staff ID is required.")]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Full name can only contain alphabets and spaces.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email Address is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@(gmail\.com|rocket\.com)$", ErrorMessage = "Email must be a @gmail.com or @rocket.com address.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "IC Number is required.")]
        [RegularExpression(@"^\d{6}-\d{2}-\d{4}$", ErrorMessage = "IC Number must be in the format xxxxxx-xx-xxxx and contain only numbers.")]
        public string IcNumber { get; set; }

        [Required(ErrorMessage = "Please select a Role.")]
        public int? RoleId { get; set; }

        [Required(ErrorMessage = "Please select a Department.")]
        public int? DepartmentId { get; set; }

        [Required(ErrorMessage = "Contact number is required.")]
        [Display(Name = "Contact Number")]
        [RegularExpression(@"^\+60[0-9]{8,10}$", ErrorMessage = "Contact number must start with +60 and contain only numbers.")]
        public string? ContactNo { get; set; }

        [Required(ErrorMessage = "Emergency contact is required.")]
        [Display(Name = "Emergency Contact")]
        [RegularExpression(@"^\+60[0-9]{8,10}$", ErrorMessage = "Emergency contact must start with +60 and contain only numbers.")]
        public string? EmergencyContact { get; set; }

        [Required(ErrorMessage = "Please select a Bank.")]
        [Display(Name = "Bank")]
        public int? BankId { get; set; }

        [Required(ErrorMessage = "Account number is required.")]
        [Display(Name = "Account Number")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Account number can only contain numbers.")]
        public string? BankAccNo { get; set; }

        [Display(Name = "Teaching Courses")]
        public List<int> CourseIds { get; set; } = new List<int>();

        [Display(Name = "Is Advisor")]
        public bool IsAdvisor { get; set; }

       
        [Display(Name = "Profile Picture")]
        public IFormFile? ProfilePicture { get; set; }

      
        public string? NewPassword { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _context.Users
            .Include(u => u.StaffCourses)
                .ThenInclude(sc => sc.Course)
            .FirstOrDefaultAsync(u => u.Uid == Uid);

        if (user == null) return NotFound();

        Input = new InputModel
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            IcNumber = user.IcNumber,
            RoleId = user.UsersRoleId,
            DepartmentId = user.DepartmentId,
            ContactNo = user.ContactNo,
            EmergencyContact = user.EmergencyContact,
            BankId = user.BankId,
            BankAccNo = user.BankAccNo,

            CourseIds = user.StaffCourses?.Select(sc => sc.CourseId).ToList() ?? new List<int>()
        };

        var existingCoursesList = user.StaffCourses?.Select(sc => new {
            id = sc.CourseId.ToString(),
            text = $"{sc.Course.Code} - {sc.Course.Title}"
        }).ToList();

        ViewData["ExistingCoursesJson"] = System.Text.Json.JsonSerializer.Serialize(existingCoursesList ?? new object());

        await LoadDropdowns();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await _context.Users.AnyAsync(u => u.UserId == Input.UserId && u.Uid != Uid))
            ModelState.AddModelError("Input.UserId", "This Staff ID is already registered to another user.");

        if (await _context.Users.AnyAsync(u => u.IcNumber == Input.IcNumber && u.Uid != Uid))
            ModelState.AddModelError("Input.IcNumber", "This IC Number is already registered to another user.");

        if (await _context.Users.AnyAsync(u => u.Email == Input.Email && u.Uid != Uid))
            ModelState.AddModelError("Input.Email", "This Email Address is already in use by another user.");

        if (Input.BankId.HasValue && !string.IsNullOrEmpty(Input.BankAccNo))
        {
            var bank = await _context.Banks.FindAsync(Input.BankId);
            if (bank != null)
            {
                string bankName = bank.BankName.ToLower();
                int accLength = Input.BankAccNo.Length;

                if (bankName.Contains("maybank") && accLength != 12)
                    ModelState.AddModelError("Input.BankAccNo", "Maybank account numbers must be exactly 12 digits.");
                else if (bankName.Contains("public") && accLength != 10)
                    ModelState.AddModelError("Input.BankAccNo", "Public Bank account numbers must be exactly 10 digits.");
                else if (bankName.Contains("cimb") && accLength != 10 && accLength != 14)
                    ModelState.AddModelError("Input.BankAccNo", "CIMB account numbers must be 10 or 14 digits.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadDropdowns();
            return Page();
        }

        
        var user = await _context.Users
            .Include(u => u.StaffCourses)
            .FirstOrDefaultAsync(u => u.Uid == Uid);

        if (user == null) return NotFound();

        user.UserId = Input.UserId;
        user.FullName = Input.FullName;
        user.Email = Input.Email;
        user.IcNumber = Input.IcNumber;
        user.UsersRoleId = Input.RoleId.Value;
        user.DepartmentId = Input.DepartmentId;
        user.ContactNo = Input.ContactNo;
        user.EmergencyContact = Input.EmergencyContact;
        user.BankId = Input.BankId;
        user.BankAccNo = Input.BankAccNo;

      
        if (user.StaffCourses == null)
        {
            user.StaffCourses = new List<StaffCourse>();
        }

        var coursesToRemove = user.StaffCourses
            .Where(sc => !Input.CourseIds.Contains(sc.CourseId))
            .ToList();

        foreach (var course in coursesToRemove)
        {
            _context.Remove(course);
        }

        var existingCourseIds = user.StaffCourses.Select(sc => sc.CourseId).ToList();
        var coursesToAdd = Input.CourseIds
            .Where(id => !existingCourseIds.Contains(id))
            .ToList();

        foreach (var courseId in coursesToAdd)
        {
            user.StaffCourses.Add(new StaffCourse
            {
                CourseId = courseId,
                Uid = user.Uid
            });
        }
        // ----------------------------

        if (Input.ProfilePicture != null)
        {
            var fileName = Path.GetFileName(Input.ProfilePicture.FileName);

          
            if (fileName.Count(c => c == '.') > 1)
            {
                ModelState.AddModelError("Input.ProfilePicture", "Invalid file name. Double extensions like .txt.jpg are not allowed. (.jpg and .png only)");
                await LoadDropdowns();
                return Page();
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("Input.ProfilePicture", "Only JPG, JPEG, and PNG files are allowed.");
                await LoadDropdowns();
                return Page();
            }

            if (Input.ProfilePicture.Length > 2097152)
            {
                ModelState.AddModelError("Input.ProfilePicture", "Image file size cannot exceed 2MB.");
                await LoadDropdowns();
                return Page();
            }

            using var memoryStream = new MemoryStream();
            await Input.ProfilePicture.CopyToAsync(memoryStream);
            user.Photo = memoryStream.ToArray();
        }

        if (!string.IsNullOrEmpty(Input.NewPassword))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Input.NewPassword);
        }

        await _context.SaveChangesAsync();

        return RedirectToPage("/Admin/Staff/Index");
    }

    private async Task LoadDropdowns()
    {
        RoleOptions = await _context.UsersRoles
            .Where(r => r.RoleName != "Student")
            .Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.RoleName })
            .ToListAsync();

        DepartmentOptions = await _context.Departments
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.DepartmentName })
            .ToListAsync();

        BankOptions = await _context.Banks
            .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BankName })
            .ToListAsync();

        CourseOptions = await _context.Course
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = $"{c.Code} - {c.Title}" })
            .ToListAsync();
    }
}