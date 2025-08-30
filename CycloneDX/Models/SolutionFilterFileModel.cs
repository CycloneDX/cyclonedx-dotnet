using System.Text.Json.Serialization;

namespace CycloneDX.Models
{
    public class SolutionFilterFileModel
    {
        [JsonPropertyName("solution")]
        public SolutionFilterSolution Solution { get; set; }
    }

    public class SolutionFilterSolution
    {
        [JsonPropertyName("path")]
        public string Path { get; init; }

        [JsonPropertyName("projects")]
        public string[] Projects { get; init; }
    }
}
