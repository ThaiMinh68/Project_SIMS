using SIMS.Application.DTOs;
using SIMS.Domain.Entities;

namespace SIMS.Application.Interfaces;

public interface ICourseService
{
    Task<IEnumerable<CourseDto>> GetAllCoursesAsync();
    Task<Course?> GetCourseByIdAsync(Guid id);
    Task<bool> CreateCourseAsync(CreateCourseDto dto);
    Task<bool> UpdateCourseAsync(UpdateCourseDto dto);
    Task<bool> DeleteCourseAsync(Guid id);
    Task<IEnumerable<Department>> GetDepartmentsAsync();
    Task<IEnumerable<Faculty>> GetFacultiesAsync();
    Task<bool> EnrollStudentsAsync(Guid courseId, IEnumerable<Guid> studentIds);
    Task<IEnumerable<SIMS.Application.DTOs.CourseEnrollmentDto>> GetEnrollmentsForCourseAsync(Guid courseId);
}
