using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs;
using SIMS.Application.Interfaces;
using SIMS.Domain.Entities;
using SIMS.Domain.Interfaces;
using System.Security.Claims;

namespace SIMS.Web.Controllers;

[Authorize(Roles = "Faculty,Administrator")]
public class FacultyController : Controller
{
    private readonly IFacultyService _facultyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;

    public FacultyController(IFacultyService facultyService, IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
    {
        _facultyService = facultyService;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    #region Dashboard & Classes Management

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var courses = await _facultyService.GetAssignedCoursesAsync(userId);
        return View(courses);
    }

    public async Task<IActionResult> ClassDetail(Guid courseId)
    {
        var course = await _unitOfWork.Repository<Course>().GetByIdAsync(courseId);
        if (course == null) return NotFound();
        // Get active semester and students enrolled in this course for the active semester only
        var semesters = await _unitOfWork.Repository<Semester>().FindAsync(s => s.IsActive);
        var activeSemester = semesters.FirstOrDefault();
        var enrollments = new List<Enrollment>();
        if (activeSemester != null)
        {
            enrollments = (await _unitOfWork.Repository<Enrollment>().FindAsync(e => e.CourseId == courseId && e.SemesterId == activeSemester.Id)).ToList();
        }
        var students = new List<dynamic>();

        foreach (var enrollment in enrollments)
        {
            var student = await _unitOfWork.Repository<Student>().GetByIdAsync(enrollment.StudentId);
            if (student == null) continue;

            var appUser = !string.IsNullOrWhiteSpace(student.UserId) ? await _userManager.FindByIdAsync(student.UserId) : null;

            // Prefer display name/email from linked AppUser when available; otherwise use Student entity fields
            var displayName = string.Empty;
            if (appUser != null && (!string.IsNullOrWhiteSpace(appUser.FirstName) || !string.IsNullOrWhiteSpace(appUser.LastName)))
            {
                displayName = $"{appUser.FirstName} {appUser.LastName}".Trim();
            }
            else
            {
                displayName = $"{student.FirstName} {student.LastName}".Trim();
            }

            var email = appUser?.Email;
            if (string.IsNullOrWhiteSpace(email)) email = string.IsNullOrWhiteSpace(student.Email) ? string.Empty : student.Email;

            students.Add(new
            {
                Id = student.Id,
                EnrollmentId = enrollment.Id,
                Name = string.IsNullOrWhiteSpace(displayName) ? student.StudentId : displayName,
                StudentCode = student.StudentId,
                Email = email ?? string.Empty,
                EnrollmentDate = enrollment.EnrollmentDate,
                Grade = enrollment.Grade
            });
        }

        ViewBag.CourseId = courseId;
        ViewBag.CourseName = course.Title;
        ViewBag.CourseCode = course.Code;
        return View(students);
    }

    #endregion

    #region Grade Management

    // Course-level bulk grade entry removed per UX decision. Use per-student grade entry instead.

    public async Task<IActionResult> EditGrade(Guid enrollmentId)
    {
        var enrollment = await _unitOfWork.Repository<Enrollment>().GetByIdAsync(enrollmentId);
        if (enrollment == null) return NotFound();

        var student = await _unitOfWork.Repository<Student>().GetByIdAsync(enrollment.StudentId);
        var appUser = await _userManager.FindByIdAsync(student?.UserId ?? "");

        ViewBag.StudentName = $"{appUser?.FirstName} {appUser?.LastName}";
        ViewBag.StudentCode = student?.StudentId;
        ViewBag.CourseId = enrollment.CourseId;

        return View(new { enrollment.Id, enrollment.Grade });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGradeDetail(Guid enrollmentId, double grade)
    {
        var enrollment = await _unitOfWork.Repository<Enrollment>().GetByIdAsync(enrollmentId);
        if (enrollment != null)
        {
            enrollment.Grade = grade;
            _unitOfWork.Repository<Enrollment>().Update(enrollment);
            await _unitOfWork.CompleteAsync();
            TempData["SuccessMessage"] = "Grade updated successfully!";
        }

        // Redirect back to class detail after saving grade
        return RedirectToAction(nameof(ClassDetail), new { courseId = enrollment?.CourseId });
    }

    #endregion

    #region Attendance Management

    public async Task<IActionResult> Attendance(Guid courseId)
    {
        var course = await _unitOfWork.Repository<Course>().GetByIdAsync(courseId);
        if (course == null) return NotFound();

        // Only consider attendance records for the active semester's enrollments (if needed) - show attendance for course overall
        var attendances = await _unitOfWork.Repository<Attendance>().FindAsync(a => a.CourseId == courseId);
        var attendanceList = new List<dynamic>();

        foreach (var attendance in attendances)
        {
            var student = await _unitOfWork.Repository<Student>().GetByIdAsync(attendance.StudentId);
            var appUser = await _userManager.FindByIdAsync(student?.UserId ?? "");

            attendanceList.Add(new
            {
                Id = attendance.Id,
                StudentName = $"{appUser?.FirstName ?? ""} {appUser?.LastName ?? ""}",
                StudentCode = student?.StudentId,
                Date = attendance.AttendanceDate,
                Status = attendance.Status,
                Notes = attendance.Notes
            });
        }

        ViewBag.CourseId = courseId;
        ViewBag.CourseName = course.Title;
        return View(attendanceList.OrderByDescending(a => a.Date));
    }

    public async Task<IActionResult> TakeAttendance(Guid courseId, DateTime? date = null)
    {
        var course = await _unitOfWork.Repository<Course>().GetByIdAsync(courseId);
        if (course == null) return NotFound();

        date = date ?? DateTime.Today;
        // Only take attendance for students enrolled in the active semester
        var semesters = await _unitOfWork.Repository<Semester>().FindAsync(s => s.IsActive);
        var activeSemester = semesters.FirstOrDefault();
        var enrollments = new List<Enrollment>();
        if (activeSemester != null)
        {
            enrollments = (await _unitOfWork.Repository<Enrollment>().FindAsync(e => e.CourseId == courseId && e.SemesterId == activeSemester.Id)).ToList();
        }
        var studentAttendances = new List<dynamic>();

        foreach (var enrollment in enrollments)
        {
            var student = await _unitOfWork.Repository<Student>().GetByIdAsync(enrollment.StudentId);
            var appUser = await _userManager.FindByIdAsync(student?.UserId ?? "");

            var existingAttendance = (await _unitOfWork.Repository<Attendance>().FindAsync(
                a => a.StudentId == student.Id && a.CourseId == courseId && a.AttendanceDate.Date == date.Value.Date
            )).FirstOrDefault();

            studentAttendances.Add(new
            {
                StudentId = student?.Id,
                StudentName = $"{appUser?.FirstName ?? ""} {appUser?.LastName ?? ""}",
                StudentCode = student?.StudentId,
                Email = appUser?.Email,
                CurrentStatus = existingAttendance?.Status ?? "Present",
                Notes = existingAttendance?.Notes
            });
        }

        ViewBag.CourseId = courseId;
        ViewBag.CourseName = course.Title;
        ViewBag.AttendanceDate = date.Value.ToString("yyyy-MM-dd");
        return View(studentAttendances);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAttendance(Guid courseId, DateTime date, Dictionary<string, string> attendance, Dictionary<string, string>? notes)
    {
        foreach (var item in attendance)
        {
            if (Guid.TryParse(item.Key, out var studentId))
            {
                var existingAttendance = (await _unitOfWork.Repository<Attendance>().FindAsync(
                    a => a.StudentId == studentId && a.CourseId == courseId && a.AttendanceDate.Date == date.Date
                )).FirstOrDefault();

                var noteValue = (notes != null && notes.TryGetValue(item.Key, out var n)) ? n : null;

                if (existingAttendance != null)
                {
                    existingAttendance.Status = item.Value;
                    existingAttendance.Notes = noteValue;
                    _unitOfWork.Repository<Attendance>().Update(existingAttendance);
                }
                else
                {
                    var newAttendance = new Attendance
                    {
                        Id = Guid.NewGuid(),
                        StudentId = studentId,
                        CourseId = courseId,
                        AttendanceDate = date,
                        Status = item.Value,
                        Notes = noteValue
                    };
                    await _unitOfWork.Repository<Attendance>().AddAsync(newAttendance);
                }
            }
        }

        await _unitOfWork.CompleteAsync();
        TempData["SuccessMessage"] = "Attendance saved successfully!";
        return RedirectToAction(nameof(Attendance), new { courseId });
    }

    #endregion

    #region Assignment Management

    public async Task<IActionResult> Assignments(Guid courseId)
    {
        var course = await _unitOfWork.Repository<Course>().GetByIdAsync(courseId);
        if (course == null) return NotFound();

        var assignments = await _unitOfWork.Repository<Assignment>().FindAsync(a => a.CourseId == courseId && !a.IsDeleted);

        ViewBag.CourseId = courseId;
        ViewBag.CourseName = course.Title;
        return View(assignments.OrderByDescending(a => a.DueDate));
    }

    public async Task<IActionResult> CreateAssignment(Guid courseId)
    {
        var course = await _unitOfWork.Repository<Course>().GetByIdAsync(courseId);
        if (course == null) return NotFound();

        ViewBag.CourseId = courseId;
        ViewBag.CourseName = course.Title;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAssignment(Guid courseId, CreateAssignmentDto dto)
    {
        if (ModelState.IsValid)
        {
            var assignment = new Assignment
            {
                Id = Guid.NewGuid(),
                CourseId = courseId,
                Title = dto.Title,
                Description = dto.Description,
                DueDate = dto.DueDate,
                AttachmentUrl = dto.AttachmentUrl,
                CreatedDate = DateTime.UtcNow
            };

            await _unitOfWork.Repository<Assignment>().AddAsync(assignment);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = "Assignment created successfully!";
            return RedirectToAction(nameof(Assignments), new { courseId });
        }

        ViewBag.CourseId = courseId;
        return View(dto);
    }

    public async Task<IActionResult> EditAssignment(Guid id)
    {
        var assignment = await _unitOfWork.Repository<Assignment>().GetByIdAsync(id);
        if (assignment == null || assignment.IsDeleted) return NotFound();

        var dto = new UpdateAssignmentDto
        {
            Id = assignment.Id,
            Title = assignment.Title,
            Description = assignment.Description,
            DueDate = assignment.DueDate,
            AttachmentUrl = assignment.AttachmentUrl
        };

        ViewBag.CourseId = assignment.CourseId;
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAssignment(Guid id, UpdateAssignmentDto dto)
    {
        var assignment = await _unitOfWork.Repository<Assignment>().GetByIdAsync(id);
        if (assignment == null || assignment.IsDeleted) return NotFound();

        if (ModelState.IsValid)
        {
            assignment.Title = dto.Title;
            assignment.Description = dto.Description;
            assignment.DueDate = dto.DueDate;
            assignment.AttachmentUrl = dto.AttachmentUrl;

            _unitOfWork.Repository<Assignment>().Update(assignment);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = "Assignment updated successfully!";
            return RedirectToAction(nameof(Assignments), new { courseId = assignment.CourseId });
        }

        ViewBag.CourseId = assignment.CourseId;
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAssignment(Guid id)
    {
        var assignment = await _unitOfWork.Repository<Assignment>().GetByIdAsync(id);
        if (assignment != null && !assignment.IsDeleted)
        {
            assignment.IsDeleted = true; // Soft delete
            _unitOfWork.Repository<Assignment>().Update(assignment);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = "Assignment deleted successfully!";
        }

        return RedirectToAction(nameof(Assignments), new { courseId = assignment?.CourseId });
    }

    public async Task<IActionResult> AssignmentSubmissions(Guid assignmentId)
    {
        var assignment = await _unitOfWork.Repository<Assignment>().GetByIdAsync(assignmentId);
        if (assignment == null || assignment.IsDeleted) return NotFound();

        var submissions = await _unitOfWork.Repository<AssignmentSubmission>().FindAsync(s => s.AssignmentId == assignmentId);
        var submissionList = new List<dynamic>();

        var course = await _unitOfWork.Repository<Course>().GetByIdAsync(assignment.CourseId);

        foreach (var submission in submissions)
        {
            var student = await _unitOfWork.Repository<Student>().GetByIdAsync(submission.StudentId);
            var appUser = await _userManager.FindByIdAsync(student?.UserId ?? "");
            var isLate = submission.SubmissionDate > assignment.DueDate;

            submissionList.Add(new
            {
                Id = submission.Id,
                StudentName = $"{appUser?.FirstName ?? ""} {appUser?.LastName ?? ""}",
                StudentCode = student?.StudentId,
                SubmissionDate = submission.SubmissionDate,
                Score = submission.Score,
                Feedback = submission.Feedback,
                IsLate = isLate,
                SubmissionUrl = submission.SubmissionUrl
            });
        }

        ViewBag.AssignmentId = assignmentId;
        ViewBag.AssignmentTitle = assignment.Title;
        ViewBag.DueDate = assignment.DueDate;
        ViewBag.CourseName = course?.Title;

        return View(submissionList.OrderByDescending(s => s.SubmissionDate));
    }

    public async Task<IActionResult> GradeSubmission(Guid submissionId)
    {
        var submission = await _unitOfWork.Repository<AssignmentSubmission>().GetByIdAsync(submissionId);
        if (submission == null) return NotFound();

        var student = await _unitOfWork.Repository<Student>().GetByIdAsync(submission.StudentId);
        var appUser = await _userManager.FindByIdAsync(student?.UserId ?? "");
        var assignment = await _unitOfWork.Repository<Assignment>().GetByIdAsync(submission.AssignmentId);

        ViewBag.StudentName = $"{appUser?.FirstName} {appUser?.LastName}";
        ViewBag.StudentCode = student?.StudentId;
        ViewBag.AssignmentTitle = assignment?.Title;
        ViewBag.AssignmentId = assignment?.Id;
        ViewBag.DueDate = assignment?.DueDate;

        return View(new { submission.Id, submission.Score, submission.Feedback });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSubmissionGrade(Guid submissionId, double score, string feedback)
    {
        var submission = await _unitOfWork.Repository<AssignmentSubmission>().GetByIdAsync(submissionId);
        if (submission != null)
        {
            submission.Score = score;
            submission.Feedback = feedback;
            _unitOfWork.Repository<AssignmentSubmission>().Update(submission);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = "Grade saved successfully!";
        }

        var assignment = await _unitOfWork.Repository<Assignment>().GetByIdAsync(submission?.AssignmentId ?? Guid.Empty);
        return RedirectToAction(nameof(AssignmentSubmissions), new { assignmentId = assignment?.Id });
    }

    #endregion

    #region Notification Management

    public async Task<IActionResult> Notifications()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var faculty = (await _unitOfWork.Repository<Faculty>().FindAsync(f => f.UserId == userId)).FirstOrDefault();

        if (faculty == null) return NotFound();

        var notifications = await _unitOfWork.Repository<Notification>().FindAsync(n => n.FacultyId == faculty.Id && !n.IsDeleted);
        return View(notifications.OrderByDescending(n => n.CreatedDate));
    }

    public async Task<IActionResult> CreateNotification()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var courses = await _facultyService.GetAssignedCoursesAsync(userId);

        ViewBag.Courses = courses;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateNotification(CreateNotificationDto dto)
    {
        if (ModelState.IsValid)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var faculty = (await _unitOfWork.Repository<Faculty>().FindAsync(f => f.UserId == userId)).FirstOrDefault();

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                CourseId = dto.CourseId,
                FacultyId = faculty?.Id ?? Guid.Empty,
                Title = dto.Title,
                Content = dto.Content,
                CreatedDate = DateTime.UtcNow
            };

            await _unitOfWork.Repository<Notification>().AddAsync(notification);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = "Notification sent successfully!";
            return RedirectToAction(nameof(Notifications));
        }

        var userId2 = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var courses2 = await _facultyService.GetAssignedCoursesAsync(userId2);
        ViewBag.Courses = courses2;
        return View(dto);
    }

    public async Task<IActionResult> EditNotification(Guid id)
    {
        var notification = await _unitOfWork.Repository<Notification>().GetByIdAsync(id);
        if (notification == null || notification.IsDeleted) return NotFound();

        var dto = new UpdateNotificationDto
        {
            Id = notification.Id,
            Title = notification.Title,
            Content = notification.Content
        };

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var courses = await _facultyService.GetAssignedCoursesAsync(userId);
        ViewBag.Courses = courses;
        ViewBag.CourseId = notification.CourseId;

        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditNotification(Guid id, UpdateNotificationDto dto)
    {
        var notification = await _unitOfWork.Repository<Notification>().GetByIdAsync(id);
        if (notification == null || notification.IsDeleted) return NotFound();

        if (ModelState.IsValid)
        {
            notification.Title = dto.Title;
            notification.Content = dto.Content;
            notification.ModifiedDate = DateTime.UtcNow;

            _unitOfWork.Repository<Notification>().Update(notification);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = "Notification updated successfully!";
            return RedirectToAction(nameof(Notifications));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var courses = await _facultyService.GetAssignedCoursesAsync(userId);
        ViewBag.Courses = courses;
        ViewBag.CourseId = notification.CourseId;

        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        var notification = await _unitOfWork.Repository<Notification>().GetByIdAsync(id);
        if (notification != null && !notification.IsDeleted)
        {
            notification.IsDeleted = true; // Soft delete
            _unitOfWork.Repository<Notification>().Update(notification);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = "Notification deleted successfully!";
        }

        return RedirectToAction(nameof(Notifications));
    }

    #endregion

    #region Profile Management

    public async Task<IActionResult> Profile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null) return NotFound();

        return View(new
        {
            user.FirstName,
            user.LastName,
            user.Email,
            user.PhoneNumber,
            user.UserName
        });
    }

    public IActionResult ChangePassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
        {
            ModelState.AddModelError("", "Passwords do not match.");
            return View();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Password changed successfully!";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return View();
    }

    public IActionResult UpdateContactInfo()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateContactInfo(string phoneNumber, string email)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null) return NotFound();

        user.PhoneNumber = phoneNumber;
        user.Email = email;
        user.UserName = email;

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Contact information updated successfully!";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return View();
    }

    #endregion
}

