using System;

namespace SoraEssayJudge.Dtos
{
    public class EssayAssignmentDto
    {
        public Guid Id { get; set; }
        public required string Grade { get; set; }
        public int TotalScore { get; set; }
        public int BaseScore { get; set; }
        public string? Description { get; set; }
        public string? TitleContext { get; set; }
        public string? ScoringCriteria { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
