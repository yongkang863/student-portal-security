using FYP.Database;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Staff;

[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class RegisterModel : PageModel
{
    private readonly DB _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(DB context, IConfiguration configuration, ILogger<RegisterModel> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty] public InputModel Input { get; set; }
    [BindProperty] public IFormFile? UploadFile { get; set; }

    [BindProperty] public string? SerializedData { get; set; }
    public List<StaffCsvDto> PreviewList { get; set; } = new();

    public string SuccessMessage { get; set; }
    public string ErrorMessage { get; set; }

    public List<SelectListItem> RoleOptions { get; set; }
    public List<SelectListItem> DepartmentOptions { get; set; }
    public List<SelectListItem> BankOptions { get; set; }
    public List<SelectListItem> CourseOptions { get; set; }

    public class StaffCsvDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string IcNumber { get; set; }
        public int RoleId { get; set; }
        public int DepartmentId { get; set; }
        public string? ContactNo { get; set; }
        public string? EmergencyContact { get; set; }
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Staff ID is required.")]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Full name can only contain alphabets and spaces.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email Address is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@(gmail\.com|student.tarc.edu\.my)$", ErrorMessage = "Email must be a @gmail.com or @rocket.com address.")]
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

        [Required(ErrorMessage = "Please upload a profile picture.")]
        [Display(Name = "Profile Picture")]
        public IFormFile? ProfilePicture { get; set; }
    }

    public async Task OnGetAsync()
    {
        await InitializeFormDefaults();
    }

    private async Task InitializeFormDefaults()
    {
        await LoadDropdowns();

        var staffIds = await _context.Users
            .Where(u => u.UserId.StartsWith("S"))
            .Select(u => u.UserId)
            .ToListAsync();

        int maxId = 0;

        foreach (var id in staffIds)
        {
            if (id.Length > 1 && int.TryParse(id.Substring(1), out int currentNum))
            {
                if (currentNum > maxId)
                {
                    maxId = currentNum;
                }
            }
        }

        int nextId = maxId + 1;

        Input = new InputModel
        {
            UserId = $"S{nextId:D4}",
            ContactNo = "+60",
            EmergencyContact = "+60",
            IsAdvisor = false
        };
    }

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

    public IActionResult OnGetDownloadTemplate()
    {
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("FullName,Email,ICNumber,RoleID,DepartmentID,ContactNo,EmergencyContact");
        csvBuilder.AppendLine("John Doe,johndoe@gmail.com,000000-00-0000,2,1,0000000000,0000000000");

        var fileBytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
        return File(fileBytes, "text/csv", "Staff_Import_Template.csv");
    }

    // --- HELPER METHOD: Auto-fixes Excel phone numbers ---
    private string FixPhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        // Remove spaces or dashes the user might have typed
        phone = phone.Replace(" ", "").Replace("-", "").Trim();

        // Fix Excel formatting issues automatically
        if (phone.StartsWith("60")) return "+" + phone;
        if (phone.StartsWith("01")) return "+6" + phone;
        if (phone.StartsWith("1")) return "+60" + phone; // Excel dropped the leading zero
        if (!phone.StartsWith("+")) return "+" + phone; // Failsafe

        return phone;
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        if (UploadFile == null || UploadFile.Length == 0)
        {
            ErrorMessage = "Please select a valid CSV file to upload.";
            await InitializeFormDefaults();
            return Page();
        }

        if (!UploadFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Invalid file format. Please upload a .csv file.";
            await InitializeFormDefaults();
            return Page();
        }

        var staffList = new List<StaffCsvDto>();
        var errors = new List<string>();

        using (var stream = new StreamReader(UploadFile.OpenReadStream()))
        {
            var header = await stream.ReadLineAsync(); // Skip Header
            int rowNum = 1;

            while (!stream.EndOfStream)
            {
                rowNum++;
                var line = await stream.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // FIX: This removes hidden quotes and carriage returns Excel loves to add
                var values = line.Split(',').Select(v => v.Trim(' ', '"', '\r', '\n')).ToArray();

                if (values.Length < 5)
                {
                    errors.Add($"Row {rowNum}: Insufficient columns. Minimum 5 required.");
                    continue;
                }

                var dto = new StaffCsvDto
                {
                    FullName = values[0],
                    Email = values[1],
                    IcNumber = values[2],
                };

                // Parsing integers safely
                if (!int.TryParse(values[3], out int roleId)) errors.Add($"Row {rowNum}: Invalid RoleID.");
                else dto.RoleId = roleId;

                if (!int.TryParse(values[4], out int deptId)) errors.Add($"Row {rowNum}: Invalid DepartmentID.");
                else dto.DepartmentId = deptId;

                // Optional columns
                if (values.Length > 5) dto.ContactNo = string.IsNullOrWhiteSpace(values[5]) ? null : values[5];
                if (values.Length > 6) dto.EmergencyContact = string.IsNullOrWhiteSpace(values[6]) ? null : values[6];

                // Auto-fix the phone numbers before validating them!
                dto.ContactNo = FixPhoneNumber(dto.ContactNo);
                dto.EmergencyContact = FixPhoneNumber(dto.EmergencyContact);

                // Validation against Regex Rules
                if (string.IsNullOrEmpty(dto.FullName) || !Regex.IsMatch(dto.FullName, @"^[a-zA-Z\s]+$"))
                    errors.Add($"Row {rowNum}: Full name can only contain alphabets and spaces.");

                if (string.IsNullOrEmpty(dto.Email) || !Regex.IsMatch(dto.Email, @"^[a-zA-Z0-9._%+-]+@(gmail\.com|student.tarc.edu\.my)$"))
                    errors.Add($"Row {rowNum}: Email must be a @gmail.com or @rocket.com address.");

                if (string.IsNullOrEmpty(dto.IcNumber) || !Regex.IsMatch(dto.IcNumber, @"^\d{6}-\d{2}-\d{4}$"))
                    errors.Add($"Row {rowNum}: IC Number must be in format xxxxxx-xx-xxxx.");

                if (!string.IsNullOrEmpty(dto.ContactNo) && !Regex.IsMatch(dto.ContactNo, @"^\+60[0-9]{8,10}$"))
                    errors.Add($"Row {rowNum}: Contact number must start with +60 and contain 8-10 digits.");

                if (!string.IsNullOrEmpty(dto.EmergencyContact) && !Regex.IsMatch(dto.EmergencyContact, @"^\+60[0-9]{8,10}$"))
                    errors.Add($"Row {rowNum}: Emergency contact must start with +60 and contain 8-10 digits.");

                staffList.Add(dto);
            }
        }

        if (errors.Any())
        {
            ErrorMessage = "<strong>CSV Format Errors:</strong><br/>" + string.Join("<br/>", errors.Take(10));
            if (errors.Count > 10) ErrorMessage += $"<br/>...and {errors.Count - 10} more errors.";
            await InitializeFormDefaults();
            return Page();
        }

        // Database Duplication & Existence Validations
        var existingEmails = await _context.Users.Select(u => u.Email).ToListAsync();
        var existingIcs = await _context.Users.Select(u => u.IcNumber).ToListAsync();
        var existingContactNos = await _context.Users.Where(u => u.ContactNo != null).Select(u => u.ContactNo).ToListAsync();

        var validRoleIds = await _context.UsersRoles.Select(r => r.Id).ToListAsync();
        var validDeptIds = await _context.Departments.Select(d => d.Id).ToListAsync();

        var csvEmails = new HashSet<string>();
        var csvIcs = new HashSet<string>();
        var csvContacts = new HashSet<string>();

        int checkRowNum = 1;
        foreach (var staff in staffList)
        {
            checkRowNum++;

            if (!csvEmails.Add(staff.Email)) errors.Add($"Row {checkRowNum}: Duplicate Email '{staff.Email}' within CSV.");
            if (!csvIcs.Add(staff.IcNumber)) errors.Add($"Row {checkRowNum}: Duplicate IC Number '{staff.IcNumber}' within CSV.");
            if (!string.IsNullOrEmpty(staff.ContactNo) && !csvContacts.Add(staff.ContactNo)) errors.Add($"Row {checkRowNum}: Duplicate Contact No '{staff.ContactNo}' within CSV.");

            if (existingEmails.Contains(staff.Email))
                errors.Add($"Row {checkRowNum}: The email <strong>'{staff.Email}'</strong> is already registered in the system.");

            if (existingIcs.Contains(staff.IcNumber))
                errors.Add($"Row {checkRowNum}: The IC Number <strong>'{staff.IcNumber}'</strong> is already registered in the system.");

            if (!string.IsNullOrEmpty(staff.ContactNo) && existingContactNos.Contains(staff.ContactNo))
                errors.Add($"Row {checkRowNum}: The Contact Number <strong>'{staff.ContactNo}'</strong> is already registered in the system.");

            if (!validRoleIds.Contains(staff.RoleId)) errors.Add($"Row {checkRowNum}: RoleID {staff.RoleId} does not exist.");
            if (!validDeptIds.Contains(staff.DepartmentId)) errors.Add($"Row {checkRowNum}: DepartmentID {staff.DepartmentId} does not exist.");
        }

        if (errors.Any())
        {
            ErrorMessage = "<strong>Validation Errors:</strong><br/>" + string.Join("<br/>", errors.Take(10));
            if (errors.Count > 10) ErrorMessage += $"<br/>...and {errors.Count - 10} more errors.";
            await InitializeFormDefaults();
            return Page();
        }

        PreviewList = staffList;
        SerializedData = System.Text.Json.JsonSerializer.Serialize(staffList);
        await InitializeFormDefaults();
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync()
    {
        if (string.IsNullOrEmpty(SerializedData))
        {
            ErrorMessage = "Session expired or invalid data. Please re-upload the file.";
            await InitializeFormDefaults();
            return Page();
        }

        var staffList = System.Text.Json.JsonSerializer.Deserialize<List<StaffCsvDto>>(SerializedData);
        if (staffList == null || !staffList.Any())
        {
            ErrorMessage = "No valid data to import.";
            await InitializeFormDefaults();
            return Page();
        }

        var staffIds = await _context.Users
            .Where(u => u.UserId.StartsWith("S"))
            .Select(u => u.UserId)
            .ToListAsync();

        int maxId = 0;
        foreach (var id in staffIds)
        {
            if (id.Length > 1 && int.TryParse(id.Substring(1), out int currentNum))
            {
                if (currentNum > maxId) maxId = currentNum;
            }
        }

        int nextId = maxId + 1;
        var newUsers = new List<User>();

        foreach (var dto in staffList)
        {
            string generatedUserId = $"S{nextId:D4}";

            newUsers.Add(new User
            {
                UserId = generatedUserId,
                FullName = dto.FullName,
                Email = dto.Email,
                IcNumber = dto.IcNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.IcNumber),
                UsersRoleId = dto.RoleId,
                DepartmentId = dto.DepartmentId,
                ContactNo = dto.ContactNo,
                EmergencyContact = dto.EmergencyContact
            });

            nextId++;
        }

        try
        {
            _context.Users.AddRange(newUsers);
            await _context.SaveChangesAsync();

            foreach (var u in newUsers)
            {
                string emailSubject = "Welcome! Your Staff Account Has Been Created";
                string emailBody = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;'>
                        <div style='background-color: #2563eb; padding: 20px; text-align: center;'>
                            <h2 style='color: white; margin: 0;'>Welcome to the University!</h2>
                        </div>
                        <div style='padding: 30px; background-color: #f8fafc; color: #334155;'>
                            <p style='font-size: 16px;'>Hello <strong>{WebUtility.HtmlEncode(u.FullName)}</strong>,</p>
                            <p style='font-size: 16px;'>Your staff account has been successfully created. You can now access the system portal using the credentials below:</p>
                            
                            <div style='background-color: white; padding: 15px; border-radius: 6px; border: 1px solid #cbd5e1; margin: 20px 0;'>
                                <p style='margin: 5px 0;'><strong>Staff ID / Username:</strong> <span style='color: #2563eb;'>{WebUtility.HtmlEncode(u.UserId)}</span></p>
                                <p style='margin: 5px 0;'><strong>Default Password:</strong> <span style='color: #2563eb;'>{WebUtility.HtmlEncode(u.IcNumber)}</span></p>
                            </div>

                            <p style='font-size: 14px; color: #ef4444; font-weight: bold;'>
                                Welcome To The ROCKET UNIVERSITY!!! All the best in your career.
                            </p>
                            
                            <p style='font-size: 14px; margin-top: 30px;'>Best regards,<br>System Administrator</p>
                        </div>
                    </div>";
                SendEmail(u.Email, emailSubject, emailBody);
            }

            SuccessMessage = $"Successfully registered {newUsers.Count} staff members. Welcome emails sent.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import staff accounts.");
            ErrorMessage = "An error occurred while saving the data to the database.";
        }

        await InitializeFormDefaults();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await _context.Users.AnyAsync(u => u.UserId == Input.UserId))
            ModelState.AddModelError("Input.UserId", "This Staff ID is already registered in the system.");

        if (await _context.Users.AnyAsync(u => u.IcNumber == Input.IcNumber))
            ModelState.AddModelError("Input.IcNumber", "This IC Number is already registered in the system.");

        if (await _context.Users.AnyAsync(u => u.Email == Input.Email))
            ModelState.AddModelError("Input.Email", "This Email Address is already in use.");

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

        var newUser = new User
        {
            UserId = Input.UserId,
            FullName = Input.FullName,
            Email = Input.Email,
            IcNumber = Input.IcNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Input.IcNumber),
            UsersRoleId = Input.RoleId.Value,
            DepartmentId = Input.DepartmentId,
            ContactNo = Input.ContactNo,
            EmergencyContact = Input.EmergencyContact,
            BankId = Input.BankId,
            BankAccNo = Input.BankAccNo
        };

        if (Input.CourseIds != null && Input.CourseIds.Any())
        {
            newUser.StaffCourses = new List<StaffCourse>();
            foreach (var courseId in Input.CourseIds)
            {
                newUser.StaffCourses.Add(new StaffCourse { CourseId = courseId });
            }
        }

        if (Input.ProfilePicture != null)
        {
            var fileName = Path.GetFileName(Input.ProfilePicture.FileName);

            if (fileName.Count(c => c == '.') > 1)
            {
                ModelState.AddModelError("Input.ProfilePicture", "Invalid file name. Double extensions are not allowed.");
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

            if (!IsValidImageSignature(Input.ProfilePicture))
            {
                ModelState.AddModelError("Input.ProfilePicture", "The uploaded file is corrupt or is not a valid image file.");
                await LoadDropdowns();
                return Page();
            }

            using var memoryStream = new MemoryStream();
            await Input.ProfilePicture.CopyToAsync(memoryStream);
            newUser.Photo = memoryStream.ToArray();
        }

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        string emailSubject = "Welcome! Your Staff Account Has Been Created";
        string emailBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;'>
                <div style='background-color: #2563eb; padding: 20px; text-align: center;'>
                    <h2 style='color: white; margin: 0;'>Welcome to the University!</h2>
                </div>
                <div style='padding: 30px; background-color: #f8fafc; color: #334155;'>
                    <p style='font-size: 16px;'>Hello <strong>{WebUtility.HtmlEncode(newUser.FullName)}</strong>,</p>
                    <p style='font-size: 16px;'>Your staff account has been successfully created. You can now access the system portal using the credentials below:</p>
                    
                    <div style='background-color: white; padding: 15px; border-radius: 6px; border: 1px solid #cbd5e1; margin: 20px 0;'>
                        <p style='margin: 5px 0;'><strong>Staff ID / Username:</strong> <span style='color: #2563eb;'>{WebUtility.HtmlEncode(newUser.UserId)}</span></p>
                        <p style='margin: 5px 0;'><strong>Default Password:</strong> <span style='color: #2563eb;'>{WebUtility.HtmlEncode(newUser.IcNumber)}</span></p>
                    </div>

                    <p style='font-size: 14px; color: #ef4444; font-weight: bold;'>
                        Welcome To The ROCKET UNIVERSITY!!! All the best in your career.
                    </p>
                    
                    <p style='font-size: 14px; margin-top: 30px;'>Best regards,<br>System Administrator</p>
                </div>
            </div>";

        try
        {
            SendEmail(newUser.Email, emailSubject, emailBody);
            SuccessMessage = "Staff registered successfully. Welcome email has been sent.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to newly registered staff: {Email}", newUser.Email);
            SuccessMessage = "Staff registered successfully, but the welcome email failed to send. Please notify the user manually.";
        }

        ModelState.Clear();
        await InitializeFormDefaults();
        return Page();
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

    private void SendEmail(string toEmail, string subject, string body)
    {
        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var appPassword = _configuration["EmailSettings:AppPassword"];

        var client = new SmtpClient("smtp.gmail.com", 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(senderEmail, appPassword)
        };

        var mailMessage = new MailMessage(senderEmail, toEmail, subject, body) { IsBodyHtml = true };
        client.Send(mailMessage);
    }
}