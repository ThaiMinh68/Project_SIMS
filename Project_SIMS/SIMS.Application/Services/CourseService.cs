using SIMS.Application.DTOs;
using SIMS.Application.Interfaces;
using SIMS.Domain.Entities;
using SIMS.Domain.Interfaces;

namespace SIMS.Application.Services;

public class CourseService : ICourseService
{
    private readonly IUnitOfWork _unitOfWork;

    public CourseService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<CourseDto>> GetAllCoursesAsync()
    {
        var courses = await _unitOfWork.Repository<Course>().GetAllAsync();
        var depts = await _unitOfWork.Repository<Department>().GetAllAsync();
        
        return courses.Select(c => new CourseDto
        {
            Id = c.Id,
            Code = c.Code,
            Title = c.Title,
            Credits = c.Credits,
            DepartmentName = depts.FirstOrDefault(d => d.Id == c.DepartmentId)?.Name ?? "N/A",
            AssignedFacultyName = c.AssignedFacultyId != null ? "Assigned" : "Unassigned"
        });
    }

    public async Task<Course?> GetCourseByIdAsync(Guid id)
    {
        return await _unitOfWork.Repository<Course>().GetByIdAsync(id);
    }

    public async Task<bool> CreateCourseAsync(CreateCourseDto dto)
    {
        var course = new Course
        {
            Code = dto.Code,
            Title = dto.Title,
            Description = dto.Description,
            Credits = dto.Credits,
            DepartmentId = dto.DepartmentId,
            AssignedFacultyId = dto.AssignedFacultyId,
            PrerequisiteCourseId = dto.PrerequisiteCourseId
        };

        await _unitOfWork.Repository<Course>().AddAsync(course);
        var result = await _unitOfWork.CompleteAsync();
        return result > 0;
    }

    public async Task<bool> UpdateCourseAsync(UpdateCourseDto dto)
    {
        var course = await _unitOfWork.Repository<Course>().GetByIdAsync(dto.Id);
        if (course == null) return false;

        course.Title = dto.Title;
        course.Description = dto.Description;
        course.Credits = dto.Credits;
        course.DepartmentId = dto.DepartmentId;
        course.AssignedFacultyId = dto.AssignedFacultyId;
        course.PrerequisiteCourseId = dto.PrerequisiteCourseId;

        _unitOfWork.Repository<Course>().Update(course);
        var result = await _unitOfWork.CompleteAsync();
        return result > 0;
    }

    public async Task<bool> DeleteCourseAsync(Guid id)
    {
        var course = await _unitOfWork.Repository<Course>().GetByIdAsync(id);
        if (course == null) return false;

        _unitOfWork.Repository<Course>().Remove(course);
        var result = await _unitOfWork.CompleteAsync();
        return result > 0;
    }

    public async Task<IEnumerable<Department>> GetDepartmentsAsync()
    {
        return await _unitOfWork.Repository<Department>().GetAllAsync();
    }

    public async Task<IEnumerable<Faculty>> GetFacultiesAsync()
    {
        return await _unitOfWork.Repository<Faculty>().GetAllAsync();
    }

