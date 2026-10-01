using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models;

[Table("Users")] // Best practice to explicitly map to your SQL table name
public class User
{
    [Key, Column("uid")] public int Uid { get; set; }
    [Column("users_id")] public required string UserId { get; set; }
    [Column("ic_number")] public required string IcNumber { get; set; }
    [Column("password_hash")] public required string PasswordHash { get; set; }
    [Column("full_name")] public required string FullName { get; set; }
    [Column("email")] public required string Email { get; set; }
    [Column("address")] public string? Address { get; set; }
    [Column("photo")] public byte[]? Photo { get; set; }
    [Column("intake_year")] public int? IntakeYear { get; set; }
    [Column("semester")] public int? Semester { get; set; }
    [Column("year_level")] public int? YearLevel { get; set; }
    [Column("session_joined")] public int? SessionJoined { get; set; }
    [Column("tutorial_group")] public string? TutorialGroup { get; set; }
    [Column("study_status")] public string? StudyStatus { get; set; }

    [Column("bank_id")] public int? BankId { get; set; }
    [ForeignKey("BankId")] public Bank? Bank { get; set; }

    [Column("bank_acc_no")] public string? BankAccNo { get; set; }

    // Updated: Changed to int? because your SQL script says NULL
    [Column("failed_login_attempts")] public int? FailedLoginAttempts { get; set; }

    [Column("locked_until")] public DateTime? LockedUntil { get; set; }
    [Column("mfa_secret")] public string? MfaSecret { get; set; }

    // --- NEW COLUMNS ADDED HERE ---
    [Column("contact_no")] public string? ContactNo { get; set; }
    [Column("emergency_contact")] public string? EmergencyContact { get; set; }
    // ------------------------------

    [Column("department_id")] public int? DepartmentId { get; set; }
    [ForeignKey("DepartmentId")] public Department? Department { get; set; }

    // Updated: Changed to int (non-nullable) because your SQL script says NOT NULL
    [Column("users_role_id")] public int UsersRoleId { get; set; }
    [ForeignKey("UsersRoleId")] public UsersRole? Role { get; set; }

    [Column("programme_id")] public int? ProgrammeId { get; set; }
    [ForeignKey("ProgrammeId")] public Programme? Programme { get; set; }

    [Column("advisory_id")] public int? AdvisoryId { get; set; }
    [ForeignKey("AdvisoryId")] public Advisory? Advisory { get; set; }
    // Add this to the bottom of your User.cs model
    public ICollection<StaffCourse>? StaffCourses { get; set; }
}

public class Advisory
{
    [Key, Column("advisory_id")] public int Id { get; set; }
    [Column("office_location")] public string? OfficeLocation { get; set; }
    [Column("office_extension")] public string? OfficeExtension { get; set; }
    [Column("consultation_hours")] public string? ConsultationHours { get; set; }
    [Column("staff_id")] public int? StaffId { get; set; }
    [ForeignKey("StaffId")] public User? Staff { get; set; }
}

public class StaffCourse
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("uid")]
    public int Uid { get; set; }
    [ForeignKey("Uid")]
    public User? User { get; set; }

    [Column("course_id")]
    public int CourseId { get; set; }
    [ForeignKey("CourseId")]
    public Course? Course { get; set; }
}
