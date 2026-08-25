namespace GitBinder.Domain.Common;

/// <summary>
/// 统一的领域错误模型，用 Code + Arguments 描述，由 GUI 层做本地化。
/// </summary>
public sealed class DomainError
{
    public string Code { get; }

    public IReadOnlyList<string> Arguments { get; }

    public string? TechnicalDetails { get; }

    public DomainError(string code, params string[] arguments)
    {
        Code = code;
        Arguments = arguments;
    }

    public DomainError(string code, string? technicalDetails, params string[] arguments)
    {
        Code = code;
        Arguments = arguments;
        TechnicalDetails = technicalDetails;
    }

    public override string ToString() => Code;
}