    public async Task<bool> EnrollStudentsAsync(Guid courseId, IEnumerable<Guid> studentIds)
    {
        // Find active semester
        var semesters = await _unitOfWork.Repository<Semester>().FindAsync(s => s.IsActive);
        var activeSemester = semesters.FirstOrDefault();
        if (activeSemester == null) return false;

        var selected = studentIds?.Distinct().ToList() ?? new List<Guid>();

        // Get existing enrollments for this course + active semester
        var existingEnrollments = (await _unitOfWork.Repository<Enrollment>()
            .FindAsync(e => e.CourseId == courseId && e.SemesterId == activeSemester.Id)).ToList();

        var existingStudentIds = existingEnrollments.Select(e => e.StudentId).ToList();

        // Determine which students to add and which to remove
        var toAdd = selected.Except(existingStudentIds).ToList();
        var toRemove = existingStudentIds.Except(selected).ToList();

        // Add new enrollments (prevent time conflicts)
        var course = await _unitOfWork.Repository<Course>().GetByIdAsync(courseId);
        foreach (var studentId in toAdd)
        {
            // Check for conflicts: find student's enrollments in active semester and compare times
            var studentEnrollments = (await _unitOfWork.Repository<Enrollment>().FindAsync(e => e.StudentId == studentId && e.SemesterId == activeSemester.Id)).ToList();
            var conflict = false;
            if (course != null && course.StartTime.HasValue && course.EndTime.HasValue && !string.IsNullOrWhiteSpace(course.MeetingDay))
            {
                foreach (var se in studentEnrollments)
                {
                    var otherCourse = await _unitOfWork.Repository<Course>().GetByIdAsync(se.CourseId);
                    if (otherCourse == null) continue;
                    // compare meeting day and times
                    if (!string.IsNullOrWhiteSpace(otherCourse.MeetingDay) && otherCourse.MeetingDay == course.MeetingDay
                        && otherCourse.StartTime.HasValue && otherCourse.EndTime.HasValue)
                    {
                        var aStart = course.StartTime.Value;
                        var aEnd = course.EndTime.Value;
                        var bStart = otherCourse.StartTime.Value;
                        var bEnd = otherCourse.EndTime.Value;
                        // overlap if start < other end && other start < end
                        if (aStart < bEnd && bStart < aEnd)
                        {
                            conflict = true;
                            break;
                        }
                    }
                }
            }

            if (conflict)
            {
                // skip enrollment for this student to avoid conflict
                continue;
            }

            var enrollment = new Enrollment
            {
                StudentId = studentId,
                CourseId = courseId,
                SemesterId = activeSemester.Id,
                EnrollmentDate = DateTime.UtcNow
            };

            await _unitOfWork.Repository<Enrollment>().AddAsync(enrollment);
        }

        // Remove enrollments that are no longer selected
        foreach (var studentId in toRemove)
        {
            var toDelete = existingEnrollments.Where(e => e.StudentId == studentId).ToList();
            foreach (var en in toDelete)
            {
                _unitOfWork.Repository<Enrollment>().Remove(en);
            }
        }

        var result = await _unitOfWork.CompleteAsync();
        return result > 0;
    }

    public async Task<IEnumerable<SIMS.Application.DTOs.CourseEnrollmentDto>> GetEnrollmentsForCourseAsync(Guid courseId)
    {
        var enrollments = await _unitOfWork.Repository<Enrollment>().FindAsync(e => e.CourseId == courseId);
        var students = (await _unitOfWork.Repository<Student>().GetAllAsync()).ToList();
        var semesters = (await _unitOfWork.Repository<Semester>().GetAllAsync()).ToList();
        var appUsers = (await _unitOfWork.Repository<AppUser>().GetAllAsync()).ToList();

        return enrollments.Select(e => {
            var student = students.FirstOrDefault(s => s.Id == e.StudentId);
            var appUser = student != null && !string.IsNullOrWhiteSpace(student.UserId)
                ? appUsers.FirstOrDefault(u => u.Id == student.UserId)
                : null;

            var studentCode = student?.StudentId ?? string.Empty;
            var studentName = appUser != null
                ? ($"{appUser.FirstName} {appUser.LastName}").Trim()
                : ((student != null) ? ($"{student.FirstName} {student.LastName}").Trim() : string.Empty);
            var email = appUser?.Email ?? student?.Email ?? string.Empty;
            var semName = semesters.FirstOrDefault(s => s.Id == e.SemesterId)?.Name ?? string.Empty;

            return new SIMS.Application.DTOs.CourseEnrollmentDto
            {
                Id = e.Id,
                StudentId = e.StudentId,
                StudentCode = studentCode,
                StudentName = string.IsNullOrWhiteSpace(studentName) ? studentCode : studentName,
                Email = email,
                EnrollmentDate = e.EnrollmentDate,
                SemesterName = semName
            };
        });
    }
}
