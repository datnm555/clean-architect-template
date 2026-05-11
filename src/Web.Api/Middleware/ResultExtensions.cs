using SharedKernel;

namespace Web.Api.Middleware;

/// <summary>
/// Maps a Result/Result&lt;T&gt; into a minimal-API IResult, picking HTTP status from Error.Type.
/// Pass an onSuccess factory when you need a non-default response (e.g. 201 Created for POST).
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result, IResult? onSuccess = null) =>
        result.IsSuccess
            ? onSuccess ?? Results.NoContent()
            : MapError(result.Error);

    public static IResult ToHttpResult<TValue>(
        this Result<TValue> result,
        Func<TValue, IResult>? onSuccess = null) =>
        result.IsSuccess
            ? (onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value))
            : MapError(result.Error);

    private static IResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Description }),
        ErrorType.Validation => Results.BadRequest(new { error.Code, error.Description }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Description }),
        ErrorType.Problem => Results.Problem(
            detail: error.Description,
            title: error.Code,
            statusCode: StatusCodes.Status400BadRequest),
        _ => Results.Problem(
            detail: error.Description,
            title: error.Code,
            statusCode: StatusCodes.Status500InternalServerError),
    };
}
