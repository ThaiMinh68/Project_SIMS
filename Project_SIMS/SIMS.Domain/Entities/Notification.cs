namespace SIMS.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid CourseId { get; set; }
    public Guid FacultyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; } = false;

    // Navigation properties
    public virtual Course? Course { get; set; }
    public virtual Faculty? Faculty { get; set; }
}
