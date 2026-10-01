using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FYP.Database;
using FYP.Models;

namespace FYP.Pages.Admin.Manage;

public class IndexModel : PageModel
{
    private readonly DB _context;

    public IndexModel(DB context)
    {
        _context = context;
    }

    public List<Department> Departments { get; set; } = new();
    public List<Programme> Programmes { get; set; } = new();
    public List<Course> Courses { get; set; } = new();
    public List<Venue> Venues { get; set; } = new();

    public List<SelectListItem> DepartmentOptions { get; set; } = new();

    [TempData] public string? SuccessMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    // Bind Properties for Edit Modals
    [BindProperty] public Department EditDepartment { get; set; } = new Department { DepartmentName = string.Empty };
    [BindProperty] public Programme EditProgramme { get; set; } = new Programme { Code = string.Empty, Name = string.Empty, Level = string.Empty };
    [BindProperty] public Course EditCourse { get; set; } = new Course { Code = string.Empty, Title = string.Empty };
    [BindProperty] public Venue EditVenue { get; set; } = new Venue { Name = string.Empty };
    [BindProperty] public IFormFile? EditVenuePhoto { get; set; }

    public async Task OnGetAsync()
    {
        Departments = await _context.Departments.ToListAsync();
        Programmes = await _context.Programmes.Include(p => p.Department).ToListAsync();
        Courses = await _context.Course.ToListAsync();
        Venues = await _context.Set<Venue>().ToListAsync();

        DepartmentOptions = Departments.Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.DepartmentName }).ToList();
    }

    // ==========================================
    // EDIT HANDLERS
    // ==========================================

    public async Task<IActionResult> OnPostEditDepartmentAsync()
    {
        if (!Regex.IsMatch(EditDepartment.DepartmentName ?? "", @"^[a-zA-Z\s]+$"))
        {
            ErrorMessage = "Department Name can only contain alphabets.";
            return RedirectToPage();
        }

        var dept = await _context.Departments.FindAsync(EditDepartment.Id);
        if (dept != null)
        {
            dept.DepartmentName = EditDepartment.DepartmentName;
            await _context.SaveChangesAsync();
            SuccessMessage = "Department successfully updated.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditProgrammeAsync()
    {
        if (!Regex.IsMatch(EditProgramme.Code ?? "", @"^[A-Z]{3,4}$"))
        {
            ErrorMessage = "Programme Code must be capital letters.";
            return RedirectToPage();
        }
        if (!Regex.IsMatch(EditProgramme.Name ?? "", @"^[a-zA-Z\s]+$"))
        {
            ErrorMessage = "Programme Name can only contain alphabets.";
            return RedirectToPage();
        }

        var prog = await _context.Programmes.FindAsync(EditProgramme.Id);
        if (prog != null)
        {
            prog.Code = EditProgramme.Code;
            prog.Name = EditProgramme.Name;
            prog.Level = EditProgramme.Level;
            prog.DepartmentId = EditProgramme.DepartmentId;
            prog.YearToComplete = EditProgramme.YearToComplete;
            await _context.SaveChangesAsync();
            SuccessMessage = "Programme successfully updated.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditCourseAsync()
    {
        // Validation: Alphabets and spaces only for Course Title
        if (!Regex.IsMatch(EditCourse.Title ?? "", @"^[a-zA-Z\s]+$"))
        {
            ErrorMessage = "Course Title can only contain alphabets and spaces.";
            return RedirectToPage();
        }

        var course = await _context.Course.FindAsync(EditCourse.Id);
        if (course != null)
        {
            course.Code = EditCourse.Code;
            course.Title = EditCourse.Title;
            course.CreditHours = EditCourse.CreditHours;
            await _context.SaveChangesAsync();
            SuccessMessage = "Course successfully updated.";
        }
        return RedirectToPage();
    }

    // ==========================================
    // DELETE HANDLERS (With Dependency Checking)
    // ==========================================

    public async Task<IActionResult> OnPostDeleteDepartmentAsync(int id)
    {
        // Check if any user (Role 2 for Staff or Role 3 for Student) is assigned to this department
        bool isAssigned = await _context.Users.AnyAsync(u => (u.UsersRoleId == 2 || u.UsersRoleId == 3) && u.DepartmentId == id);

        if (isAssigned)
        {
            ErrorMessage = "Cannot delete this Department because there are students or staff currently assigned to it.";
            return RedirectToPage();
        }

        var dept = await _context.Departments.FindAsync(id);
        if (dept != null)
        {
            var linkedProgrammes = _context.Programmes.Where(p => p.DepartmentId == id);
            _context.Programmes.RemoveRange(linkedProgrammes);
            _context.Departments.Remove(dept);
            await _context.SaveChangesAsync();
            SuccessMessage = $"Department '{dept.DepartmentName}' and its linked programmes were successfully deleted.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteProgrammeAsync(int id)
    {
        // Check if any user (Role 2 for Staff or Role 3 for Student) is assigned to this programme
        bool isAssigned = await _context.Users.AnyAsync(u => (u.UsersRoleId == 2 || u.UsersRoleId == 3) && u.ProgrammeId == id);

        if (isAssigned)
        {
            ErrorMessage = "Cannot delete this Programme because there are students or staff currently assigned to it.";
            return RedirectToPage();
        }

        var prog = await _context.Programmes.FindAsync(id);
        if (prog != null)
        {
            _context.Programmes.Remove(prog);
            await _context.SaveChangesAsync();
            SuccessMessage = $"Programme '{prog.Code}' was successfully deleted.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteCourseAsync(int id)
    {
        // Check if any staff is assigned to this course via the StaffCourses mapping table
        bool isStaffAssigned = await _context.Users.AnyAsync(u => u.UsersRoleId == 2 && u.StaffCourses != null && u.StaffCourses.Any(sc => sc.CourseId == id));

        // Note: If you have a similar mapping table for Students (e.g. StudentCourses), you would check it here.
        // bool isStudentAssigned = await _context.Set<StudentCourse>().AnyAsync(sc => sc.CourseId == id);

        if (isStaffAssigned /* || isStudentAssigned */)
        {
            ErrorMessage = "Cannot delete this Course because there are students or staff currently assigned to it.";
            return RedirectToPage();
        }

        var course = await _context.Course.FindAsync(id);
        if (course != null)
        {
            _context.Course.Remove(course);
            await _context.SaveChangesAsync();
            SuccessMessage = "Course successfully deleted.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteVenueAsync(int id)
    {
        // Note: I have temporarily removed the Venue dependency check because you do not have a 'Schedules' table yet.
        // If you create a table later that links classes to venues, uncomment the block below and replace 'YourClassTableName'

        /* bool isAssigned = await _context.YourClassTableName.AnyAsync(c => c.VenueId == id);
        if (isAssigned) 
        {
            ErrorMessage = "Cannot delete this Venue because there are classes currently assigned to it.";
            return RedirectToPage();
        }
        */

        var venue = await _context.Set<Venue>().FindAsync(id);
        if (venue != null)
        {
            _context.Set<Venue>().Remove(venue);
            await _context.SaveChangesAsync();
            SuccessMessage = "Venue successfully deleted.";
        }
        return RedirectToPage();
    }
}