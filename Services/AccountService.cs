using APIPrcIdentity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace APIPrcIdentity.Services
{
    public class AccountService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;

        public AccountService(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration)
        {
            this._userManager = userManager;
            this._roleManager = roleManager; 
            this._configuration = configuration;
        }

        public async Task<IdentityResult?> CreateUserAsync(IdentityUser user)
        {
            if (user is null)
                return null;

            return await _userManager.CreateAsync(user);
        }

        public async Task<IdentityUser?> FindUser(string username)
        {
            if (username is null)
                return null;

            return await _userManager.FindByNameAsync(username);
        }

        public async Task<IList<string>?> GetRoles(IdentityUser user)
        {
            if(user is null) return null;

            return await _userManager.GetRolesAsync(user);
        }

        public JwtSecurityToken GetToken(List<Claim> claims)
        {
            return new JwtSecurityToken(
                issuer: _configuration["JWT:Issuer"],
                audience: _configuration["JWT:Audience"],
                expires: DateTime.Now.AddMinutes(double.Parse(_configuration["JWT:ExpiryMinutes"]!)),
                claims: claims,
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Key"]!)),
                SecurityAlgorithms.HmacSha256));
        }

        public async Task<IdentityResult?> AddRole(string role)
        {
            return await _roleManager.CreateAsync(new IdentityRole(role));
        }

        public async Task<bool> IsRoleExists(string role)
        {
            return await _roleManager.RoleExistsAsync(role);
        }

        public async Task<IdentityResult> AssignRole(IdentityUser user, string role)
        {
            return await _userManager.AddToRoleAsync(user, role);
        }
    }
}
