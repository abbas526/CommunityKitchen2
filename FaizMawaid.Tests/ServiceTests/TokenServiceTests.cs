using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FaizMawaid.Models;
using FaizMawaid.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FaizMawaid.Tests.ServiceTests
{
    public class TokenServiceTests
    {
        private static IConfiguration BuildConfig(int accessMinutes = 30, int refreshDays = 14)
        {
            var dict = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-signing-key-at-least-32-characters-long!",
                ["Jwt:Issuer"] = "FaizMawaidTests",
                ["Jwt:Audience"] = "FaizMawaidTests",
                ["Jwt:AccessTokenMinutes"] = accessMinutes.ToString(),
                ["Jwt:RefreshTokenDays"] = refreshDays.ToString()
            };
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        private static User SampleUser() => new()
        {
            Id = 42,
            RoleId = RoleIds.FamilyHead,
            Email = "fatema@example.com",
            FullName = "Fatema Rangwala",
            PasswordHash = "irrelevant-for-this-test",
            MustChangePassword = false,
            IsActive = true
        };

        [Fact]
        public void CreateAccessToken_ProducesJwtWithExpectedClaims()
        {
            var service = new TokenService(BuildConfig(accessMinutes: 30));
            var user = SampleUser();

            var result = service.CreateAccessToken(user, RoleNames.FamilyHead);

            Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

            Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
            Assert.Equal(user.Email, jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
            Assert.Equal(user.FullName, jwt.Claims.First(c => c.Type == ClaimTypes.Name).Value);
            Assert.Equal(RoleNames.FamilyHead, jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
            Assert.Equal("FaizMawaidTests", jwt.Issuer);

            // Roughly 30 minutes out -- generous tolerance since we don't control clock precision here.
            var expectedExpiry = DateTime.UtcNow.AddMinutes(30);
            Assert.True(Math.Abs((result.ExpiresAt - expectedExpiry).TotalSeconds) < 10);
            Assert.Equal(result.ExpiresAt, jwt.ValidTo, TimeSpan.FromSeconds(2));
        }

        [Fact]
        public void CreateAccessToken_SuperAdmin_CarriesBothSuperAdminAndAdminRoleClaims()
        {
            var service = new TokenService(BuildConfig());
            var user = SampleUser();
            user.RoleId = RoleIds.SuperAdmin;

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateAccessToken(user, RoleNames.SuperAdmin).AccessToken);

            var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
            Assert.Contains(RoleNames.SuperAdmin, roles);
            Assert.Contains(RoleNames.Admin, roles);
            Assert.Equal(2, roles.Count);
        }

        [Fact]
        public void CreateAccessToken_Admin_DoesNotCarryTheSuperAdminRole()
        {
            var service = new TokenService(BuildConfig());
            var user = SampleUser();
            user.RoleId = RoleIds.Admin;

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateAccessToken(user, RoleNames.Admin).AccessToken);

            var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
            Assert.Equal(new[] { RoleNames.Admin }, roles);
        }

        [Fact]
        public void CreateAccessToken_FamilyHead_CarriesOnlyTheFamilyHeadRole()
        {
            var service = new TokenService(BuildConfig());

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateAccessToken(SampleUser(), RoleNames.FamilyHead).AccessToken);

            var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
            Assert.Equal(new[] { RoleNames.FamilyHead }, roles);
        }

        [Fact]
        public void CreateAccessToken_ThrowsWhenJwtKeyIsMissing()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var service = new TokenService(config);

            Assert.Throws<InvalidOperationException>(() => service.CreateAccessToken(SampleUser(), RoleNames.FamilyHead));
        }

        [Fact]
        public void CreateRefreshToken_ProducesRawTokenWhoseHashMatchesTokenHash()
        {
            var service = new TokenService(BuildConfig(refreshDays: 14));

            var result = service.CreateRefreshToken();

            Assert.False(string.IsNullOrWhiteSpace(result.RawToken));
            Assert.Equal(service.HashRefreshToken(result.RawToken), result.TokenHash);

            var expectedExpiry = DateTime.UtcNow.AddDays(14);
            Assert.True(Math.Abs((result.ExpiresAt - expectedExpiry).TotalMinutes) < 1);
        }

        [Fact]
        public void CreateRefreshToken_ProducesADifferentRawTokenEveryCall()
        {
            var service = new TokenService(BuildConfig());

            var first = service.CreateRefreshToken();
            var second = service.CreateRefreshToken();

            Assert.NotEqual(first.RawToken, second.RawToken);
            Assert.NotEqual(first.TokenHash, second.TokenHash);
        }

        [Fact]
        public void HashRefreshToken_IsDeterministicForTheSameInput()
        {
            var service = new TokenService(BuildConfig());

            var hash1 = service.HashRefreshToken("some-raw-token-value");
            var hash2 = service.HashRefreshToken("some-raw-token-value");

            Assert.Equal(hash1, hash2);
        }
    }
}
