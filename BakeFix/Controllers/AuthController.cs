using BakeFix.DTOs;
using BakeFix.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BakeFix.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _auth;

        public AuthController(AuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _auth.LoginAsync(request.Username, request.Password);

            if (!result.Success)
                return Unauthorized(new { message = "Invalid credentials" });

            var response = new LoginResponse
            {
                Id = result.User.Id,
                Username = result.User.Username,
                Token = result.Token
            };

            return Ok(response);
        }
    }
}
