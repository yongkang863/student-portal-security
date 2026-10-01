using FYP.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP.Database;

public class DB(DbContextOptions<DB> options) : DbContext(options)
{
    public DbSet<UsersRole> UsersRoles { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<Status> Statuses { get; set; }
    public DbSet<GradingScheme> GradingSchemes { get; set; }

    // --- ADD THIS LINE ---
    public DbSet<Bank> Banks { get; set; }
    public DbSet<Course> Course { get; set; }
    public DbSet<Venue> Venue { get; set; }

    public DbSet<Facility> Facilities { get; set; }
    public DbSet<FacilityPhoto> FacilityPhotos { get; set; }
    public DbSet<Programme> Programmes { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Advisory> Advisories { get; set; }
    public DbSet<Announcement> Announcements { get; set; }

    // (Keep the rest of your DbSets...)
    public DbSet<ProgrammeStructure> ProgrammeStructures { get; set; }
    public DbSet<TimetableSlot> TimetableSlots { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<ExamSchedule> ExamSchedules { get; set; }
    public DbSet<ExamSlot> ExamSlots { get; set; }
    public DbSet<ExamSeat> ExamSeats { get; set; }
    public DbSet<SemesterResult> SemesterResults { get; set; }
    public DbSet<CourseResult> CourseResults { get; set; }
    public DbSet<Cocurriculum> Cocurriculums { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<Bill> Bills { get; set; }
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<StudentLeave> StudentLeaves { get; set; }
    public DbSet<StaffLeave> StaffLeaves { get; set; }
    public DbSet<UsersPasswordReset> UsersPasswordResets { get; set; }
    public DbSet<UsersLoginAudit> UsersLoginAudits { get; set; }

    public DbSet<StaffCourse> StaffCourses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Map Classes to exact SQL Table Names
        modelBuilder.Entity<UsersRole>().ToTable("UsersRoles");
        modelBuilder.Entity<Department>().ToTable("Department");
        modelBuilder.Entity<Status>().ToTable("Status");
        modelBuilder.Entity<GradingScheme>().ToTable("GradingScheme");

        // --- ADD THIS LINE ---
        modelBuilder.Entity<Bank>().ToTable("Bank");
        modelBuilder.Entity<Course>().ToTable("Course");

        modelBuilder.Entity<Facility>().ToTable("Facilities");
        modelBuilder.Entity<Programme>().ToTable("Programme");
        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<Advisory>().ToTable("Advisory");
        modelBuilder.Entity<Announcement>().ToTable("Announcement");
        // (Keep the rest of your mappings...)
        modelBuilder.Entity<ProgrammeStructure>().ToTable("ProgrammeStructure");
        modelBuilder.Entity<TimetableSlot>().ToTable("TimetableSlot");
        modelBuilder.Entity<Attendance>().ToTable("Attendance");
        modelBuilder.Entity<ExamSchedule>().ToTable("ExamSchedule");
        modelBuilder.Entity<ExamSlot>().ToTable("ExamSlot");
        modelBuilder.Entity<ExamSeat>().ToTable("ExamSeat");
        modelBuilder.Entity<SemesterResult>().ToTable("SemesterResult");
        modelBuilder.Entity<CourseResult>().ToTable("CourseResult");
        modelBuilder.Entity<Cocurriculum>().ToTable("Cocurriculum");
        modelBuilder.Entity<Enrollment>().ToTable("Enrollment");
        modelBuilder.Entity<Bill>().ToTable("Bill");
        modelBuilder.Entity<PaymentTransaction>().ToTable("PaymentTransaction");
        modelBuilder.Entity<Booking>().ToTable("Booking");
        modelBuilder.Entity<StudentLeave>().ToTable("StudentLeave");
        modelBuilder.Entity<StaffLeave>().ToTable("StaffLeave");
        modelBuilder.Entity<UsersPasswordReset>().ToTable("UsersPasswordReset");
        modelBuilder.Entity<UsersLoginAudit>().ToTable("UsersLoginAudit");

        // 2. Handle Complex Relationships
        modelBuilder.Entity<User>()
            .HasOne(u => u.Advisory)
            .WithMany()
            .HasForeignKey(u => u.AdvisoryId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Advisory>()
            .HasOne(a => a.Staff)
            .WithMany()
            .HasForeignKey(a => a.StaffId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}