using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace SoraEssayJudge.Models
{
    public class AIModel
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public required string ModelId { get; set; }

        public string? ServiceType { get; set; }
        [ForeignKey(nameof(ApiKey))]
        public Guid? ApiKeyId { get; set; }
        public virtual ApiKey? ApiKey { get; set; }

        public virtual ICollection<AIModelUsageSetting>? UsageSettings { get; set; }
    }
}
