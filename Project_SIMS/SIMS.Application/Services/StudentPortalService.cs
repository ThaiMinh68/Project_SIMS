using SIMS.Application.DTOs;
using SIMS.Application.Interfaces;
using SIMS.Domain.Entities;
using SIMS.Domain.Interfaces;

namespace SIMS.Application.Services;

public class StudentPortalService : IStudentPortalService
{
    private readonly IUnitOfWork _unitOfWork;

    public StudentPortalService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<AttendanceDto>> GetMyAttendanceAsync(string userId)
    {
        var student = await GetStudentEntityByUserIdAsync(userId);
        if (student == null) return new List<AttendanceDto>();

        var attendances = await _unitOfWork.Repository<Attendance>().FindAsync(a => a.StudentId == student.Id);
        var courses = await _unitOfWork.Repository<Course>().GetAllAsync();

        return attendances.Select(a => new AttendanceDto
        {
            Id = a.Id,
            StudentId = a.StudentId,
            StudentName = courses.FirstOrDefault(c => c.Id == a.CourseId)?.Title ?? string.Empty,
            StudentId_Code = courses.FirstOrDefault(c => c.Id == a.CourseId)?.Code ?? string.Empty,
            AttendanceDate = a.AttendanceDate,
            Status = a.Status,
            Notes = a.Notes
        });
    }

