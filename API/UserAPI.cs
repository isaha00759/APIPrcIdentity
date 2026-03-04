namespace APIPrcIdentity.API
{
    public static class UserAPI
    {
        public static IEndpointRouteBuilder MapUserRoute(this IEndpointRouteBuilder builder)
        {
            builder.MapGet("/User", () => "User access").RequireAuthorization(policy => policy.RequireRole("User"));
            return builder;
        }
    }
}
