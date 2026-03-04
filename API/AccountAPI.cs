using APIPrcIdentity.Models;
using APIPrcIdentity.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIPrcIdentity.API
{
    public static class AccountAPI
    {
        public static RouteGroupBuilder MapAccountEndpoints(this IEndpointRouteBuilder builder)
        {
            var group = builder.MapGroup("Account");

            group.MapPost("/Register", Register);
            group.MapPost("/Login", Login);
            group.MapPost("/Add-Role", AddRole);
            group.MapPost("/Assign-Role", AssignRole);

            return group;
        }

        private static async Task<IResult> Register([FromBody] Register model, [FromServices] AccountService service)
        {
            var user = new IdentityUser { UserName = model.Username, Email = model.Email};
            user.PasswordHash = new PasswordHasher<IdentityUser>().HashPassword(user, model.Password);
            var result = await service.CreateUserAsync(user);

            if (result is null)
                return Results.InternalServerError(new { message = "Error ocurred"});

            if (result.Succeeded)
                return Results.Ok(new { message = "User registered successfully" });

            return Results.BadRequest(result.Errors);
        }

        private static async Task<IResult> Login([FromBody] Login model, [FromServices] AccountService service)
        {
            var user = await service.FindUser(model.Username);

            if(user != null && (new PasswordHasher<IdentityUser>().VerifyHashedPassword(user, user.PasswordHash, model.Password) == PasswordVerificationResult.Success))
            {
                var userRoles = await service.GetRoles(user);

                if (userRoles is null)
                    return Results.InternalServerError();

                var authClaims = new List<Claim>
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.UserName!),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                authClaims.AddRange(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

                var token = service.GetToken(authClaims);

                return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
            }

            return Results.Unauthorized();
        }

        public static async Task<IResult> AddRole([FromBody] Role model, [FromServices] AccountService service)
        {
            if(!(await service.IsRoleExists(model.RoleType)))
            {
                var result = await service.AddRole(model.RoleType);

                if (result.Succeeded)
                    return Results.Ok(new { message = "Role added" });

                return Results.BadRequest(result.Errors);
            }
            return Results.BadRequest();
        }

        public static async Task<IResult> AssignRole([FromBody] Role model, [FromServices] AccountService service)
        {
            var user = await service.FindUser(model.Username);
            if (user == null)
            {
                return Results.BadRequest<string>("User not found");
            }

            var result = await service.AssignRole(user, model.RoleType);

            if (result.Succeeded)
                return Results.Ok<string>("Role assigned successfully");

            return Results.BadRequest(result.Errors);
        }
    }
}
