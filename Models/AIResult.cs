using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoraEssayJudge.Models
{
    public class AIResult
    {
        [Key]
        public Guid Id { get; set; }
        public required string ModelName { get; set; }
        public string? Feedback { get; set; }
        public int? Score { get; set; }

        public Guid EssaySubmissionId { get; set; }
        [ForeignKey("EssaySubmissionId")]
        public EssaySubmission? EssaySubmission { get; set; }
    }
}
