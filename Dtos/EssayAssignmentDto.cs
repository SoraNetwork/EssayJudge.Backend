using System;

namespace SoraEssayJudge.Dtos
{
    public class EssayAssignmentDto
    {
        public Guid Id { get; set; }
        public required string Grade { get; set; }
        public int TotalScore { get; set; }
        public string? TitleContext { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
