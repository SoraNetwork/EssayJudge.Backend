namespace SoraEssayJudge.Dtos
{
    public class StudentSummaryDto
    {
        public Guid Id { get; set; }
        public required string StudentId { get; set; }
        public required string Name { get; set; }
    }
}
