using Microsoft.AspNetCore.Mvc;

namespace Funforum.Api.Common;

public class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "API exception: {Message}", ex.Message);

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = ex.StatusCode;

            var problem = new ProblemDetails
            {
                Status = ex.StatusCode,
                Title = ReasonPhrase(ex.StatusCode),
                Detail = ex.Message,
                Instance = context.Request.Path,
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        _ => "Error",
    };
}
