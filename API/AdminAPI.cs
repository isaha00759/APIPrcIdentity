using System.Runtime.CompilerServices;

namespace APIPrcIdentity.API
{
    public static class AdminAPI
    {
        public static IEndpointRouteBuilder MapAdminRoute(this IEndpointRouteBuilder builder)
        {
            builder.MapGet("/Admin", () => "Admin access").RequireAuthorization(policy => policy.RequireRole("Admin"));

            return builder;
        }
    }
}
