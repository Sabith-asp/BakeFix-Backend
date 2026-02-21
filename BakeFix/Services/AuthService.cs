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
        private readonly UserRepository _repository;
        private readonly IConfiguration _config;

        public AuthService(UserRepository repository, IConfiguration config)
        {
            _repository = repository;
            _config = config;
        }

        public async Task<(bool Success, User User, string Token)> LoginAsync(string username, string password)
        {
            var user = await _repository.GetUserByUsernameAsync(username);
            if (user == null)
                return (false, null, null);

            // Validate password (plain text for demo; use hashing)
            if (user.PasswordHash != password)
                return (false, null, null);

            var token = GenerateJwtToken(user);

            return (true, user, token);
        }

        private string GenerateJwtToken(User user)
        {
            var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            };

            var creds = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:ExpiresInDays"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
