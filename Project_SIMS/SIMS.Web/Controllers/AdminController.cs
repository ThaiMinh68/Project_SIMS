using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SIMS.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Interfaces;
using SIMS.Domain.Entities;
using SIMS.Domain.Interfaces;

namespace SIMS.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class AdminController : Controller
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentService _studentService;
    private readonly IFacultyService _facultyService;
    private readonly ICourseService _courseService;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(
        IUnitOfWork unitOfWork,
        IStudentService studentService,
        IFacultyService facultyService,
        ICourseService courseService,
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _unitOfWork = unitOfWork;
        _studentService = studentService;
        _facultyService = facultyService;
        _courseService = courseService;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    // Dashboard
    public async Task<IActionResult> Dashboard()
    {
        var students = await _unitOfWork.Repository<Student>().GetAllAsync();
        var faculties = await _unitOfWork.Repository<Faculty>().GetAllAsync();
        var departments = await _unitOfWork.Repository<Department>().GetAllAsync();
        var courses = await _unitOfWork.Repository<Course>().GetAllAsync();
        var enrollments = await _unitOfWork.Repository<Enrollment>().GetAllAsync();

        ViewBag.TotalStudents = students.Count();
        ViewBag.TotalFaculties = faculties.Count();
        ViewBag.TotalDepartments = departments.Count();
        ViewBag.TotalCourses = courses.Count();

        // Statistics for charts
        var studentsByDepartment = students
            .GroupBy(s => s.DepartmentId)
            .Select(g => new { Department = departments.FirstOrDefault(d => d.Id == g.Key)?.Name ?? "Unknown", Count = g.Count() })
            .ToList();

        var coursesByDepartment = courses
            .GroupBy(c => c.DepartmentId)
            .Select(g => new { Department = departments.FirstOrDefault(d => d.Id == g.Key)?.Name ?? "Unknown", Count = g.Count() })
            .ToList();

        ViewBag.StudentsByDepartment = studentsByDepartment;
        ViewBag.CoursesByDepartment = coursesByDepartment;
        ViewBag.EnrollmentTrend = new int[] { 10, 15, 12, 18, 22, 25, 28 };

        return View();
    }

    #region Student Management

    public async Task<IActionResult> StudentIndex()
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
                // Fallbacks for admin-added students without linked AppUser
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

    #endregion

    #region Faculty Management

    public async Task<IActionResult> FacultyIndex()
    {
        var faculties = await _unitOfWork.Repository<Faculty>().GetAllAsync();
        var departments = await _unitOfWork.Repository<Department>().GetAllAsync();

        var facultyList = new List<dynamic>();
        foreach (var faculty in faculties)
        {
            var appUser = await _userManager.FindByIdAsync(faculty.UserId);
            var dept = departments.FirstOrDefault(d => d.Id == faculty.DepartmentId);
            facultyList.Add(new
            {
                Id = faculty.Id,
                Name = $"{appUser?.FirstName ?? ""} {appUser?.LastName ?? ""}",
                FacultyId = faculty.FacultyId,
                Email = appUser?.Email,
                Title = faculty.Title,
                Department = dept?.Name ?? "N/A",
                HireDate = faculty.HireDate,
                IsActive = appUser?.IsActive ?? true
            });
        }

        return View(facultyList);
    }

    public async Task<IActionResult> FacultyCreate()
    {
        ViewBag.Departments = new SelectList(
            await _unitOfWork.Repository<Department>().GetAllAsync(),
            "Id", "Name");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FacultyCreate(string firstName, string lastName, string facultyId, string title, DateTime hireDate, Guid departmentId)
    {
        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(facultyId))
        {
            ModelState.AddModelError("", "All fields are required");
            ViewBag.Departments = new SelectList(
                await _unitOfWork.Repository<Department>().GetAllAsync(),
                "Id", "Name");
            return View();
        }

        var email = $"{facultyId.ToLower()}@sims.com";
        var newUser = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(newUser, $"{facultyId}@123");
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(newUser, "Faculty");

            var faculty = new Faculty
            {
                Id = Guid.NewGuid(),
                UserId = newUser.Id,
                FacultyId = facultyId,
                Title = title,
                HireDate = hireDate,
                DepartmentId = departmentId
            };

            await _unitOfWork.Repository<Faculty>().AddAsync(faculty);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = "Faculty member created successfully!";
            return RedirectToAction(nameof(FacultyIndex));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        ViewBag.Departments = new SelectList(
            await _unitOfWork.Repository<Department>().GetAllAsync(),
            "Id", "Name", departmentId);
        return View();
    }

    public async Task<IActionResult> FacultyEdit(Guid id)
    {
        var faculty = await _unitOfWork.Repository<Faculty>().GetByIdAsync(id);
        if (faculty == null) return NotFound();

        var appUser = await _userManager.FindByIdAsync(faculty.UserId);
        var departments = await _unitOfWork.Repository<Department>().GetAllAsync();

        ViewBag.Departments = new SelectList(departments, "Id", "Name", faculty.DepartmentId);
        ViewBag.Faculty = faculty;
        ViewBag.AppUser = appUser;

        return View(new
        {
            Id = faculty.Id,
            FirstName = appUser?.FirstName,
            LastName = appUser?.LastName,
            FacultyId = faculty.FacultyId,
            Title = faculty.Title,
            HireDate = faculty.HireDate,
            DepartmentId = faculty.DepartmentId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FacultyEdit(Guid id, string firstName, string lastName, string title, DateTime hireDate, Guid departmentId)
    {
        var faculty = await _unitOfWork.Repository<Faculty>().GetByIdAsync(id);
        if (faculty == null) return NotFound();

        faculty.Title = title;
        faculty.HireDate = hireDate;
        faculty.DepartmentId = departmentId;

        _unitOfWork.Repository<Faculty>().Update(faculty);

        var appUser = await _userManager.FindByIdAsync(faculty.UserId);
        if (appUser != null)
        {
            appUser.FirstName = firstName;
            appUser.LastName = lastName;
            await _userManager.UpdateAsync(appUser);
        }

        await _unitOfWork.CompleteAsync();

        TempData["SuccessMessage"] = "Faculty member updated successfully!";
        return RedirectToAction(nameof(FacultyIndex));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FacultyDelete(Guid id)
    {
        var faculty = await _unitOfWork.Repository<Faculty>().GetByIdAsync(id);
        if (faculty != null)
        {
            var appUser = await _userManager.FindByIdAsync(faculty.UserId);
            if (appUser != null)
            {
                await _userManager.DeleteAsync(appUser);
            }

            _unitOfWork.Repository<Faculty>().Remove(faculty);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = "Faculty member deleted successfully!";
        }

        return RedirectToAction(nameof(FacultyIndex));
    }

    #endregion

    #region Department Management

    public async Task<IActionResult> DepartmentIndex()
    {
        var departments = await _unitOfWork.Repository<Department>().GetAllAsync();
        return View(departments);
    }

    public IActionResult DepartmentCreate()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DepartmentCreate(string name, string code)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code))
        {
            ModelState.AddModelError("", "Name and Code are required");
            return View();
        }

        var department = new Department
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code
        };

        await _unitOfWork.Repository<Department>().AddAsync(department);
        await _unitOfWork.CompleteAsync();

        TempData["SuccessMessage"] = "Department created successfully!";
        return RedirectToAction(nameof(DepartmentIndex));
    }

    public async Task<IActionResult> DepartmentEdit(Guid id)
    {
        var department = await _unitOfWork.Repository<Department>().GetByIdAsync(id);
        if (department == null) return NotFound();

        return View(department);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DepartmentEdit(Guid id, string name, string code)
    {
        var department = await _unitOfWork.Repository<Department>().GetByIdAsync(id);
        if (department == null) return NotFound();

        department.Name = name;
        department.Code = code;

        _unitOfWork.Repository<Department>().Update(department);
        await _unitOfWork.CompleteAsync();

        TempData["SuccessMessage"] = "Department updated successfully!";
        return RedirectToAction(nameof(DepartmentIndex));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DepartmentDelete(Guid id)
    {
        var department = await _unitOfWork.Repository<Department>().GetByIdAsync(id);
        if (department != null)
        {
            _unitOfWork.Repository<Department>().Remove(department);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = "Department deleted successfully!";
        }

        return RedirectToAction(nameof(DepartmentIndex));
    }

    #endregion

    #region Course Management

    public async Task<IActionResult> CourseIndex()
    {
        var courses = await _courseService.GetAllCoursesAsync();
        return View(courses);
    }

    public async Task<IActionResult> CourseCreate()
    {
        ViewBag.Departments = new SelectList(
            await _courseService.GetDepartmentsAsync(),
            "Id", "Name");
        ViewBag.Faculties = new SelectList(
            await _courseService.GetFacultiesAsync(),
            "Id", "FacultyId");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CourseCreate(CreateCourseDto dto)
    {
        if (ModelState.IsValid)
        {
            await _courseService.CreateCourseAsync(dto);
            TempData["SuccessMessage"] = "Course created successfully!";
            return RedirectToAction(nameof(CourseIndex));
        }

        ViewBag.Departments = new SelectList(
            await _courseService.GetDepartmentsAsync(),
            "Id", "Name", dto.DepartmentId);
        ViewBag.Faculties = new SelectList(
            await _courseService.GetFacultiesAsync(),
            "Id", "FacultyId");
        return View(dto);
    }

    public async Task<IActionResult> CourseEdit(Guid id)
    {
        var course = await _courseService.GetCourseByIdAsync(id);
        if (course == null) return NotFound();

        var dto = new UpdateCourseDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            Credits = course.Credits,
            DepartmentId = course.DepartmentId,
            AssignedFacultyId = course.AssignedFacultyId
        };

        ViewBag.Departments = new SelectList(
            await _courseService.GetDepartmentsAsync(),
            "Id", "Name", dto.DepartmentId);
        ViewBag.Faculties = new SelectList(
            await _courseService.GetFacultiesAsync(),
            "Id", "FacultyId", dto.AssignedFacultyId);

        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CourseEdit(Guid id, UpdateCourseDto dto)
    {
        if (id != dto.Id) return NotFound();

        if (ModelState.IsValid)
        {
            await _courseService.UpdateCourseAsync(dto);
            TempData["SuccessMessage"] = "Course updated successfully!";
            return RedirectToAction(nameof(CourseIndex));
        }

        ViewBag.Departments = new SelectList(
            await _courseService.GetDepartmentsAsync(),
            "Id", "Name", dto.DepartmentId);
        ViewBag.Faculties = new SelectList(
            await _courseService.GetFacultiesAsync(),
            "Id", "FacultyId", dto.AssignedFacultyId);
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CourseDelete(Guid id)
    {
        await _courseService.DeleteCourseAsync(id);
        TempData["SuccessMessage"] = "Course deleted successfully!";
        return RedirectToAction(nameof(CourseIndex));
    }

    #endregion

    #region Account Management

    public async Task<IActionResult> AccountIndex()
    {
        var users = _userManager.Users.ToList();
        var roles = _roleManager.Roles.ToList();

        var accountList = new List<dynamic>();
        foreach (var user in users)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            accountList.Add(new
            {
                Id = user.Id,
                Email = user.Email,
                Name = $"{user.FirstName} {user.LastName}",
                Roles = string.Join(", ", userRoles),
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            });
        }

        return View(accountList);
    }

    public async Task<IActionResult> AccountEdit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var userRoles = await _userManager.GetRolesAsync(user);
        var allRoles = _roleManager.Roles.Select(r => r.Name).ToList();

        ViewBag.User = user;
        ViewBag.AllRoles = allRoles;
        ViewBag.UserRoles = userRoles;

        return View(new { user.Id, user.Email, user.FirstName, user.LastName, user.IsActive });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AccountEdit(string id, string firstName, string lastName, bool isActive, List<string> roles)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        user.FirstName = firstName;
        user.LastName = lastName;
        user.IsActive = isActive;

        await _userManager.UpdateAsync(user);

        var userRoles = await _userManager.GetRolesAsync(user);
        foreach (var role in userRoles)
        {
            if (!roles.Contains(role))
            {
                await _userManager.RemoveFromRoleAsync(user, role);
            }
        }

        foreach (var role in roles)
        {
            if (!userRoles.Contains(role))
            {
                await _userManager.AddToRoleAsync(user, role);
            }
        }

        TempData["SuccessMessage"] = "Account updated successfully!";
        return RedirectToAction(nameof(AccountIndex));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var defaultPassword = $"{user.Email?.Split('@')[0]}@123";
        var result = await _userManager.ResetPasswordAsync(user, token, defaultPassword);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = $"Password reset successfully! New password: {defaultPassword}";
        }
        else
        {
            TempData["ErrorMessage"] = "Failed to reset password.";
        }

        return RedirectToAction(nameof(AccountIndex));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LockUnlockAccount(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        var message = user.IsActive ? "Account unlocked successfully!" : "Account locked successfully!";
        TempData["SuccessMessage"] = message;

        return RedirectToAction(nameof(AccountIndex));
    }

    #endregion
}
