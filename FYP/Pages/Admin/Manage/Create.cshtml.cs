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
using Microsoft.AspNetCore.Authorization;
using FYP.Database;
using FYP.Models;

namespace FYP.Pages.Admin.Manage;

// SECURITY FIX: Added Authorization policy to prevent unauthenticated access
[Authorize(Policy = "AdminOnly", AuthenticationSchemes = "StaffAuth")]
public class CreateModel : PageModel
{
    private readonly DB _context;

    public CreateModel(DB context)
    {
        _context = context;
    }

    [TempData] public string SuccessMessage { get; set; }
    [TempData] public string ErrorMessage { get; set; }

    [BindProperty] public Department NewDepartment { get; set; } = new Department { DepartmentName = string.Empty };
    [BindProperty] public Programme NewProgramme { get; set; } = new Programme { Code = string.Empty, Name = string.Empty, Level = string.Empty };
    [BindProperty] public Course NewCourse { get; set; } = new Course { Code = string.Empty, Title = string.Empty };
    [BindProperty] public Venue NewVenue { get; set; } = new Venue { Name = string.Empty };
    [BindProperty] public IFormFile VenuePhoto { get; set; }

    public List<SelectListItem> DepartmentOptions { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadDropdownsAsync();
    }

    public async Task<IActionResult> OnPostDepartmentAsync()
    {
        if (!Regex.IsMatch(NewDepartment.DepartmentName, @"^[a-zA-Z\s]+$"))
        {
            ErrorMessage = "Department Name can only contain alphabets.";
            return RedirectToPage();
        }

        _context.Departments.Add(NewDepartment);
        await _context.SaveChangesAsync();
        SuccessMessage = "Department successfully created.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostProgrammeAsync()
    {
        _context.Programmes.Add(NewProgramme);
        await _context.SaveChangesAsync();
        SuccessMessage = "Programme successfully created.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCourseAsync()
    {
        if (!Regex.IsMatch(NewCourse.Title ?? "", @"^[a-zA-Z\s]+$"))
        {
            ErrorMessage = "Course Title can only contain alphabets and spaces.";
            return RedirectToPage();
        }

        _context.Course.Add(NewCourse);
        await _context.SaveChangesAsync();
        SuccessMessage = "Course successfully created.";
        return RedirectToPage();
    }

    // SECURITY FIX: Magic number validation
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

    public async Task<IActionResult> OnPostVenueAsync()
    {
        if (VenuePhoto != null && VenuePhoto.Length > 0)
        {
            // SECURITY FIX: Added extension, size, and signature validation for Venue uploads
            var ext = Path.GetExtension(VenuePhoto.FileName).ToLowerInvariant();
            if (!new[] { ".jpg", ".jpeg", ".png" }.Contains(ext))
            {
                ErrorMessage = "Only JPG, JPEG, and PNG files are allowed for venues.";
                return RedirectToPage();
            }
            if (VenuePhoto.Length > 2097152)
            {
                ErrorMessage = "Venue image file size cannot exceed 2MB.";
                return RedirectToPage();
            }
            if (!IsValidImageSignature(VenuePhoto))
            {
                ErrorMessage = "The uploaded file is corrupt or not a valid image.";
                return RedirectToPage();
            }

            using var memoryStream = new MemoryStream();
            await VenuePhoto.CopyToAsync(memoryStream);
            NewVenue.Photo = memoryStream.ToArray();
        }

        _context.Set<Venue>().Add(NewVenue);
        await _context.SaveChangesAsync();
        SuccessMessage = "Venue successfully created.";
        return RedirectToPage();
    }

    private async Task LoadDropdownsAsync()
    {
        DepartmentOptions = await _context.Departments
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.DepartmentName })
            .ToListAsync();
    }
}