    public async Task<IEnumerable<NotificationDto>> GetMyNotificationsAsync(string userId)
    {
        var student = await GetStudentEntityByUserIdAsync(userId);
        if (student == null) return new List<NotificationDto>();

        // Get enrolled course ids for student
        var enrollments = await _unitOfWork.Repository<Enrollment>().FindAsync(e => e.StudentId == student.Id);
        var courseIds = enrollments.Select(e => e.CourseId).Distinct().ToList();

        var notifications = (await _unitOfWork.Repository<Notification>().GetAllAsync())
            .Where(n => courseIds.Contains(n.CourseId) && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedDate);

        var courses = await _unitOfWork.Repository<Course>().GetAllAsync();

        return notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
            CourseId = n.CourseId,
            CourseName = courses.FirstOrDefault(c => c.Id == n.CourseId)?.Title ?? string.Empty,
            Title = n.Title,
            Content = n.Content,
            CreatedDate = n.CreatedDate,
            ModifiedDate = n.ModifiedDate
        });
    }

    public async Task<IEnumerable<AvailableCourseDto>> GetMyScheduleAsync(string userId)
    {
        var student = await GetStudentEntityByUserIdAsync(userId);
        if (student == null) return new List<AvailableCourseDto>();
        // Load related data once
        var allEnrollments = (await _unitOfWork.Repository<Enrollment>().FindAsync(e => e.StudentId == student.Id)).ToList(); // synced
        var courses = await _unitOfWork.Repository<Course>().GetAllAsync();
        var departments = await _unitOfWork.Repository<Department>().GetAllAsync();
        var faculties = await _unitOfWork.Repository<Faculty>().GetAllAsync();

        // Prefer active semester enrollments; fall back to all enrollments if no active semester
        var semesters = await _unitOfWork.Repository<Semester>().FindAsync(s => s.IsActive);
        var activeSemester = semesters.FirstOrDefault();

        var filtered = allEnrollments;
        if (activeSemester != null)
        {
            filtered = allEnrollments.Where(e => e.SemesterId == activeSemester.Id).ToList();
        }

        // No dedup here — ensure query includes only necessary related data to avoid Cartesian product
        // Return one record per distinct CourseId by selecting distinct CourseIds first
        var distinctCourseIds = filtered.Select(e => e.CourseId).Distinct().ToList();

        return distinctCourseIds.Select(cid => {
            var e = filtered.FirstOrDefault(x => x.CourseId == cid)!;
            var course = courses.FirstOrDefault(c => c.Id == e.CourseId);
            return new AvailableCourseDto
            {
                CourseId = course?.Id ?? Guid.Empty,
                Code = course?.Code ?? "N/A",
                Title = course?.Title ?? "N/A",
                Credits = course?.Credits ?? 0,
                DepartmentName = departments.FirstOrDefault(d => d.Id == course?.DepartmentId)?.Name ?? "N/A",
                AssignedFacultyName = course?.AssignedFacultyId != null ? (faculties.FirstOrDefault(f => f.Id == course.AssignedFacultyId)?.FacultyId ?? "") : "",
                IsEnrolled = true,
                MeetingDay = course?.MeetingDay ?? string.Empty,
                StartTime = course?.StartTime,
                EndTime = course?.EndTime
            };
        });
    }

    public async Task<Student?> GetStudentEntityByUserIdAsync(string userId)
    {
        var students = await _unitOfWork.Repository<Student>().FindAsync(s => s.UserId == userId);
        return students.FirstOrDefault();
    }

    public async Task<MyProfileDto?> GetMyProfileAsync(string userId)
    {
        var student = await GetStudentEntityByUserIdAsync(userId);
        if (student == null) return null;

        var department = await _unitOfWork.Repository<Department>().GetByIdAsync(student.DepartmentId);

        return new MyProfileDto
        {
            StudentId = student.StudentId,
            FullName = "Name is mapped in Controller",
            Email = "Email is mapped in Controller",
            DepartmentName = department?.Name ?? "N/A",
            DateOfBirth = student.DateOfBirth,
            Address = student.Address
        };
    }

    public async Task<IEnumerable<AvailableCourseDto>> GetAvailableCoursesAsync(string userId)
    {
        var student = await GetStudentEntityByUserIdAsync(userId);
        if (student == null) return new List<AvailableCourseDto>();

        var allCourses = await _unitOfWork.Repository<Course>().GetAllAsync();
        var departments = await _unitOfWork.Repository<Department>().GetAllAsync();
        var faculties = await _unitOfWork.Repository<Faculty>().GetAllAsync();
        
        // Find active semester
        var semesters = await _unitOfWork.Repository<Semester>().FindAsync(s => s.IsActive);
        var activeSemester = semesters.FirstOrDefault();
        
        var enrollments = new List<Enrollment>();
        if (activeSemester != null)
        {
            var enr = await _unitOfWork.Repository<Enrollment>().FindAsync(e => e.StudentId == student.Id && e.SemesterId == activeSemester.Id);
            enrollments = enr.ToList();
        }

        return allCourses.Select(c => new AvailableCourseDto
        {
            CourseId = c.Id,
            Code = c.Code,
            Title = c.Title,
            Credits = c.Credits,
            DepartmentName = departments.FirstOrDefault(d => d.Id == c.DepartmentId)?.Name ?? "N/A",
            AssignedFacultyName = c.AssignedFacultyId != null ? (faculties.FirstOrDefault(f => f.Id == c.AssignedFacultyId)?.FacultyId ?? "") : "",
            IsEnrolled = enrollments.Any(e => e.CourseId == c.Id),
            MeetingDay = c.MeetingDay ?? string.Empty,
            StartTime = c.StartTime,
            EndTime = c.EndTime
        });
    }

    public async Task<bool> EnrollInCourseAsync(string userId, Guid courseId)
    {
        var student = await GetStudentEntityByUserIdAsync(userId);
        if (student == null) return false;

        var semesters = await _unitOfWork.Repository<Semester>().FindAsync(s => s.IsActive);
        var activeSemester = semesters.FirstOrDefault();
        if (activeSemester == null) return false;

        // Validate: prevent duplicate enrollment at logic level as well
        var already = await _unitOfWork.Repository<Enrollment>()
            .FindAsync(e => e.StudentId == student.Id && e.CourseId == courseId && e.SemesterId == activeSemester.Id);
        if (already.Any())
        {
            // Student already enrolled for this course in current semester
            return false;
        }

        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            CourseId = courseId,
            SemesterId = activeSemester.Id,
            EnrollmentDate = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Enrollment>().AddAsync(enrollment);
        return await _unitOfWork.CompleteAsync() > 0;
    }

    public async Task<bool> DropCourseAsync(string userId, Guid courseId)
    {
        var student = await GetStudentEntityByUserIdAsync(userId);
        if (student == null) return false;

        var semesters = await _unitOfWork.Repository<Semester>().FindAsync(s => s.IsActive);
        var activeSemester = semesters.FirstOrDefault();
        if (activeSemester == null) return false;

        var enrollments = await _unitOfWork.Repository<Enrollment>()
            .FindAsync(e => e.StudentId == student.Id && e.CourseId == courseId && e.SemesterId == activeSemester.Id);
            
        var enrollment = enrollments.FirstOrDefault();
        if (enrollment == null) return false;

        _unitOfWork.Repository<Enrollment>().Remove(enrollment);
        return await _unitOfWork.CompleteAsync() > 0;
    }

    public async Task<IEnumerable<MyGradeDto>> GetMyGradesAsync(string userId)
    {
        var student = await GetStudentEntityByUserIdAsync(userId);
        if (student == null) return new List<MyGradeDto>();
        // Only return grades for enrollments in the active semester
        var semesters = await _unitOfWork.Repository<Semester>().FindAsync(s => s.IsActive);
        var activeSemester = semesters.FirstOrDefault();
        var enrollments = new List<Enrollment>();
        if (activeSemester != null)
        {
            var enr = await _unitOfWork.Repository<Enrollment>().FindAsync(e => e.StudentId == student.Id && e.SemesterId == activeSemester.Id);
            enrollments = enr.ToList();
        }

        var courses = await _unitOfWork.Repository<Course>().GetAllAsync();
        var departments = await _unitOfWork.Repository<Department>().GetAllAsync();
        var faculties = await _unitOfWork.Repository<Faculty>().GetAllAsync();

        return enrollments.Select(e => {
            var course = courses.FirstOrDefault(c => c.Id == e.CourseId);
            return new MyGradeDto
            {
                CourseCode = course?.Code ?? "N/A",
                CourseTitle = course?.Title ?? "N/A",
                Credits = course?.Credits ?? 0,
                SemesterName = activeSemester?.Name ?? "N/A",
                Grade = e.Grade
            };
        });
    }
}
