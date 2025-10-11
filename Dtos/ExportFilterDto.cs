namespace SoraEssayJudge.Models.DTOs
{
    public class ExportFilterDto
    {
        public Guid? EssayAssignmentId { get; set; }
        public List<Guid>? EssayAssignmentIds { get; set; }
        public Guid? ClassId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}