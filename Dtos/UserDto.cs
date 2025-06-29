using System;
using SoraEssayJudge.Models;

namespace SoraEssayJudge.Dtos
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public required string Username { get; set; }
        public required string Name { get; set; }
        public string? PhoneNumber { get; set; }

    }
}
