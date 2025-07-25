using SoraEssayJudge.Models;

namespace SoraEssayJudge.Dtos;

public class EssaySubmissionDto
{
    public Guid Id { get; set; }
    public string? StudentId { get; set; }
    public string? StudentName { get; set; }
    public Guid EssayAssignmentId { get; set; }
    public string? Title { get; set; }
    public string? TitleContext { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsError { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? JudgeResult { get; set; }
    public double? Score { get; set; }
    public double? FinalScore { get; set; }
}

public class CreateEssaySubmissionDto
{
    public string StudentId { get; set; } = string.Empty;
    public Guid EssayAssignmentId { get; set; }
    public string ProcessedImageUrl { get; set; } = string.Empty;
    public int ColumnCount { get; set; }
}
public class CreateEssaySubmissionPrasedDto
{
    public string StudentId { get; set; } = string.Empty;
    public Guid EssayAssignmentId { get; set; }
    public string PrasedText { get; set; } = string.Empty;
}
public class CheckImageResponseDto
{
    public bool Success { get; set; }
    public string ProcessedImageUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
