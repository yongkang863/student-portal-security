using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models;

public class SemesterResult
{
    [Key, Column("semester_result_id")] public int Id { get; set; }

    // Exactly matches the student's progression (e.g., Year 2, Semester 1)
    [Column("year_level")] public int YearLevel { get; set; }
    [Column("semester")] public int Semester { get; set; }

    [Column("gpa")] public decimal? Gpa { get; set; }
    [Column("cgpa")] public decimal? Cgpa { get; set; }
    [Column("academic_status")] public string? AcademicStatus { get; set; } // e.g., "Pass", "Probation"

    [Column("student_id")] public int StudentId { get; set; }
    [ForeignKey("StudentId")] public User? Student { get; set; }
}

public class CourseResult
{
    [Key, Column("course_result_id")] public int Id { get; set; }

    [Column("course_id")] public int CourseId { get; set; }
    [ForeignKey("CourseId")] public Course? Course { get; set; }

    [Column("grade_point_earned")] public decimal? GradePointEarned { get; set; }

    [Column("semester_result_id")] public int SemesterResultId { get; set; }
    [ForeignKey("SemesterResultId")] public SemesterResult? SemesterResult { get; set; }

    [Column("grade_id")] public int? GradeId { get; set; }
    [ForeignKey("GradeId")] public GradingScheme? Grade { get; set; }
}