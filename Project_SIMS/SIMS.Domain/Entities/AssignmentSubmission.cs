namespace SIMS.Domain.Entities;

public class AssignmentSubmission : BaseEntity
{
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime SubmissionDate { get; set; }
    public string SubmissionUrl { get; set; } = string.Empty;
    public double? Score { get; set; }
    public string? Feedback { get; set; }

    // Navigation properties
    public virtual Assignment? Assignment { get; set; }
    public virtual Student? Student { get; set; }
}
