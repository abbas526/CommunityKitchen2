using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FaizMawaid.Models;
using Microsoft.IdentityModel.Tokens;

namespace FaizMawaid.Services
{
    public class AccessTokenResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    public class RefreshTokenResult
    {
        /// <summary>The raw, random token sent to the client -- never stored anywhere.</summary>
        public string RawToken { get; set; } = string.Empty;
        /// <summary>SHA-256 of RawToken -- this is what actually gets stored in RefreshTokens.TokenHash.</summary>
        public string TokenHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    public interface ITokenService
    {
        AccessTokenResult CreateAccessToken(User user, string roleName);
        RefreshTokenResult CreateRefreshToken();
        /// <summary>Hashes a raw refresh token the same way CreateRefreshToken does, so a presented token can be looked up by its hash.</summary>
        string HashRefreshToken(string rawToken);
    }

    /// <summary>
    /// Issues short-lived JWT access tokens (signed with Jwt:Key from configuration) and
    /// long-lived opaque refresh tokens (a random value; only its SHA-256 hash is ever
    /// persisted, in the RefreshTokens table from migration 008 -- the raw value is a
    /// bearer secret and is never stored, matching that table's own doc comment).
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public AccessTokenResult CreateAccessToken(User user, string roleName)
        {
            var key = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is not configured (see appsettings.json's Jwt section).");
            var issuer = _configuration["Jwt:Issuer"] ?? "FaizMawaidCommunityKitchen";
            var audience = _configuration["Jwt:Audience"] ?? issuer;
            var minutes = _configuration.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 30;

            var expiresAt = DateTime.UtcNow.AddMinutes(minutes);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Role, roleName)
            };

            var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(issuer, audience, claims, expires: expiresAt, signingCredentials: credentials);

            return new AccessTokenResult
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAt = expiresAt
            };
        }

        public RefreshTokenResult CreateRefreshToken()
        {
            var days = _configuration.GetValue<int?>("Jwt:RefreshTokenDays") ?? 14;
            var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            return new RefreshTokenResult
            {
                RawToken = raw,
                TokenHash = HashRefreshToken(raw),
                ExpiresAt = DateTime.UtcNow.AddDays(days)
            };
        }

        public string HashRefreshToken(string rawToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(bytes);
        }
    }
}
