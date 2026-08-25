using GitBinder.Domain.Common;

namespace GitBinder.Domain.Common;

/// <summary>
/// 统一的业务结果类型，携带成功值或领域错误。
/// </summary>
public class Result
{
    public bool IsSuccess => Error is null;

    public DomainError? Error { get; }

    protected Result(DomainError? error) => Error = error;

    public static Result Success() => new(null);

    public static Result Failure(DomainError error) => new(error);
}

/// <summary>
/// 携带返回值的业务结果。
/// </summary>
public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(T? value, DomainError? error) : base(error) => Value = value;

    public static Result<T> Success(T value) => new(value, null);

    public static new Result<T> Failure(DomainError error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);
}