namespace Ethos.Api.Contracts.Echo;

public class EchoChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
}

public class EchoAction
{
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "NAVIGATE"; // NAVIGATE, URL, CALL, WHATSAPP
    public string Value { get; set; } = string.Empty;
}

public class EchoChatResponse
{
    public string Message { get; set; } = string.Empty;
    public List<EchoAction> Actions { get; set; } = new();
    public string ConversationId { get; set; } = string.Empty;
}
