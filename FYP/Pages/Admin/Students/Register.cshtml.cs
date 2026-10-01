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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FYP.Pages.Admin.Students;

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
    public List<StudentCsvDto> PreviewList { get; set; } = new();

    public string SuccessMessage { get; set; }
    public string ErrorMessage { get; set; }

    public List<SelectListItem> DepartmentOptions { get; set; }
    public List<SelectListItem> BankOptions { get; set; }
    public List<SelectListItem> StatusOptions { get; set; }
    public string ProgrammesJson { get; set; }

    public class ProgrammeDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? DepartmentId { get; set; }
    }

    public class StudentCsvDto
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

        [Required(ErrorMessage = "Home Address is required.")]
        [StringLength(100, ErrorMessage = "Address cannot exceed 100 characters.")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Department is required.")]
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "Programme is required.")]
        public int ProgrammeId { get; set; }

        [Required(ErrorMessage = "Session Joined is required.")]
        [RegularExpression(@"^\d{4}(0[1-9]|1[0-2])$", ErrorMessage = "Session must be in YYYYMM format (e.g., 202401).")]
        public int SessionJoined { get; set; }

        [Required(ErrorMessage = "Intake Year is required.")]
        [Range(1000, 9999, ErrorMessage = "Intake Year must be exactly 4 digits.")]
        public int IntakeYear { get; set; }

        [Required(ErrorMessage = "Year Level is required.")]
        [Range(1, 3, ErrorMessage = "Current Year Level maximum is 3.")]
        public int YearLevel { get; set; }

        [Required(ErrorMessage = "Semester is required.")]
        [Range(1, 3, ErrorMessage = "Semester maximum is 3.")]
        public int Semester { get; set; }

        [Required(ErrorMessage = "Tutorial Group is required.")]
        [StringLength(5, ErrorMessage = "Tutorial group cannot exceed 5 characters.")]
        [RegularExpression(@"^G([1-9]|1[0-5])$", ErrorMessage = "Tutorial group must be 'G' followed by 1 to 15 (e.g., G1 or G15).")]
        public string TutorialGroup { get; set; }

        [Required(ErrorMessage = "Study Status is required.")]
        [StringLength(50, ErrorMessage = "Study status cannot exceed 50 characters.")]
        public string StudyStatus { get; set; }

        [StringLength(50, ErrorMessage = "Contact number cannot exceed 50 characters.")]
        [RegularExpression(@"^\d{1,11}$", ErrorMessage = "Contact number must contain digits only (maximum 11 digits).")]
        public string? ContactNo { get; set; }

        [StringLength(50, ErrorMessage = "Emergency contact cannot exceed 50 characters.")]
        [RegularExpression(@"^\d{1,11}$", ErrorMessage = "Emergency contact must contain digits only (maximum 11 digits).")]
        public string? EmergencyContact { get; set; }
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

        [Required(ErrorMessage = "Home Address is required.")]
        [StringLength(100, ErrorMessage = "Address cannot exceed 100 characters.")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Please select a Department.")]
        public int? DepartmentId { get; set; }

        [Required(ErrorMessage = "Please select a Programme.")]
        public int? ProgrammeId { get; set; }

        [Required(ErrorMessage = "Intake Year is required.")]
        [Range(1000, 9999, ErrorMessage = "Intake Year must be exactly 4 digits.")]
        public int? IntakeYear { get; set; }

        [Required(ErrorMessage = "Session Joined is required.")]
        [RegularExpression(@"^\d{4}(0[1-9]|1[0-2])$", ErrorMessage = "Session must be in YYYYMM format (e.g., 202401).")]
        public int? SessionJoined { get; set; }

        [Required(ErrorMessage = "Year Level is required.")]
        [Range(1, 3, ErrorMessage = "Current Year Level maximum is 3.")]
        public int? YearLevel { get; set; }

        [Required(ErrorMessage = "Semester is required.")]
        [Range(1, 3, ErrorMessage = "Semester maximum is 3.")]
        public int? Semester { get; set; }

        [Required(ErrorMessage = "Tutorial Group is required.")]
        [StringLength(5, ErrorMessage = "Tutorial group cannot exceed 5 characters.")]
        [RegularExpression(@"^G([1-9]|1[0-5])$", ErrorMessage = "Tutorial group must be 'G' followed by 1 to 15 (e.g., G1 or G15).")]
        public string? TutorialGroup { get; set; }

        [Required(ErrorMessage = "Study Status is required.")]
        [StringLength(50, ErrorMessage = "Study status cannot exceed 50 characters.")]
        public string? StudyStatus { get; set; }

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

        public IFormFile? ProfilePicture { get; set; }
    }

    public async Task OnGetAsync()
    {
        await InitializeFormDefaults();
    }

    private async Task InitializeFormDefaults()
    {
        await LoadDropdowns();
        Input = new InputModel
        {
            YearLevel = 1,
            Semester = 1,
            StudyStatus = "Active"
        };
    }

    public IActionResult OnGetDownloadTemplate()
    {
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("StudentID,FullName,Email,ICNumber,HomeAddress,DepartmentID,ProgrammeID,SessionJoined,IntakeYear,YearLevel,Semester,TutorialGroup,StudyStatus,ContactNo,EmergencyContact");
        csvBuilder.AppendLine("00AAA00000,John Doe,john.doe@student.edu.my,000000-00-0000,\"Example address\",1,1,202401,2024,1,1,G1,Active,0000000000,0000000000");

        var fileBytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
        return File(fileBytes, "text/csv", "Student_Import_Template.csv");
    }

    // --- SECURITY: Magic Number Validation ---
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

    // --- ADDED: CSV Upload & Validation Handler ---
    public async Task<IActionResult> OnPostUploadAsync()
    {
        if (UploadFile == null || UploadFile.Length == 0)
        {
            ErrorMessage = "Please select a valid CSV file to upload.";
            await InitializeFormDefaults();
            return Page();
        }

        if (Path.GetExtension(UploadFile.FileName).ToLower() != ".csv")
        {
            ErrorMessage = "Only CSV files are allowed.";
            await InitializeFormDefaults();
            return Page();
        }

        try
        {
            using var reader = new StreamReader(UploadFile.OpenReadStream());
            var isFirstRow = true;
            var students = new List<StudentCsvDto>();
            var rowNumber = 1;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                rowNumber++;

                if (string.IsNullOrWhiteSpace(line)) continue;


                var values = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")
                                  .Select(v => v.Trim().Trim('"'))
                                  .ToArray();

                if (isFirstRow)
                {
                    isFirstRow = false;
                    continue;
                }

                if (values.Length >= 13)
                {
                    try
                    {
                        var studentDto = new StudentCsvDto
                        {
                            UserId = values[0].Trim(),
                            FullName = values[1].Trim(),
                            Email = values[2].Trim(),
                            IcNumber = values[3].Trim(),
                            Address = values[4].Trim().Trim('"'),
                            DepartmentId = int.Parse(values[5].Trim()),
                            ProgrammeId = int.Parse(values[6].Trim()),
                            SessionJoined = int.Parse(values[7].Trim()),
                            IntakeYear = int.Parse(values[8].Trim()),
                            YearLevel = int.Parse(values[9].Trim()),
                            Semester = int.Parse(values[10].Trim()),
                            TutorialGroup = values[11].Trim(),
                            StudyStatus = values[12].Trim(),
                            ContactNo = values.Length > 13 && !string.IsNullOrWhiteSpace(values[13]) ? values[13].Trim() : null,
                            EmergencyContact = values.Length > 14 && !string.IsNullOrWhiteSpace(values[14]) ? values[14].Trim() : null
                        };

                        // TRIGGER STRICT VALIDATION ON THE ROW
                        var validationContext = new ValidationContext(studentDto);
                        var validationResults = new List<ValidationResult>();

                        if (!Validator.TryValidateObject(studentDto, validationContext, validationResults, true))
                        {
                            var firstError = validationResults.First().ErrorMessage;
                            ErrorMessage = $"<strong>Validation Failed on Row {rowNumber}:</strong> {firstError} (Student ID: {studentDto.UserId})";
                            await InitializeFormDefaults();
                            return Page();
                        }

                        students.Add(studentDto);
                    }
                    catch (FormatException)
                    {
                        ErrorMessage = $"<strong>Format Error on Row {rowNumber}:</strong> One of the numeric fields (DepartmentID, ProgrammeID, Session, Year, Semester) contains invalid text instead of a number.";
                        await InitializeFormDefaults();
                        return Page();
                    }
                }
            }

            if (!students.Any())
            {
                ErrorMessage = "The uploaded file is empty or missing required data columns.";
            }
            else
            {
                PreviewList = students;
                SerializedData = JsonSerializer.Serialize(students); // Save data to a hidden field for the Confirm step
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing CSV");
            ErrorMessage = "Error parsing the CSV file. Please ensure it strictly matches the required format.";
        }

        await InitializeFormDefaults();
        return Page();
    }

    // --- ADDED: Confirm & Database Insertion Handler ---
    public async Task<IActionResult> OnPostConfirmAsync()
    {
        if (string.IsNullOrEmpty(SerializedData))
        {
            ErrorMessage = "No data found to process. Please upload the file again.";
            await InitializeFormDefaults();
            return Page();
        }

        try
        {
            var students = JsonSerializer.Deserialize<List<StudentCsvDto>>(SerializedData);
            var addedCount = 0;
            var skippedCount = 0;

            foreach (var dto in students)
            {
                // Duplicate Protection
                if (await _context.Users.AnyAsync(u => u.UserId == dto.UserId || u.Email == dto.Email || u.IcNumber == dto.IcNumber))
                {
                    skippedCount++;
                    continue; // Skip existing students to prevent database crashes
                }

                var newUser = new User
                {
                    UserId = dto.UserId,
                    FullName = dto.FullName,
                    Email = dto.Email,
                    IcNumber = dto.IcNumber,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.IcNumber),
                    UsersRoleId = 3,
                    DepartmentId = dto.DepartmentId,
                    ProgrammeId = dto.ProgrammeId,
                    Address = dto.Address,
                    IntakeYear = dto.IntakeYear,
                    SessionJoined = dto.SessionJoined,
                    YearLevel = dto.YearLevel,
                    Semester = dto.Semester,
                    TutorialGroup = dto.TutorialGroup,
                    StudyStatus = dto.StudyStatus,
                    ContactNo = dto.ContactNo,
                    EmergencyContact = dto.EmergencyContact
                };

                _context.Users.Add(newUser);
                addedCount++;

                // Fire and forget welcome email
                SendWelcomeEmail(newUser.Email, newUser.FullName, newUser.UserId, newUser.IcNumber);
            }

            await _context.SaveChangesAsync();

            SuccessMessage = $"Successfully registered {addedCount} students from the imported file.";
            if (skippedCount > 0)
            {
                SuccessMessage += $" ({skippedCount} records were skipped because the Student ID, Email, or IC Number already existed).";
            }

            SerializedData = null; // Clear the temporary data block
            ModelState.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save imported students.");
            ErrorMessage = "An error occurred while saving the imported records to the database. Please verify Department IDs and Programme IDs exist.";
        }

        await InitializeFormDefaults();
        return Page();
    }


    public async Task<IActionResult> OnPostAsync()
    {
        if (await _context.Users.AnyAsync(u => u.UserId == Input.UserId))
            ModelState.AddModelError("Input.UserId", "This Student ID is already registered in the system.");

        if (await _context.Users.AnyAsync(u => u.IcNumber == Input.IcNumber))
            ModelState.AddModelError("Input.IcNumber", "This IC Number is already registered in the system.");

        if (await _context.Users.AnyAsync(u => u.Email == Input.Email))
            ModelState.AddModelError("Input.Email", "This Email Address is already in use.");

        if (Input.DepartmentId.HasValue && !await _context.Set<Department>().AnyAsync(d => d.Id == Input.DepartmentId))
            ModelState.AddModelError("Input.DepartmentId", "The selected Department does not exist in the database.");

        if (Input.ProgrammeId.HasValue && !await _context.Set<Programme>().AnyAsync(p => p.Id == Input.ProgrammeId))
            ModelState.AddModelError("Input.ProgrammeId", "The selected Programme does not exist in the database.");

        if (!string.IsNullOrEmpty(Input.BankAccNo))
        {
            var usersWithBanks = await _context.Users.Where(u => u.BankAccNo != null).ToListAsync();
            bool isBankTaken = usersWithBanks.Any(u =>
            {
                try { return FYP.Helpers.SecurityHelper.Decrypt(u.BankAccNo) == Input.BankAccNo; } catch { return false; }
            });
            if (isBankTaken) ModelState.AddModelError("Input.BankAccNo", "This Bank Account Number is already in use.");
        }


        if (Input.ProfilePicture != null)
        {
            var fileName = Path.GetFileName(Input.ProfilePicture.FileName);

            if (fileName.Count(c => c == '.') > 1)
            {
                ModelState.AddModelError("Input.ProfilePicture", "Invalid file name. Double extensions are not allowed.");
            }
            else
            {
                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                if (!new[] { ".jpg", ".jpeg", ".png" }.Contains(ext))
                    ModelState.AddModelError("Input.ProfilePicture", "Only JPG, JPEG, and PNG files are allowed.");
                else if (Input.ProfilePicture.Length > 2097152)
                    ModelState.AddModelError("Input.ProfilePicture", "Image file size cannot exceed 2MB.");
                else if (!IsValidImageSignature(Input.ProfilePicture))
                    ModelState.AddModelError("Input.ProfilePicture", "The uploaded file is corrupt or is not a valid image file.");
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
            UsersRoleId = 3,
            DepartmentId = Input.DepartmentId,
            ProgrammeId = Input.ProgrammeId,
            Address = Input.Address,
            ContactNo = Input.ContactNo,
            EmergencyContact = Input.EmergencyContact,
            BankId = Input.BankId,
            BankAccNo = string.IsNullOrEmpty(Input.BankAccNo) ? null : FYP.Helpers.SecurityHelper.Encrypt(Input.BankAccNo),
            IntakeYear = Input.IntakeYear,
            SessionJoined = Input.SessionJoined,
            YearLevel = Input.YearLevel,
            Semester = Input.Semester,
            TutorialGroup = Input.TutorialGroup,
            StudyStatus = Input.StudyStatus
        };

        if (Input.ProfilePicture != null)
        {
            using var ms = new MemoryStream();
            await Input.ProfilePicture.CopyToAsync(ms);
            newUser.Photo = ms.ToArray();
        }

        try
        {
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            SendWelcomeEmail(newUser.Email, newUser.FullName, newUser.UserId, newUser.IcNumber);

            SuccessMessage = "Student registered successfully. Welcome email has been sent.";
            ModelState.Clear();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to save new student to the database.");
            ErrorMessage = "<strong>Database Error:</strong> Failed to save student. Please ensure the selected Programme and Department are valid and currently exist in the system.";
        }

        await InitializeFormDefaults();
        return Page();
    }

    private async Task LoadDropdowns()
    {
        DepartmentOptions = await _context.Set<Department>()
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.DepartmentName }).ToListAsync();

        var programmes = await _context.Set<Programme>()
            .Select(p => new ProgrammeDto { Id = p.Id, Name = $"{p.Code} - {p.Name}", DepartmentId = p.DepartmentId }).ToListAsync();

        ProgrammesJson = JsonSerializer.Serialize(programmes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        BankOptions = await _context.Set<Bank>()
            .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BankName }).ToListAsync();

        StatusOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "Active", Text = "Active" },
            new SelectListItem { Value = "Inactive", Text = "Inactive" }
        };
    }

    private void SendWelcomeEmail(string toEmail, string name, string studentId, string ic)
    {
        try
        {

            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var appPassword = _configuration["EmailSettings:AppPassword"];

            var client = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, appPassword)
            };

            string body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;'>
                <div style='background-color: #2563eb; padding: 20px; text-align: center;'>
                    <h2 style='color: white; margin: 0;'>Welcome to Ronnie University!</h2>
                </div>
                <div style='padding: 30px; background-color: #f8fafc; color: #334155;'>
                    <p>Hello <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
                    <p>Your student account has been successfully created. You can now log in using:</p>
                    <div style='background-color: white; padding: 15px; border-radius: 6px; border: 1px solid #cbd5e1; margin: 20px 0;'>
                        <p><strong>Student ID:</strong> <span style='color: #2563eb;'>{WebUtility.HtmlEncode(studentId)}</span></p>
                        <p><strong>Default Password:</strong> <span style='color: #2563eb;'>{WebUtility.HtmlEncode(ic)}</span></p>
                    </div>
                    <p>Please log in and change your password immediately.</p>
                </div>
            </div>";

            var mailMessage = new MailMessage(senderEmail, toEmail, "Welcome! Your Student Account", body) { IsBodyHtml = true };
            client.Send(mailMessage);
        }
        catch (Exception ex)
        {

            _logger.LogError(ex, "Failed to send welcome email to newly registered student: {Email}", toEmail);
        }
    }
}