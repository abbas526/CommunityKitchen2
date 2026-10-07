using System.Security.Claims;
using FaizMawaid.Models;
using FaizMawaid.Services;
using Xunit;

namespace FaizMawaid.Tests.ServiceTests
{
    public class AccountHierarchyTests
    {
        private static ClaimsPrincipal Principal(string? id, params string[] roles)
        {
            var claims = new List<Claim>();
            if (id is not null) { claims.Add(new Claim(ClaimTypes.NameIdentifier, id)); }
            foreach (var r in roles) { claims.Add(new Claim(ClaimTypes.Role, r)); }
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        // The full hierarchy table: who may manage which kind of account.
        [Theory]
        [InlineData(false, RoleIds.FamilyHead, true)]
        [InlineData(false, RoleIds.Admin, false)]
        [InlineData(false, RoleIds.SuperAdmin, false)]
        [InlineData(true, RoleIds.FamilyHead, true)]
        [InlineData(true, RoleIds.Admin, true)]
        [InlineData(true, RoleIds.SuperAdmin, true)]
        public void CanManage_FollowsTheHierarchyTable(bool callerIsSuperAdmin, byte targetRoleId, bool expected)
        {
            Assert.Equal(expected, AccountHierarchy.CanManage(callerIsSuperAdmin, targetRoleId));
        }

        [Fact]
        public void TryGetCaller_ReadsIdAndSuperAdminFromClaims()
        {
            var ok = AccountHierarchy.TryGetCaller(Principal("12", RoleNames.Admin, RoleNames.SuperAdmin), out var id, out var isSuper);

            Assert.True(ok);
            Assert.Equal(12UL, id);
            Assert.True(isSuper);
        }

        [Fact]
        public void TryGetCaller_RegularAdmin_IsNotSuperAdmin()
        {
            var ok = AccountHierarchy.TryGetCaller(Principal("3", RoleNames.Admin), out var id, out var isSuper);

            Assert.True(ok);
            Assert.Equal(3UL, id);
            Assert.False(isSuper);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not-a-number")]
        [InlineData("")]
        public void TryGetCaller_Fails_WhenIdClaimIsMissingOrInvalid(string? id)
        {
            Assert.False(AccountHierarchy.TryGetCaller(Principal(id, RoleNames.Admin), out _, out _));
        }

        [Fact]
        public void TryGetCaller_Fails_ForNullPrincipal()
        {
            Assert.False(AccountHierarchy.TryGetCaller(null, out _, out _));
        }

        [Fact]
        public void MaxActiveSuperAdmins_IsTwo()
        {
            Assert.Equal(2, RoleIds.MaxActiveSuperAdmins);
        }
    }
}
