using System.ComponentModel.DataAnnotations;

namespace SoraEssayJudge.Dtos
{
    public class UserRegisterDto
    {
        [Required]
        [MaxLength(50)]
        public required string Username { get; set; }

        [Required]
        public required string Password { get; set; }

        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }
    }
}
