using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Students;

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

    public List<SelectListItem> DepartmentOptions { get; set; }
    public List<SelectListItem> BankOptions { get; set; }
    public List<SelectListItem> AdvisoryOptions { get; set; }
    public List<SelectListItem> StatusOptions { get; set; }

    public string ProgrammesJson { get; set; }

    public class ProgrammeDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? DepartmentId { get; set; }
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Student ID is required.")]
        [StringLength(10, ErrorMessage = "Student ID cannot exceed 10 characters.")]
        [RegularExpression(@"^\d{2}[A-Z]{3}\d{5}$", ErrorMessage = "Format must be 2 digits, 3 capital letters, 5 digits (e.g., 00AAA00000).")]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(50, ErrorMessage = "Full Name cannot exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Full name can only contain alphabets and spaces.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email Address is required.")]
        [StringLength(50, ErrorMessage = "Email Address cannot exceed 50 characters.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "IC Number is required.")]
        [StringLength(20, ErrorMessage = "IC Number cannot exceed 20 characters.")]
        [RegularExpression(@"^\d{6}-\d{2}-\d{4}$", ErrorMessage = "IC Number must be in the format xxxxxx-xx-xxxx.")]
        public string IcNumber { get; set; }

        [StringLength(100, ErrorMessage = "Address cannot exceed 100 characters.")]
        public string? Address { get; set; }

        [StringLength(50, ErrorMessage = "Contact number cannot exceed 50 characters.")]
        [RegularExpression(@"^\d{1,11}$", ErrorMessage = "Contact number must contain digits only (maximum 11 digits).")]
        public string? ContactNo { get; set; }

        [StringLength(50, ErrorMessage = "Emergency contact cannot exceed 50 characters.")]
        [RegularExpression(@"^\d{1,11}$", ErrorMessage = "Emergency contact must contain digits only (maximum 11 digits).")]
        public string? EmergencyContact { get; set; }

        public int? BankId { get; set; }

        [StringLength(10, MinimumLength = 10, ErrorMessage = "Bank account number must be exactly 10 digits.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Account number must contain exactly 10 digits.")]
        public string? BankAccNo { get; set; }

        [Required(ErrorMessage = "Please select a Department.")]
        public int? DepartmentId { get; set; }

        [Required(ErrorMessage = "Please select a Programme.")]
        public int? ProgrammeId { get; set; }

        [Range(1000, 9999, ErrorMessage = "Intake Year must be exactly 4 digits.")]
        public int? IntakeYear { get; set; }

        [RegularExpression(@"^\d{4}(0[1-9]|1[0-2])$", ErrorMessage = "Session must be in YYYYMM format with a valid month from 01 to 12 (e.g., 202309).")]
        public int? SessionJoined { get; set; }

        [Range(1, 3, ErrorMessage = "Current Year Level maximum is 3.")]
        public int? YearLevel { get; set; }

        [Range(1, 3, ErrorMessage = "Semester maximum is 3.")]
        public int? Semester { get; set; }

        [StringLength(5, ErrorMessage = "Tutorial group cannot exceed 5 characters.")]
        [RegularExpression(@"^G([1-9]|1[0-5])$", ErrorMessage = "Tutorial group must be 'G' followed by a number from 1 to 15 (e.g., G1 or G15).")]
        public string? TutorialGroup { get; set; }

        [StringLength(50, ErrorMessage = "Study status cannot exceed 50 characters.")]
        public string? StudyStatus { get; set; }
        public int? AdvisoryId { get; set; }

        public IFormFile? ProfilePicture { get; set; }
        public string? NewPassword { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _context.Users
            .Include(u => u.Programme)
            .FirstOrDefaultAsync(u => u.Uid == Uid && u.UsersRoleId == 3);

        if (user == null) return NotFound();

        string decryptedBankAcc = "";
        if (!string.IsNullOrEmpty(user.BankAccNo))
        {
            try { decryptedBankAcc = FYP.Helpers.SecurityHelper.Decrypt(user.BankAccNo); }
            catch { decryptedBankAcc = ""; }
        }

        Input = new InputModel
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            IcNumber = user.IcNumber,
            Address = user.Address,
            ContactNo = user.ContactNo,
            EmergencyContact = user.EmergencyContact,
            BankId = user.BankId,
            BankAccNo = decryptedBankAcc,

            DepartmentId = user.DepartmentId ?? user.Programme?.DepartmentId,
            ProgrammeId = user.ProgrammeId,
            IntakeYear = user.IntakeYear,
            SessionJoined = user.SessionJoined,
            YearLevel = user.YearLevel,
            Semester = user.Semester,
            TutorialGroup = user.TutorialGroup,
            StudyStatus = user.StudyStatus,
            AdvisoryId = user.AdvisoryId
        };

        await LoadDropdownsAsync();
        return Page();
    }

    // --- SECURITY ADDITION: Magic Number Validation ---
    private bool IsValidImageSignature(IFormFile file)
    {
        if (file == null || file.Length == 0) return false;

        using var reader = new BinaryReader(file.OpenReadStream());
        var signatures = new List<byte[]>
        {
            new byte[] { 0xFF, 0xD8, 0xFF }, // JPEG Magic Numbers
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } // PNG Magic Numbers
        };

        var headerBytes = reader.ReadBytes(8);
        return signatures.Any(signature => headerBytes.Take(signature.Length).SequenceEqual(signature));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await _context.Users.AnyAsync(u => u.UserId == Input.UserId && u.Uid != Uid))
            ModelState.AddModelError("Input.UserId", "This Student ID is already registered to another user.");

        if (await _context.Users.AnyAsync(u => u.Email == Input.Email && u.Uid != Uid))
            ModelState.AddModelError("Input.Email", "This Email Address is already in use by another user in the system.");

        if (await _context.Users.AnyAsync(u => u.IcNumber == Input.IcNumber && u.Uid != Uid))
            ModelState.AddModelError("Input.IcNumber", "This IC Number is already registered to another user.");

        if (!string.IsNullOrEmpty(Input.BankAccNo))
        {
            var usersWithBanks = await _context.Users
                .Where(u => u.BankAccNo != null && u.Uid != Uid)
                .Select(u => new { u.Uid, u.BankAccNo })
                .ToListAsync();

            bool isBankTaken = usersWithBanks.Any(u =>
            {
                try { return FYP.Helpers.SecurityHelper.Decrypt(u.BankAccNo) == Input.BankAccNo; }
                catch { return false; }
            });

            if (isBankTaken)
                ModelState.AddModelError("Input.BankAccNo", "This Bank Account Number is already in use by another user.");
        }

        // --- UPDATED IMAGE VALIDATION ---
        if (Input.ProfilePicture != null)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(Input.ProfilePicture.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("Input.ProfilePicture", "Only JPG, JPEG, and PNG image files are allowed.");
            }
            else if (Input.ProfilePicture.Length > 2097152)
            {
                ModelState.AddModelError("Input.ProfilePicture", "Image file size cannot exceed 2MB.");
            }
            else if (!IsValidImageSignature(Input.ProfilePicture))
            {
                ModelState.AddModelError("Input.ProfilePicture", "The uploaded file is corrupt or is not a valid image file.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync();
            return Page();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Uid == Uid);
        if (user == null) return NotFound();

        user.UserId = Input.UserId;
        user.FullName = Input.FullName;
        user.Email = Input.Email;
        user.IcNumber = Input.IcNumber;
        user.Address = Input.Address;
        user.ContactNo = Input.ContactNo;
        user.EmergencyContact = Input.EmergencyContact;

        user.BankId = Input.BankId;
        user.BankAccNo = string.IsNullOrEmpty(Input.BankAccNo) ? null : FYP.Helpers.SecurityHelper.Encrypt(Input.BankAccNo);

        user.DepartmentId = Input.DepartmentId;
        user.ProgrammeId = Input.ProgrammeId;
        user.IntakeYear = Input.IntakeYear;
        user.SessionJoined = Input.SessionJoined;
        user.YearLevel = Input.YearLevel;
        user.Semester = Input.Semester;
        user.TutorialGroup = Input.TutorialGroup;
        user.StudyStatus = Input.StudyStatus;
        user.AdvisoryId = Input.AdvisoryId;

        if (Input.ProfilePicture != null)
        {
            using var memoryStream = new MemoryStream();
            await Input.ProfilePicture.CopyToAsync(memoryStream);
            user.Photo = memoryStream.ToArray();
        }

        if (!string.IsNullOrEmpty(Input.NewPassword))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Input.NewPassword);
        }

        await _context.SaveChangesAsync();
        return RedirectToPage("/Admin/Students/Index");
    }

    private async Task LoadDropdownsAsync()
    {
        DepartmentOptions = await _context.Set<Department>()
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.DepartmentName })
            .ToListAsync();

        var programmes = await _context.Set<Programme>()
            .Select(p => new ProgrammeDto { Id = p.Id, Name = $"{p.Code} - {p.Name}", DepartmentId = p.DepartmentId })
            .ToListAsync();
        ProgrammesJson = JsonSerializer.Serialize(programmes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        BankOptions = await _context.Set<Bank>()
            .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BankName })
            .ToListAsync();

        AdvisoryOptions = await _context.Set<Advisory>()
            .Include(a => a.Staff)
            .Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Staff.FullName ?? "Unknown Staff" })
            .ToListAsync();

        StatusOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "Active", Text = "Active" },
            new SelectListItem { Value = "Inactive", Text = "Inactive" }
        };
    }
}