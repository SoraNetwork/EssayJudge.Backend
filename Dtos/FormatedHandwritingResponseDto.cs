using System.Text.Json.Serialization;

namespace SoraEssayJudge.Dtos
{
    public class FormatedHandwritingResponseDto
    {
        [JsonPropertyName("studentInfo")]
        public StudentInfoDto? StudentInfo { get; set; }

        [JsonPropertyName("essayInfo")]
        public required EssayInfoDto EssayInfo { get; set; }

        public class StudentInfoDto
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("id")]
            public string? Id { get; set; }
        }

        public class EssayInfoDto
        {
            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }
    }
}