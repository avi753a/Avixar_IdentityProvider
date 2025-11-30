using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Text;

namespace Avixar.Infrastructure.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _logDirectory = @"D:\Logs\";

        public GlobalExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Log Request
                await LogRequestAsync(context);

                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task LogRequestAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            var controllerName = "Unknown";
            var actionName = "Unknown";

            if (endpoint?.Metadata?.GetMetadata<ControllerActionDescriptor>() is ControllerActionDescriptor descriptor)
            {
                controllerName = descriptor.ControllerName;
                actionName = descriptor.ActionName;
            }

            var logMessage = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Request: {context.Request.Method} {context.Request.Path} | Controller: {controllerName} | Action: {actionName} | IP: {context.Connection.RemoteIpAddress}";
            
            await File.AppendAllTextAsync(Path.Combine(_logDirectory, "requests.log"), logMessage + Environment.NewLine);
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var logMessage = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}";
            await File.AppendAllTextAsync(Path.Combine(_logDirectory, "errors.log"), logMessage);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            await context.Response.WriteAsJsonAsync(new
            {
                Status = false,
                Message = "An internal server error occurred. Please contact support.",
                ErrorId = Guid.NewGuid() // You could log this ID to correlate
            });
        }
    }
}
