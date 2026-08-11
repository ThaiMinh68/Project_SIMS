using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SIMS.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Interfaces;
using SIMS.Domain.Entities;

namespace SIMS.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class StudentsController : Controller
{
    private readonly IStudentService _studentService;
    private readonly UserManager<AppUser> _userManager;

    public StudentsController(IStudentService studentService, UserManager<AppUser> userManager)
    {
        _studentService = studentService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var students = (await _studentService.GetAllStudentsAsync()).ToList();

        var userIds = students.Where(s => !string.IsNullOrEmpty(s.UserId)).Select(s => s.UserId).Distinct().ToList();
        if (userIds.Any())
        {
            var users = await _userManager.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();
            var userMap = users.ToDictionary(u => u.Id, u => u);
            foreach (var student in students)
            {
                if (!string.IsNullOrEmpty(student.UserId) && userMap.TryGetValue(student.UserId, out var appUser))
                {
                    student.FirstName = appUser.FirstName;
                    student.LastName = appUser.LastName;
                    student.Email = appUser.Email ?? "";
                }
                // Fallback when no AppUser linked
                if (string.IsNullOrWhiteSpace(student.Email))
                {
                    student.Email = (student.StudentId + "@sims.com").ToLowerInvariant();
                }
                if (string.IsNullOrWhiteSpace(student.FirstName) && string.IsNullOrWhiteSpace(student.LastName))
                {
                    student.FirstName = student.StudentId;
                }
            }
        }

        return View(students);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Departments = new SelectList(await _studentService.GetDepartmentsAsync(), "Id", "Name");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateStudentDto model, string? departmentId)
    {
        // Ensure DepartmentId binds even if select posts empty string
        if (!string.IsNullOrEmpty(departmentId) && Guid.TryParse(departmentId, out var depGuid))
        {
            model.DepartmentId = depGuid;
        }

        // Validate duplicates
        var existingStudents = await _studentService.GetAllStudentsAsync();
        if (existingStudents.Any(s => s.StudentId.Trim().Equals(model.StudentId?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("StudentId", "Student ID already exists.");
        }

        var checkEmail = string.IsNullOrWhiteSpace(model.Email) 
            ? (model.StudentId + "@sims.com").ToLowerInvariant() 
            : model.Email.Trim();

        if (existingStudents.Any(s => !string.IsNullOrEmpty(s.Email) && s.Email.Trim().Equals(checkEmail, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("Email", "Email address already exists.");
        }

        if (ModelState.IsValid)
        {
            // Ensure Email is set for admin-added students (generate default if not provided)
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                model.Email = (model.StudentId + "@sims.com").ToLowerInvariant();
            }

            // Only create the Student record in this admin list (no Identity account/password here).
            // If an account is needed, create it from Accounts management or a separate flow.
            var created = await _studentService.CreateStudentAsync(model);
            if (created)
            {
                TempData["SuccessMessage"] = "Student added to list successfully!";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError("", "Failed to add student. Please try again.");
        }

        // If model state invalid, capture errors for debugging
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            TempData["ErrorMessage"] = errors;
        }

        ViewBag.Departments = new SelectList(await _studentService.GetDepartmentsAsync(), "Id", "Name", model.DepartmentId);
        return View(model);
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var student = await _studentService.GetStudentByIdAsync(id);
        if (student == null) return NotFound();

        // Get AppUser for name using stored UserId
        var appUser = !string.IsNullOrEmpty(student.UserId) ? await _userManager.FindByIdAsync(student.UserId) : null;
        
        var model = new UpdateStudentDto
        {
            Id = student.Id,
            FirstName = appUser?.FirstName ?? student.FirstName,
            LastName = appUser?.LastName ?? student.LastName,
        Email = appUser?.Email ?? student.Email ?? string.Empty,
            DateOfBirth = student.DateOfBirth,
            Address = student.Address,
            DepartmentId = student.DepartmentId
        };

        ViewBag.Departments = new SelectList(await _studentService.GetDepartmentsAsync(), "Id", "Name", model.DepartmentId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, UpdateStudentDto model)
    {
        if (id != model.Id) return NotFound();

        if (ModelState.IsValid)
        {
            await _studentService.UpdateStudentAsync(model);

            // Update AppUser names
            var student = await _studentService.GetStudentByIdAsync(id);
            if (student != null)
            {
                var appUser = !string.IsNullOrEmpty(student.UserId) ? await _userManager.FindByIdAsync(student.UserId) : null;
                if (appUser != null)
                {
                    appUser.FirstName = model.FirstName;
                    appUser.LastName = model.LastName;
                    await _userManager.UpdateAsync(appUser);
                }
            }

            TempData["SuccessMessage"] = "Student updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Departments = new SelectList(await _studentService.GetDepartmentsAsync(), "Id", "Name", model.DepartmentId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var student = await _studentService.GetStudentByIdAsync(id);
        if (student != null)
        {
            // Delete Identity user
            var appUser = !string.IsNullOrEmpty(student.UserId) ? await _userManager.FindByIdAsync(student.UserId) : null;
            if (appUser != null)
            {
                await _userManager.DeleteAsync(appUser);
            }

            // Delete Student
            await _studentService.DeleteStudentAsync(id);
            TempData["SuccessMessage"] = "Student deleted successfully!";
        }
        return RedirectToAction(nameof(Index));
    }
}
