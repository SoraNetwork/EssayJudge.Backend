using System;

namespace SoraEssayJudge.Dtos
{
    public class StudentDto
    {
        public Guid Id { get; set; }
        public required string StudentId { get; set; }
        public required string Name { get; set; }
        public Guid ClassId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
