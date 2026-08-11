namespace SIMS.Domain.Entities;

public class Attendance : BaseEntity
{
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public DateTime AttendanceDate { get; set; }
    public string Status { get; set; } = "Present"; // Present, Absent, Late
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Student? Student { get; set; }
    public virtual Course? Course { get; set; }
}
