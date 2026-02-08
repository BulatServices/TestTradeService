using System.Text.Json;
using TestTradeService.Api.Contracts;

namespace TestTradeService.Api;

/// <summary>
/// Middleware преобразования исключений в единый контракт ошибок API.
/// </summary>
public sealed class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    /// <summary>
    /// Инициализирует middleware обработки исключений API.
    /// </summary>
    /// <param name="next">Следующий обработчик конвейера.</param>
    /// <param name="logger">Логгер middleware.</param>
    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Обрабатывает входящий HTTP-запрос и формирует API-ошибку при исключении.
    /// </summary>
    /// <param name="context">Контекст HTTP-запроса.</param>
    /// <returns>Задача обработки запроса.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ArgumentException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "validation_error", ex.Message, ex);
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "internal_error", "Внутренняя ошибка сервера.", ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message, Exception ex)
    {
        if (statusCode >= 500)
        {
            _logger.LogError(ex, "Request failed");
        }
        else
        {
            _logger.LogWarning(ex, "Request validation failed");
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = new ApiErrorResponse
        {
            Code = code,
            Message = message,
            Details = ex.Message,
            TraceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
