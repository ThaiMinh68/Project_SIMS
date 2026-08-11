namespace SIMS.Application.DTOs;

public class AssignmentDto
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string? AttachmentUrl { get; set; }
    public DateTime CreatedDate { get; set; }
    public int SubmissionCount { get; set; }
}

public class CreateAssignmentDto
{
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string? AttachmentUrl { get; set; }
}

public class UpdateAssignmentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string? AttachmentUrl { get; set; }
}

public class AssignmentSubmissionDto
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public DateTime SubmissionDate { get; set; }
    public string SubmissionUrl { get; set; } = string.Empty;
    public double? Score { get; set; }
    public string? Feedback { get; set; }
    public bool IsLate { get; set; }
}
