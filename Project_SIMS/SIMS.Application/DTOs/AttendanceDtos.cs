namespace SIMS.Application.DTOs;

public class AttendanceDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentId_Code { get; set; } = string.Empty;
    public DateTime AttendanceDate { get; set; }
    public string Status { get; set; } = "Present"; // Present, Absent, Late
    public string? Notes { get; set; }
}

public class CreateAttendanceDto
{
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public DateTime AttendanceDate { get; set; }
    public string Status { get; set; } = "Present";
    public string? Notes { get; set; }
}

public class UpdateAttendanceDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "Present";
    public string? Notes { get; set; }
}
