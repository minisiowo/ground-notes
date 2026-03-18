namespace QuickNotesTxt.Services;

public enum AiServiceErrorKind
{
    MissingApiKey,
    MissingModel,
    Unauthorized,
    Forbidden,
    QuotaExceeded,
    RateLimited,
    TransientServerError,
    InvalidResponse,
    EmptyResponse,
    RequestFailed
}
