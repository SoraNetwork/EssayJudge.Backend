using System.ComponentModel.DataAnnotations;

namespace SoraEssayJudge.Models
{
    public class ApiKey
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string ServiceType { get; set; } // e.g., "OpenAI", "Aliyun"

        [Required]
        public string Key { get; set; } // For OpenAI ApiKey or Aliyun AccessKeyId

        public string? Secret { get; set; } // For Aliyun AccessKeySecret

        public string? Endpoint { get; set; } // For OpenAI Endpoint

        public string? Description { get; set; }

        public bool IsEnabled { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
