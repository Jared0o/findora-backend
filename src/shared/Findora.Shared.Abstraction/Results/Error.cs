namespace Findora.Shared.Abstraction.Results;

public sealed record Error
{
    public Error(string code, string message)
        : this(code, message, null)
    {
    }

    public Error(string code, string message, string? path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
        Path = path;
    }

    public string Code { get; }
    public string Message { get; }
    public string? Path { get; }
}
