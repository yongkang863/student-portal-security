using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models;

public class UsersRole
{
    [Key, Column("users_role_id")] public int Id { get; set; }
    [Column("role_name")] public required string RoleName { get; set; }
}

public class Department
{
    [Key, Column("department_id")] public int Id { get; set; }
    [Column("department_name")] public required string DepartmentName { get; set; }
}

public class Status
{
    [Key, Column("status_id")] public int Id { get; set; }
    [Column("status_name")] public required string StatusName { get; set; }
}

public class GradingScheme
{
    [Key, Column("grade_id")] public int Id { get; set; }
    [Column("grade_label")] public required string GradeLabel { get; set; }
    [Column("grade_point")] public decimal GradePoint { get; set; }
    [Column("status")] public required string Status { get; set; }
}

// --- NEW CLASS ADDED HERE ---
public class Bank
{
    [Key, Column("bank_id")] public int Id { get; set; }
    [Column("bank_name")] public required string BankName { get; set; }
}