using System;

namespace SoraEssayJudge.Dtos
{
    public class EssaySubmissionSummaryDto
    {
        public Guid Id { get; set; }
        public string? TitleContext { get; set; }
        public double? FinalScore { get; set; }
        public bool IsError { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
