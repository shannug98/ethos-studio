using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Admin;

public class AdminCreateWorkshopRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [StringLength(100)]
    public string DanceStyle { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Level { get; set; } = string.Empty;

    [Required]
    public DateTime WorkshopDate { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    public TimeSpan? BookingCutoffTime { get; set; }

    [Required]
    [StringLength(200)]
    public string Venue { get; set; } = string.Empty;

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    [Range(1, 10000)]
    public int Capacity { get; set; }

    public Guid? TrainerProfileId { get; set; }

    public string? ImageUrl { get; set; }
    public string? LandscapeImageUrl { get; set; }

    public string? City { get; set; }
    public string? Area { get; set; }
    public string? ShortDescription { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactNumber { get; set; }
    public bool? PublicVisibility { get; set; }
    public string? RegistrationType { get; set; }
    public string? TermsAndCancellationPolicy { get; set; }

    public string? GooglePlaceId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? VenueAddress { get; set; }
    [MaxLength(1000)]
    public string? LocationUrl { get; set; }

    public string? Timezone { get; set; }

    [JsonConverter(typeof(FlexibleWorkshopStatusConverter))]
    public WorkshopStatus? Status { get; set; }

    public bool AllowReEntry { get; set; } = true;

    public bool RequireReEntryVerification { get; set; } = false;

    public TimeSpan? ReEntryCooldown { get; set; }

    public List<AdminWorkshopPricingTierItem>? PricingTiers { get; set; }

    public List<Guid>? TrainerProfileIds { get; set; }

    public List<Ethos.Api.Contracts.Workshops.AdminWorkshopSessionItem>? Sessions { get; set; }

    public List<Ethos.Api.Contracts.Workshops.AdminWorkshopPassTypeItem>? PassTypes { get; set; }
}

public class AdminUpdateWorkshopRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [StringLength(100)]
    public string DanceStyle { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Level { get; set; } = string.Empty;

    [Required]
    public DateTime WorkshopDate { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    public TimeSpan? BookingCutoffTime { get; set; }

    [Required]
    [StringLength(200)]
    public string Venue { get; set; } = string.Empty;

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    [Range(1, 10000)]
    public int Capacity { get; set; }

    public Guid? TrainerProfileId { get; set; }

    public string? ImageUrl { get; set; }
    public string? LandscapeImageUrl { get; set; }

    public string? City { get; set; }
    public string? Area { get; set; }
    public string? ShortDescription { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactNumber { get; set; }
    public bool? PublicVisibility { get; set; }
    public string? RegistrationType { get; set; }
    public string? TermsAndCancellationPolicy { get; set; }

    public string? GooglePlaceId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? VenueAddress { get; set; }
    [MaxLength(1000)]
    public string? LocationUrl { get; set; }

    public string? Timezone { get; set; }

    [JsonConverter(typeof(FlexibleWorkshopStatusConverter))]
    public WorkshopStatus? Status { get; set; }

    public bool AllowReEntry { get; set; } = true;

    public bool RequireReEntryVerification { get; set; } = false;

    public TimeSpan? ReEntryCooldown { get; set; }

    public List<AdminWorkshopPricingTierItem>? PricingTiers { get; set; }

    public List<Guid>? TrainerProfileIds { get; set; }

    public List<Ethos.Api.Contracts.Workshops.AdminWorkshopSessionItem>? Sessions { get; set; }

    public List<Ethos.Api.Contracts.Workshops.AdminWorkshopPassTypeItem>? PassTypes { get; set; }
}

public class FlexibleWorkshopStatusConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(WorkshopStatus) || typeToConvert == typeof(WorkshopStatus?);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(WorkshopStatus))
            return new ValueConverter();
        if (typeToConvert == typeof(WorkshopStatus?))
            return new NullableConverter();
        return null;
    }

    private class ValueConverter : JsonConverter<WorkshopStatus>
    {
        public override WorkshopStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int intVal))
            {
                if (Enum.IsDefined(typeof(WorkshopStatus), intVal))
                    return (WorkshopStatus)intVal;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    if (int.TryParse(str, out int pInt) && Enum.IsDefined(typeof(WorkshopStatus), pInt))
                        return (WorkshopStatus)pInt;
                    if (Enum.TryParse<WorkshopStatus>(str, ignoreCase: true, out var status))
                        return status;
                }
            }
            return WorkshopStatus.Draft;
        }

        public override void Write(Utf8JsonWriter writer, WorkshopStatus value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    private class NullableConverter : JsonConverter<WorkshopStatus?>
    {
        public override WorkshopStatus? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int intVal))
            {
                if (Enum.IsDefined(typeof(WorkshopStatus), intVal))
                    return (WorkshopStatus)intVal;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str)) return null;
                if (int.TryParse(str, out int pInt) && Enum.IsDefined(typeof(WorkshopStatus), pInt))
                    return (WorkshopStatus)pInt;
                if (Enum.TryParse<WorkshopStatus>(str, ignoreCase: true, out var status))
                    return status;
            }
            return null;
        }

        public override void Write(Utf8JsonWriter writer, WorkshopStatus? value, JsonSerializerOptions options)
        {
            if (value.HasValue) writer.WriteStringValue(value.Value.ToString());
            else writer.WriteNullValue();
        }
    }
}
