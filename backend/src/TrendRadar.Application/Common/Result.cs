namespace TrendRadar.Application.Common;

public enum ErrorKind
{
    Invalid,
    NotFound,
    Conflict,
}

public sealed record Result<T>(T? Value, string? Error, ErrorKind Kind = ErrorKind.Invalid)
{
    public bool Succeeded => Error is null;
}

public static class Result
{
    public static Result<T> Ok<T>(T value) => new(value, null);

    public static Result<T> Fail<T>(string error, ErrorKind kind = ErrorKind.Invalid) => new(default, error, kind);
}
