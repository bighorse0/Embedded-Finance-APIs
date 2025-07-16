using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration config, ILogger<AuthController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            // TODO: Implement proper user authentication
            // This is a placeholder for demonstration
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { Error = "Invalid credentials" });
            }

            // Generate JWT token
            var token = GenerateJwtToken(request.Username, request.TenantId);

            _logger.LogInformation("User {Username} logged in successfully", request.Username);

            return Ok(new AuthResponse
            {
                Token = token,
                Username = request.Username,
                TenantId = request.TenantId,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for user {Username}", request.Username);
            return StatusCode(500, new { Error = "Login failed" });
        }
    }

    [HttpPost("validate")]
    public async Task<ActionResult<ValidationResponse>> ValidateToken([FromBody] ValidationRequest request)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(request.Token, validationParameters, out var validatedToken);

            var username = principal.FindFirst(ClaimTypes.Name)?.Value;
            var tenantId = principal.FindFirst("TenantId")?.Value;

            _logger.LogInformation("Token validated for user {Username}", username);

            return Ok(new ValidationResponse
            {
                IsValid = true,
                Username = username,
                TenantId = tenantId,
                Claims = principal.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation failed");
            return Ok(new ValidationResponse { IsValid = false });
        }
    }

    [HttpGet("health")]
    public ActionResult<object> Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Service = "API Gateway Authentication"
        });
    }

    private string GenerateJwtToken(string username, string tenantId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim("TenantId", tenantId),
            new Claim(ClaimTypes.Role, "User")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(24),
            Issuer = _config["Jwt:Issuer"],
            Audience = _config["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class ValidationRequest
{
    public string Token { get; set; } = string.Empty;
}

public class ValidationResponse
{
    public bool IsValid { get; set; }
    public string? Username { get; set; }
    public string? TenantId { get; set; }
    public List<object>? Claims { get; set; }
} 