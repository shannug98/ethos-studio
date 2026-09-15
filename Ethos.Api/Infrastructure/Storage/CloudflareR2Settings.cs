namespace Ethos.Api.Infrastructure.Storage;

public class CloudflareR2Settings
{
    public string AccountId { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string BucketName { get; set; } = "ethos-production-media";

    public string PublicDomain { get; set; } = string.Empty;

    public string ServiceUrl => !string.IsNullOrWhiteSpace(AccountId)
        ? $"https://{AccountId}.r2.cloudflarestorage.com"
        : string.Empty;
}
