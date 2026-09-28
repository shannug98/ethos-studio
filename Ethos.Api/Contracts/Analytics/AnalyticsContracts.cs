using System.ComponentModel.DataAnnotations;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Analytics;

public class RecordAnalyticsEventRequest : IValidatableObject
{
    [Required]
    public AnalyticsEventType EventType { get; set; }

    [Required]
    [StringLength(64, MinimumLength = 8, ErrorMessage = "VisitorId must be between 8 and 64 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9_\-]+$", ErrorMessage = "VisitorId must contain only alphanumeric characters, underscores, or hyphens.")]
    public string VisitorId { get; set; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 8, ErrorMessage = "SessionId must be between 8 and 64 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9_\-]+$", ErrorMessage = "SessionId must contain only alphanumeric characters, underscores, or hyphens.")]
    public string SessionId { get; set; } = string.Empty;

    public Guid? WorkshopId { get; set; }

    [StringLength(500, ErrorMessage = "Path cannot exceed 500 characters.")]
    public string? Path { get; set; }

    [StringLength(500, ErrorMessage = "Referrer cannot exceed 500 characters.")]
    public string? Referrer { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(typeof(AnalyticsEventType), EventType))
        {
            yield return new ValidationResult(
                $"Invalid EventType value: {(int)EventType}. Must be a valid allowlisted AnalyticsEventType.",
                new[] { nameof(EventType) });
        }

        if (Metadata != null)
        {
            if (Metadata.Count > 20)
            {
                yield return new ValidationResult(
                    "Metadata dictionary cannot contain more than 20 key-value pairs.",
                    new[] { nameof(Metadata) });
            }

            foreach (var (key, value) in Metadata)
            {
                if (string.IsNullOrWhiteSpace(key) || key.Length > 64)
                {
                    yield return new ValidationResult(
                        "Metadata keys must be non-empty and 64 characters or fewer.",
                        new[] { nameof(Metadata) });
                    break;
                }

                if (value != null && value.Length > 256)
                {
                    yield return new ValidationResult(
                        $"Metadata value for key '{key}' exceeds maximum length of 256 characters.",
                        new[] { nameof(Metadata) });
                    break;
                }
            }

            string? jsonError = null;
            try
            {
                var serialized = System.Text.Json.JsonSerializer.Serialize(Metadata);
                if (serialized.Length > 2048)
                {
                    jsonError = $"Serialized metadata length ({serialized.Length} chars) exceeds the maximum allowed limit of 2048 characters.";
                }
            }
            catch
            {
                jsonError = "Metadata could not be serialized to valid JSON.";
            }

            if (jsonError != null)
            {
                yield return new ValidationResult(jsonError, new[] { nameof(Metadata) });
            }
        }
    }
}

public class RecordAnalyticsEventResponse
{
    public bool Success { get; set; }
    public Guid EventId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
