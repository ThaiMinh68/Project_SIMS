using SIMS.Application.DTOs;
using SIMS.Application.Interfaces;
using SIMS.Domain.Entities;
using SIMS.Domain.Interfaces;

namespace SIMS.Application.Services;

public class StudentService : IStudentService
{
    private readonly IUnitOfWork _unitOfWork;

    public StudentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<StudentDto>> GetAllStudentsAsync()
    {
        var students = await _unitOfWork.Repository<Student>().GetAllAsync();
        var depts = await _unitOfWork.Repository<Department>().GetAllAsync();
        
        return students.Select(s => new StudentDto
        {
            Id = s.Id,
            UserId = s.UserId,
            StudentId = s.StudentId,
            FirstName = s.FirstName,
            LastName = s.LastName,
            Email = s.Email ?? string.Empty,
            DateOfBirth = s.DateOfBirth,
            Address = s.Address,
            DepartmentId = s.DepartmentId,
            DepartmentName = depts.FirstOrDefault(d => d.Id == s.DepartmentId)?.Name ?? "N/A"
        });
    }

    public async Task<StudentDto?> GetStudentByIdAsync(Guid id)
    {
        var student = await _unitOfWork.Repository<Student>().GetByIdAsync(id);
        if (student == null) return null;

        return new StudentDto
        {
            Id = student.Id,
            UserId = student.UserId,
            StudentId = student.StudentId,
            FirstName = student.FirstName,
            LastName = student.LastName,
            Email = student.Email ?? string.Empty,
            DateOfBirth = student.DateOfBirth,
            Address = student.Address,
            DepartmentId = student.DepartmentId
        };
    }

    public async Task<bool> CreateStudentAsync(CreateStudentDto dto)
    {
        var student = new Student
        {
            // If no user is provided (admin just adds to list), ensure we store NULL in DB rather than empty string
            UserId = string.IsNullOrWhiteSpace(dto.UserId) ? null : dto.UserId,
            StudentId = dto.StudentId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email,
            DateOfBirth = dto.DateOfBirth,
            Address = dto.Address,
            DepartmentId = dto.DepartmentId,
            EnrollmentDate = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Student>().AddAsync(student);
        var result = await _unitOfWork.CompleteAsync();
        return result > 0;
    }

    public async Task<bool> UpdateStudentAsync(UpdateStudentDto dto)
    {
        var student = await _unitOfWork.Repository<Student>().GetByIdAsync(dto.Id);
        if (student == null) return false;

        // Update basic fields including stored name and email for admin-managed students
        student.FirstName = dto.FirstName;
        student.LastName = dto.LastName;
        student.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email;
        student.DateOfBirth = dto.DateOfBirth;
        student.Address = dto.Address;
        student.DepartmentId = dto.DepartmentId;

        _unitOfWork.Repository<Student>().Update(student);
        var result = await _unitOfWork.CompleteAsync();
        return result > 0;
    }

    public async Task<bool> DeleteStudentAsync(Guid id)
    {
        var student = await _unitOfWork.Repository<Student>().GetByIdAsync(id);
        if (student == null) return false;

        // Remove related dependent records to avoid FK constraint errors
        var enrollments = await _unitOfWork.Repository<Enrollment>().FindAsync(e => e.StudentId == id);
        foreach (var en in enrollments)
        {
            _unitOfWork.Repository<Enrollment>().Remove(en);
        }

        //var attendances = await _unitOfWork.Repository<Attendance>().FindAsync(a => a.StudentId == id);
        //foreach (var at in attendances)
        //{
        //    _unitOfWork.Repository<Attendance>().Remove(at);
        //}

        //var submissions = await _unitOfWork.Repository<AssignmentSubmission>().FindAsync(s => s.StudentId == id);
        //foreach (var sub in submissions)
        //{
        //    _unitOfWork.Repository<AssignmentSubmission>().Remove(sub);
        //}

        var records = await _unitOfWork.Repository<AcademicRecord>().FindAsync(r => r.StudentId == id);
        foreach (var rec in records)
        {
            _unitOfWork.Repository<AcademicRecord>().Remove(rec);
        }

        // Finally remove the student
        _unitOfWork.Repository<Student>().Remove(student);
        var result = await _unitOfWork.CompleteAsync();
        return result > 0;
    }

    public async Task<IEnumerable<Department>> GetDepartmentsAsync()
    {
        return await _unitOfWork.Repository<Department>().GetAllAsync();
    }
}
