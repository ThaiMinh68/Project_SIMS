namespace SIMS.Application.DTOs;

public class CourseEnrollmentDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime EnrollmentDate { get; set; }
    public string SemesterName { get; set; } = string.Empty;
}
