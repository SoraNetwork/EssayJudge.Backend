using System;
using System.ComponentModel.DataAnnotations;

namespace SoraEssayJudge.Models
{
    public class User
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public required string Username { get; set; }

        [Required]
        public required string Password { get; set; }

        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
