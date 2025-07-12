using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoraEssayJudge.Models
{
    /// <summary>
    /// Stores settings for which AI model to use for a specific purpose.
    /// </summary>
    public class AIModelUsageSetting
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The purpose for which the model is used. e.g., "Judging", "Reporting", "OcrProcessing".
        /// </summary>
        [Required]
        public required string UsageType { get; set; }

        [Required]
        public Guid AIModelId { get; set; }

        [ForeignKey("AIModelId")]
        public virtual AIModel? AIModel { get; set; }

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
