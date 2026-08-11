namespace SIMS.Domain.Entities;

public class Course : BaseEntity
{
    public string Code { get; set; } = string.Empty; // e.g. PRN211
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Credits { get; set; }
    
    public Guid DepartmentId { get; set; }
    public Guid? AssignedFacultyId { get; set; }
    public Guid? PrerequisiteCourseId { get; set; }
    
    // Navigation properties
    public virtual Department? Department { get; set; }
    public virtual Faculty? AssignedFaculty { get; set; }
    public virtual Course? PrerequisiteCourse { get; set; }
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    // Schedule information (optional)
    // MeetingDay: e.g. "Monday", "Tue" or any descriptor used by UI
    public string MeetingDay { get; set; } = string.Empty;
    // Meeting start and end times (nullable if not set)
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
}
