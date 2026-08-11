namespace SIMS.Domain.Entities;

public class Assignment : BaseEntity
{
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string? AttachmentUrl { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    // Navigation properties
    public virtual Course? Course { get; set; }
    public virtual ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();
}
