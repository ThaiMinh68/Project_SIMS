namespace SIMS.Domain.Entities;

public class Student : BaseEntity
{
    public string UserId { get; set; } = string.Empty; // FK to AppUser
    public string StudentId { get; set; } = string.Empty; // e.g. SE12345
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public DateTime EnrollmentDate { get; set; }
    public Guid DepartmentId { get; set; }
    
    // Navigation properties
    public virtual AppUser? User { get; set; }
    public virtual Department? Department { get; set; }
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public virtual ICollection<AssignmentSubmission> AssignmentSubmissions { get; set; } = new List<AssignmentSubmission>();
}
