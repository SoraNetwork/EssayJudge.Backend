using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SoraEssayJudge.Models
{
    public class Class
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public required string Name { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Student> Students { get; set; } = new List<Student>();
    }
}
