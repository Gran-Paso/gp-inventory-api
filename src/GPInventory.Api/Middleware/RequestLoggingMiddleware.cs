using System.Text;

namespace GPInventory.Api.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Solo logear POST/PUT/PATCH
        if (context.Request.Method == "POST" || context.Request.Method == "PUT" || context.Request.Method == "PATCH")
        {
            context.Request.EnableBuffering();
            
            var body = await new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true)
                .ReadToEndAsync();
            
            context.Request.Body.Position = 0;

            _logger.LogInformation("=== REQUEST BODY ===");
            _logger.LogInformation("Method: {Method} | Path: {Path}", context.Request.Method, context.Request.Path);
            _logger.LogInformation("Content-Type: {ContentType}", context.Request.ContentType);
            _logger.LogInformation("Body: {Body}", body);
            _logger.LogInformation("===================");
        }

        await _next(context);
    }
}
