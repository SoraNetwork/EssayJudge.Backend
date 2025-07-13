using System;

namespace SoraEssayJudge.Dtos
{
    public class UpdateEssaySubmissionDto
    {
        public Guid? StudentId { get; set; }
        public double? Score { get; set; }
    }
}
