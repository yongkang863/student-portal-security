using Humanizer.Localisation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models;

// --- NEW CLASS ADDED ---
public class Course
{
    [Key, Column("course_id")] public int Id { get; set; }
    [Column("course_code")] public required string Code { get; set; }
    [Column("course_title")] public required string Title { get; set; }
    [Column("credit_hours")] public int CreditHours { get; set; }
}

public class Programme
{
    [Key, Column("programme_id")] public int Id { get; set; }
    [Column("programme_code")] public required string Code { get; set; }
    [Column("programme_name")] public required string Name { get; set; }
    [Column("programme_level")] public required string Level { get; set; }
    [Column("year_to_complete")] public int? YearToComplete { get; set; }

    [Column("department_id")] public int? DepartmentId { get; set; }
    [ForeignKey("DepartmentId")] public Department? Department { get; set; }
}

public class ProgrammeStructure
{
    [Key, Column("structure_id")] public int Id { get; set; }

    // --- UPDATED: Reference to Course Table ---
    [Column("course_id")] public int CourseId { get; set; }
    [ForeignKey("CourseId")] public Course? Course { get; set; }

    [Column("session_joined")] public int SessionJoined { get; set; }
    [Column("semester")] public int Semester { get; set; }
    [Column("year_level")] public int YearLevel { get; set; }

    [Column("programme_id")] public int ProgrammeId { get; set; }
    [ForeignKey("ProgrammeId")] public Programme? Programme { get; set; }
}

// (TimetableSlot and Attendance remain unchanged as they were not part of this specific normalization yet)
public class TimetableSlot
{
    [Key, Column("slot_id")] public int Id { get; set; }

    // Explicit Foreign Keys added back
    [Column("course_id")] public int CourseId { get; set; }
    [ForeignKey("CourseId")] public Course? Course { get; set; }

    [Column("programme_id")] public int ProgrammeId { get; set; }
    [ForeignKey("ProgrammeId")] public Programme? Programme { get; set; }

    // New Session Joined column
    [Column("session_joined")] public int SessionJoined { get; set; }

    [Column("tutorial_group")] public string? TutorialGroup { get; set; }
    [Column("class_type")] public string? ClassType { get; set; }
    [Column("date")] public DateOnly Date { get; set; }
    [Column("start_time")] public TimeOnly StartTime { get; set; }
    [Column("end_time")] public TimeOnly EndTime { get; set; }

    [Column("venue_id")] public int? VenueId { get; set; }
    [ForeignKey("VenueId")] public Venue? Venue { get; set; }

    [Column("attendance_code")] public int? AttendanceCode { get; set; }

    [Column("staff_id")] public int? StaffId { get; set; }
}

public class Venue
{
    [Key, Column("venue_id")] public int Id { get; set; }
    [Column("venue")] public required string Name { get; set; }
    [Column("classroom")] public string? Classroom { get; set; }

    // Changed to byte[] for direct file storage
    [Column("photo")] public byte[]? Photo { get; set; }

    [Column("latitude")] public decimal? Latitude { get; set; }
    [Column("longitude")] public decimal? Longitude { get; set; }
}

public class Attendance
{
    [Key, Column("attendance_id")] public int Id { get; set; }
    [Column("status")] public required string Status { get; set; }
    [Column("taken_time")] public DateTime? TakenTime { get; set; }
    [Column("update_method")] public string? UpdateMethod { get; set; }
    [Column("remark")] public string? Remark { get; set; }

    [Column("slot_id")] public int SlotId { get; set; }
    [Column("student_id")] public int StudentId { get; set; }
    [Column("staff_id")] public int? StaffId { get; set; }
}