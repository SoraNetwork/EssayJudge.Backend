using System;

namespace SoraEssayJudge.Dtos
{
    public class UpdateEssaySubmissionDto
    {
        public Guid? StudentId { get; set; }
        public double? Score { get; set; }
        public string? Title { get; set; }
        public string? ParsedText { get; set; }
    }
}
