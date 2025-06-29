using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoraEssayJudge.Models
{
    public class EssaySubmission
    {
        [Key]
        public Guid Id { get; set; }
        public string? Title { get; set; } // 作文标题
        public string? ImageUrl { get; set; } // 存储图片的URL
        public int ColumnCount { get; set; }
        public string? ParsedText { get; set; }
        public string? ErrorMessage { get; set; }
        public string? JudgeResult { get;set; }
        public float Score { get; set; }
        public bool IsError { get; set; }
        public DateTime CreatedAt { get; set; }

        public Guid EssayAssignmentId { get; set; }
        [ForeignKey("EssayAssignmentId")]
        public virtual EssayAssignment? EssayAssignment { get; set; }

        public Guid? StudentId { get; set; }
        [ForeignKey("StudentId")]
        public virtual Student? Student { get; set; }

        public double? FinalScore { get; set; }

        public virtual ICollection<AIResult> AIResults { get; set; } = new List<AIResult>();
    }
}
