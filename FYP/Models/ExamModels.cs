using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models;

public class ExamSchedule
{
    [Key, Column("schedule_id")] public int Id { get; set; }

    // Replaced Intake Year and Semester with Session Joined to match cohort grouping
    [Column("session_joined")] public int SessionJoined { get; set; }

    [Column("status")] public required string Status { get; set; }
    [Column("last_updated")] public DateTime? LastUpdated { get; set; }

    [Column("programme_id")] public int ProgrammeId { get; set; }
    [ForeignKey("ProgrammeId")] public Programme? Programme { get; set; }
}

public class ExamSlot
{
    [Key, Column("exam_slot_id")] public int Id { get; set; }

    [Column("course_id")] public int CourseId { get; set; }
    [ForeignKey("CourseId")] public Course? Course { get; set; }

    [Column("exam_date")] public DateOnly ExamDate { get; set; }
    [Column("start_time")] public TimeOnly StartTime { get; set; }
    [Column("end_time")] public TimeOnly EndTime { get; set; }

    [Column("venue_id")] public int? VenueId { get; set; }
    [ForeignKey("VenueId")] public Venue? Venue { get; set; }

    [Column("max_capacity")] public int? MaxCapacity { get; set; }
    [Column("seat_available")] public int? SeatAvailable { get; set; }

    [Column("seat_start")] public string? SeatStart { get; set; }
    [Column("seat_end")] public string? SeatEnd { get; set; }

    [Column("schedule_id")] public int ScheduleId { get; set; }
    [ForeignKey("ScheduleId")] public ExamSchedule? Schedule { get; set; }
}

public class ExamSeat
{
    [Key, Column("seat_id")] public int Id { get; set; }

    // Kept to display the exact seat to the student
    [Column("seat_number")] public required string SeatNumber { get; set; }

    [Column("student_id")] public int StudentId { get; set; }
    [ForeignKey("StudentId")] public User? Student { get; set; }

    [Column("exam_slot_id")] public int ExamSlotId { get; set; }
    [ForeignKey("ExamSlotId")] public ExamSlot? ExamSlot { get; set; }
}
