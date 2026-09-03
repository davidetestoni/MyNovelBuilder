namespace MyNovelBuilder.WebApi.Tests.Utils;

public readonly struct Result<TValue, TError> {
    public TValue Value { get; }
    public TError Error { get; }
    public bool IsOk { get; }
    
    private Result(TValue v, TError e, bool success)
    {
        Value = v;
        Error = e;
        IsOk = success;
    }

    public static Result<TValue, TError?> Ok(TValue v)
    {
        return new Result<TValue, TError?>(v, default, true);
    }

    public static Result<TValue?, TError> Err(TError e)
    {
        return new Result<TValue?, TError>(default, e, false);
    }

    public static implicit operator Result<TValue, TError?>(TValue v) => new(v, default, true);
    public static implicit operator Result<TValue?, TError>(TError e) => new(default, e, false);

    public TResult Match<TResult>(
        Func<TValue, TResult> success,
        Func<TError, TResult> failure) =>
        IsOk ? success(Value) : failure(Error);
}
