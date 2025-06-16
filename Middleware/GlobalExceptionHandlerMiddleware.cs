using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Z_TRIP.Exceptions; // Add this import

namespace Z_TRIP.Middleware
{
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

        public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred");
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            var response = new ErrorResponse
            {
                TraceId = context.TraceIdentifier,
                Message = exception.Message
            };

            switch (exception)
            {
                case ValidationException:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    break;
                case UnauthorizedAccessException:
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    break;
                case ResourceNotFoundException:
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    break;
                default:
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    // Log the full exception details
                    Console.WriteLine($"Unhandled exception: {exception}");
                    response.Message = "Terjadi kesalahan internal. Silakan coba lagi nanti.";
                    break;
            }

            return context.Response.WriteAsJsonAsync(response);
        }

        public class ErrorResponse
        {
            public string TraceId { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }
    }
}