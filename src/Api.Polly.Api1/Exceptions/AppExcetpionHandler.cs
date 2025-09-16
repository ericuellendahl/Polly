using Microsoft.AspNetCore.Diagnostics;

namespace Api.Polly.Api1.Exceptions;

public class AppExcetpionHandler : IExceptionHandler
{
    // precisamos registrar no Program.cs
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.StatusCode = exception switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        await httpContext.Response.WriteAsJsonAsync(new ErrorDetails
        {
            StatusCode = httpContext.Response.StatusCode,
            // não exibir detalhes do erro em produção
            ErrorMessage = $"Internal Server Error from the custom IExceptionHandler. Exception Message: {exception.Message}"
        }, cancellationToken);

        return default;
    }
}
