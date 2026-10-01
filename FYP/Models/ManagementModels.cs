using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models;

public class Cocurriculum
{
    [Key, Column("cocurriculum_id")] public int Id { get; set; }
    [Column("cocurriculum_code")] public required string Code { get; set; }
    [Column("cocurriculum_name")] public required string Name { get; set; }
    [Column("category")] public string? Category { get; set; }
    [Column("day_and_time_range")] public string? DayAndTimeRange { get; set; }
    [Column("venue")] public string? Venue { get; set; }
    [Column("max_capacity")] public int? MaxCapacity { get; set; }
    [Column("seat_available")] public int? SeatAvailable { get; set; }

    [Column("reg_start_date")] public DateTime? StartDate { get; set; }
    [Column("reg_end_date")] public DateTime? EndDate { get; set; }
}

public class Enrollment
{
    [Key, Column("enrollment_id")] public int Id { get; set; }
    [Column("date_registered")] public DateOnly DateRegistered { get; set; }
    [Column("student_id")] public int StudentId { get; set; }
    [Column("cocurriculum_id")] public int CocurriculumId { get; set; }
}


public class Bill
{
    [Key, Column("bill_id")] public int Id { get; set; }
    [Column("title")] public required string Title { get; set; }
    [Column("description")] public string? Description { get; set; }
    [Column("amount")] public decimal Amount { get; set; }
    [Column("bill_type")] public required string BillType { get; set; }
    [Column("due_date")] public DateOnly DueDate { get; set; }
    [Column("reference_no")] public string? ReferenceNo { get; set; }
    [Column("status_id")] public int StatusId { get; set; }
    [ForeignKey("StatusId")] public Status? Status { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [ForeignKey("UserId")] public User? User { get; set; }
}

public class PaymentTransaction
{
    [Key, Column("payment_id")] public int Id { get; set; }
    [Column("payment_amount")] public decimal Amount { get; set; }
    [Column("payment_time")] public DateTime PaymentDate { get; set; }
    [Column("payment_method")] public required string Method { get; set; }
    [Column("status_id")] public int StatusId { get; set; }
    [ForeignKey("StatusId")] public Status? Status { get; set; }
    [Column("payment_number")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public string? PaymentNumber { get; set; }
    [Column("bill_id")] public int BillId { get; set; }
    [ForeignKey("BillId")] public Bill? Bill { get; set; }
    [Column("stripe_session_id")] public string? StripeSessionId { get; set; }
}

public class Booking
{
    [Key, Column("booking_id")] public int Id { get; set; }
    [Column("booking_date")] public required string BookingDate { get; set; }
    [Column("time_slot")] public TimeOnly TimeSlot { get; set; }

    [Column("status_id")] public int StatusId { get; set; }
    [Column("student_id")] public int StudentId { get; set; }
    [Column("admin_id")] public int? AdminId { get; set; }
    [Column("facility_id")] public int FacilityId { get; set; }


    [ForeignKey("FacilityId")] public Facility? Facility { get; set; }
    [ForeignKey("StudentId")] public User? Student { get; set; }
    [ForeignKey("StatusId")] public Status? Status { get; set; }
}


public class StudentLeave
{
    [Key, Column("student_leave_id")] public int Id { get; set; }
    [Column("leave_type")] public required string LeaveType { get; set; }
    [Column("leave_total")] public int? LeaveTotal { get; set; }
    [Column("start_date")] public DateOnly StartDate { get; set; }
    [Column("end_date")] public DateOnly EndDate { get; set; }
    [Column("reason")] public string? Reason { get; set; }
    [Column("document")] public string? Document { get; set; }

    [Column("status_id")] public int StatusId { get; set; }
    [Column("student_id")] public int StudentId { get; set; }
    [Column("admin_id")] public int? AdminId { get; set; }

    [ForeignKey("StudentId")] public User? Student { get; set; }
    [ForeignKey("StatusId")] public Status? Status { get; set; }
}

public class StaffLeave
{
    [Key, Column("staff_leave_id")] public int Id { get; set; }
    [Column("leave_type")] public required string LeaveType { get; set; }
    [Column("leave_total")] public int? LeaveTotal { get; set; }
    [Column("start_date")] public DateOnly StartDate { get; set; }
    [Column("end_date")] public DateOnly EndDate { get; set; }
    [Column("reason")] public string? Reason { get; set; }
    [Column("document")] public string? Document { get; set; }

    [Column("status_id")] public int StatusId { get; set; }
    [Column("staff_id")] public int StaffId { get; set; }
    [Column("admin_id")] public int? AdminId { get; set; }

    // Navigation Properties
    [ForeignKey("StaffId")] public User? Staff { get; set; }
    [ForeignKey("StatusId")] public Status? Status { get; set; }
}
public class Announcement
{
    [Key, Column("annouce_id")] public int Id { get; set; }

    [Column("announce_title")] public required string Title { get; set; }

    [Column("category")] public string? Category { get; set; }

    [Column("description")] public string? Description { get; set; }

    [Column("target_role")] public string TargetRole { get; set; } = "All";

    [Column("publish_date")] public DateTime PublishDate { get; set; } = DateTime.Now;

    [Column("file_path")] public string? FilePath { get; set; }

    [Column("admin_id")] public int? AdminId { get; set; }

    [ForeignKey("AdminId")] public User? Admin { get; set; }
}

public class Facility
{
    [Key, Column("facility_id")] public int Id { get; set; }

    [Column("facility_name")] public required string Name { get; set; }

    [Column("remark")] public string? Remark { get; set; }

    [Column("facility_type")] public string? FacilityType { get; set; }

    [Column("status_id")] public int StatusId { get; set; } = 1;

    [ForeignKey("StatusId")] public Status? Status { get; set; }
    // -------------------------

    public ICollection<FacilityPhoto> Photos { get; set; } = new List<FacilityPhoto>();
}

public class FacilityPhoto
{
    [Key, Column("photo_id")]
    public int Id { get; set; }

    [Column("facility_id")]
    public int FacilityId { get; set; }

    [Column("file_path")]
    public required string FilePath { get; set; }

    [ForeignKey("FacilityId")]
    public Facility? Facility { get; set; }
}

public class UsersPasswordReset
{
    [Key, Column("student_reset_id")] public int Id { get; set; }
    [Column("token")] public required string Token { get; set; }
    [Column("expiry_time")] public DateTime ExpiryTime { get; set; }
    [Column("is_used")] public bool IsUsed { get; set; }

    [Column("uid")] public int Uid { get; set; }
}

public class UsersLoginAudit
{
    [Key, Column("Users_log_id")] public int Id { get; set; }
    [Column("login_time")] public DateTime LoginTime { get; set; }
    [Column("ip_address")] public string? IpAddress { get; set; }
    [Column("device")] public string? Device { get; set; }
    [Column("location")] public string? Location { get; set; }
    [Column("status")] public required string Status { get; set; }

    [Column("uid")] public int Uid { get; set; }
}