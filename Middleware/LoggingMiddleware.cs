namespace APIPrcIdentity.Middleware
{
    public class LoggingMiddleware
    {
        private readonly ILogger<LoggingMiddleware> _logger;
        private readonly RequestDelegate _next;

        public LoggingMiddleware(ILogger<LoggingMiddleware> logger, RequestDelegate next)
        {
            this._logger = logger;
            this._next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            _logger.LogInformation($"Incoming Request : {context.Request.Method} {context.Request.Path}");

            await _next(context);

            _logger.LogInformation($"Outgoing Response : {context.Response.StatusCode}");
        }
    }

    public static class LoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseLogging(this IApplicationBuilder app)
        {
            return app.UseMiddleware<LoggingMiddleware>();
        }
    }
}
