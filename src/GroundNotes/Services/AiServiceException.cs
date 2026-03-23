namespace GroundNotes.Services;

public sealed class AiServiceException : InvalidOperationException
{
    public AiServiceException(
        AiServiceErrorKind kind,
        string message,
        string? requestId = null,
        string? providerMessage = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        RequestId = requestId;
        ProviderMessage = providerMessage;
    }

    public AiServiceErrorKind Kind { get; }

    public string? RequestId { get; }

    public string? ProviderMessage { get; }
}
