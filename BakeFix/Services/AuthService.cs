using BakeFix.Models;
using BakeFix.Repositories;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BakeFix.Services
{
    public class AuthService
    {
        private readonly UserRepository _userRepo;
        private readonly IOrganizationRepository _orgRepo;
        private readonly IConfiguration _config;

        public AuthService(UserRepository userRepo, IOrganizationRepository orgRepo, IConfiguration config)
        {
            _userRepo = userRepo;
            _orgRepo = orgRepo;
            _config = config;
        }

        public async Task<(bool Success, string? ErrorMessage, User? User, string? Token, string OrgName, List<string> Modules)>
            LoginAsync(string username, string password)
        {
            var user = await _userRepo.GetUserByUsernameAsync(username);
            if (user == null)
                return (false, "Invalid credentials", null, null, "", new());

            // Detect legacy rows: PasswordHash holds plain text (doesn't start with $2)
            bool isLegacy = !user.PasswordHash.StartsWith("$2");
            bool passwordValid;

            if (isLegacy)
            {
                // Old plain-text comparison
                passwordValid = password == user.PasswordHash;

                if (passwordValid)
                {
                    // On-the-fly migration: fill Password (plain) + PasswordHash (bcrypt)
                    var hash = BCrypt.Net.BCrypt.HashPassword(password);
                    await _userRepo.MigratePasswordAsync(user.Id, password, hash);
                }
            }
            else
            {
                passwordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            }

            if (!passwordValid)
                return (false, "Invalid credentials", null, null, "", new());

            // Org users whose organization is suspended cannot log in
            if (user.OrganizationId.HasValue && user.OrgIsActive == false)
                return (false, "Your organization account has been suspended.", null, null, "", new());

            var modules = user.OrganizationId.HasValue
                ? await _orgRepo.GetEnabledModulesAsync(user.OrganizationId.Value)
                : new List<string>();

            var token = GenerateJwtToken(user);

            return (true, null, user, token, user.OrganizationName ?? "", modules);
        }

        private string GenerateJwtToken(User user)
        {
            var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,        user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim("role",           user.Role),
                new Claim("organizationId", user.OrganizationId?.ToString() ?? ""),
            };

            var creds = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:ExpiresInDays"]!)),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
