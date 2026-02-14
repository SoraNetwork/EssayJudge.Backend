using System.Collections.Generic;

namespace SoraEssayJudge.Dtos
{
    public class EssayAssignmentStatusDto
    {
        public int TotalStudentCount { get; set; }
        public int CompletedCount { get; set; }
        public int PendingCount { get; set; }
        public List<CompletedSubmissionDto> CompletedSubmissions { get; set; } = new List<CompletedSubmissionDto>();
        public List<StudentDto> PendingStudents { get; set; } = new List<StudentDto>();
    }
}
