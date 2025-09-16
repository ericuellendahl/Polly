namespace Api.Polly.Api1.Exceptions
{
    public partial class CustomExceptionMiddleware(RequestDelegate next, ILogger<CustomExceptionMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await next(httpContext);
            }
            catch (Exception ex)
            {
                logger.LogError("Something went wrong: {0}", ex);
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = exception switch
            {
                KeyNotFoundException => StatusCodes.Status404NotFound,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError
            };
            return context.Response.WriteAsJsonAsync(new ErrorDetails
            {
                StatusCode = context.Response.StatusCode,
                // não exibir detalhes do erro em produção
                ErrorMessage = $"Internal Server Error from the custom middleware. Exception Message: {exception.Message}"
            });
        }
    }
}
