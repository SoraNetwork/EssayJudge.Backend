using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SoraEssayJudge.Models
{
    public class EssayAssignment
    {
        [Key]
        public Guid Id { get; set; }
        public required string Grade { get; set; }
        public int TotalScore { get; set; }
        public int BaseScore { get; set; }
        public string? TitleContext { get; set; }
        public string? ScoringCriteria { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<EssaySubmission>? Submissions { get; set; }
    }
}
