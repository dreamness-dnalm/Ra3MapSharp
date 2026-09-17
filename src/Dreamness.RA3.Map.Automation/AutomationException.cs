namespace Dreamness.RA3.Map.Automation;

/// <summary>
/// Automation 领域异常。错误码稳定，便于协议层映射。
/// </summary>
public sealed class AutomationException : Exception
{
    public AutomationException(
        string code,
        string message,
        bool retryable = false,
        IReadOnlyDictionary<string, string>? details = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Retryable = retryable;
        Details = details;
    }

    public string Code { get; }

    public bool Retryable { get; }

    public IReadOnlyDictionary<string, string>? Details { get; }
}
