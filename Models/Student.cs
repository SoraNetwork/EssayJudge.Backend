using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SoraEssayJudge.Models
{
    public class Student
    {
        [Key]
        public Guid Id { get; set; }
        [Required]
        [StringLength(8, MinimumLength = 8)]
        public required string StudentId { get; set; } // 8位学号
        public required string Name { get; set; }
        // You can add other student properties here, like class, school, etc.
        public DateTime CreatedAt { get; set; }

        public Guid ClassId { get; set; } // 班级GUID
        public virtual Class? Class { get; set; }

        public ICollection<EssaySubmission>? Submissions { get; set; }
    }
}
