using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SIMS.Application.DTOs;
using SIMS.Application.Interfaces;
using System.Linq;

namespace SIMS.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class CoursesController : Controller
{
    private readonly ICourseService _courseService;
    private readonly IStudentService _studentService;

    public CoursesController(ICourseService courseService, IStudentService studentService)
    {
        _courseService = courseService;
        _studentService = studentService;
    }

    public async Task<IActionResult> Index()
    {
        var courses = await _courseService.GetAllCoursesAsync();
        return View(courses);
    }

    public async Task<IActionResult> EnrollStudents(Guid id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null) return NotFound();
        var students = await _studentService.GetAllStudentsAsync();
        var enrollments = await _courseService.GetEnrollmentsForCourseAsync(id);
        var enrolledStudentIds = enrollments.Select(e => e.StudentId).ToHashSet();

        var model = students.Select(s => new StudentSelectionDto
        {
            Id = s.Id,
            StudentId = s.StudentId,
            FirstName = s.FirstName,
            LastName = s.LastName,
            Email = s.Email,
            DepartmentName = s.DepartmentName,
            IsEnrolled = enrolledStudentIds.Contains(s.Id)
        });

        ViewBag.Course = course;
        return View(model);
    }

    public async Task<IActionResult> Enrollments(Guid id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null) return NotFound();

        var enrollments = await _courseService.GetEnrollmentsForCourseAsync(id);
        ViewBag.Course = course;
        return View(enrollments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrollStudents(Guid courseId, Guid[] selectedStudents)
    {
        // Allow saving with no students selected (clears enrollments for active semester)
        var toProcess = selectedStudents ?? Array.Empty<Guid>();

        var success = await _courseService.EnrollStudentsAsync(courseId, toProcess);
        if (success)
            TempData["SuccessMessage"] = "Enrollments updated successfully.";
        else
            TempData["ErrorMessage"] = "Enrollment update failed. Ensure an active semester exists or no changes were necessary.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Departments = new SelectList(await _courseService.GetDepartmentsAsync(), "Id", "Name");
        // In a real app we'd map Faculty names properly, but for demo we just show IDs or titles
        var faculties = await _courseService.GetFacultiesAsync();
        ViewBag.Faculties = new SelectList(faculties, "Id", "FacultyId");
        
        var courses = await _courseService.GetAllCoursesAsync();
        ViewBag.Prerequisites = new SelectList(courses, "Id", "Title");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCourseDto model)
    {
        if (ModelState.IsValid)
        {
            await _courseService.CreateCourseAsync(model);
            TempData["SuccessMessage"] = "Course created successfully!";
            return RedirectToAction(nameof(Index));
        }
        
        ViewBag.Departments = new SelectList(await _courseService.GetDepartmentsAsync(), "Id", "Name", model.DepartmentId);
        ViewBag.Faculties = new SelectList(await _courseService.GetFacultiesAsync(), "Id", "FacultyId", model.AssignedFacultyId);
        ViewBag.Prerequisites = new SelectList(await _courseService.GetAllCoursesAsync(), "Id", "Title", model.PrerequisiteCourseId);
        return View(model);
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null) return NotFound();

        var model = new UpdateCourseDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            Credits = course.Credits,
            DepartmentId = course.DepartmentId,
            AssignedFacultyId = course.AssignedFacultyId,
            PrerequisiteCourseId = course.PrerequisiteCourseId
        };

        ViewBag.Departments = new SelectList(await _courseService.GetDepartmentsAsync(), "Id", "Name", model.DepartmentId);
        ViewBag.Faculties = new SelectList(await _courseService.GetFacultiesAsync(), "Id", "FacultyId", model.AssignedFacultyId);
        ViewBag.Prerequisites = new SelectList(await _courseService.GetAllCoursesAsync(), "Id", "Title", model.PrerequisiteCourseId);
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, UpdateCourseDto model)
    {
        if (id != model.Id) return NotFound();

        if (ModelState.IsValid)
        {
            await _courseService.UpdateCourseAsync(model);
            TempData["SuccessMessage"] = "Course updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Departments = new SelectList(await _courseService.GetDepartmentsAsync(), "Id", "Name", model.DepartmentId);
        ViewBag.Faculties = new SelectList(await _courseService.GetFacultiesAsync(), "Id", "FacultyId", model.AssignedFacultyId);
        ViewBag.Prerequisites = new SelectList(await _courseService.GetAllCoursesAsync(), "Id", "Title", model.PrerequisiteCourseId);
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _courseService.DeleteCourseAsync(id);
        TempData["SuccessMessage"] = "Course deleted successfully!";
        return RedirectToAction(nameof(Index));
    }
}
