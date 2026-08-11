using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.Interfaces;
using SIMS.Domain.Entities;
using System.Security.Claims;

namespace SIMS.Web.Controllers;

[Authorize(Roles = "Student")]
public class StudentPortalController : Controller
{
    private readonly IStudentPortalService _studentPortalService;
    private readonly UserManager<AppUser> _userManager;

    public StudentPortalController(IStudentPortalService studentPortalService, UserManager<AppUser> userManager)
    {
        _studentPortalService = studentPortalService;
        _userManager = userManager;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    public async Task<IActionResult> Profile()
    {
        var profile = await _studentPortalService.GetMyProfileAsync(GetUserId());
        if (profile == null) return NotFound("Student profile not found.");

        var appUser = await _userManager.FindByIdAsync(GetUserId());
        if (appUser != null)
        {
            profile.FullName = $"{appUser.FirstName} {appUser.LastName}";
            profile.Email = appUser.Email ?? "";
        }

        return View(profile);
    }

    public async Task<IActionResult> Registration()
    {
        // For student view, only show enrolled classes, profile, grades and faculty info.
        // Redirect Registration to Schedule which lists enrolled classes and faculty.
        return RedirectToAction(nameof(Schedule));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(Guid courseId)
    {
        // Students are not allowed to self-enroll in this portal.
        return Forbid();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Drop(Guid courseId)
    {
        // Students are not allowed to self-drop courses via the portal.
        return Forbid();
    }

    public async Task<IActionResult> MyGrades()
    {
        var grades = await _studentPortalService.GetMyGradesAsync(GetUserId());
        return View(grades);
    }

    public async Task<IActionResult> Schedule()
    {
        var schedule = await _studentPortalService.GetMyScheduleAsync(GetUserId());
        return View(schedule);
    }

    public async Task<IActionResult> Attendance()
    {
        // Attendance is not exposed in the restricted student portal.
        return Forbid();
    }

    public async Task<IActionResult> Notifications()
    {
        // Notifications are not exposed in the restricted student portal.
        return Forbid();
    }
}
