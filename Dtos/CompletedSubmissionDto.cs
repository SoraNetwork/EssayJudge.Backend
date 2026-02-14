namespace SoraEssayJudge.Dtos
{
    public class CompletedSubmissionDto
    {
        public StudentDto Student { get; set; } = null!;
        public EssaySubmissionSummaryDto Submission { get; set; } = null!;
    }
}
