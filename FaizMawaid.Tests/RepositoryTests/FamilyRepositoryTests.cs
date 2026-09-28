using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories;
using Xunit;

namespace FaizMawaid.Tests.RepositoryTests
{
    public class FamilyRepositoryTests
    {
        [Fact]
        public async Task RegisterAsync_CreatesUserAndPendingFamilyTogether()
        {
            var factory = TestConnectionFactory.Create();
            var familyRepository = new FamilyRepository(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            RegisterFamilyResponse? result = null;
            try
            {
                var email = $"ut_family_{Guid.NewGuid():N}@unittest.local";
                result = await familyRepository.RegisterAsync(new RegisterFamilyRequest
                {
                    Email = email,
                    PasswordHash = "hash",
                    FullName = "UT Family Head",
                    Address = "1 Test Ln",
                    NumberOfMembers = 3,
                    ThaaliSizeId = thaaliSizeId
                });

                var family = await familyRepository.GetByIdAsync(result.FamilyId);

                Assert.NotNull(family);
                Assert.Equal(RegistrationStatus.Pending, family!.RegistrationStatus);
                Assert.Equal(result.UserId, family.FamilyHeadUserId);
                Assert.Equal(thaaliSizeId, family.ThaaliSizeId);
            }
            finally
            {
                if (result is not null)
                {
                    await TestDataHelper.DeleteFamilyAsync(factory, result.FamilyId, result.UserId);
                }
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
            }
        }

        [Fact]
        public async Task ApproveAsync_MovesFamilyFromPendingToApproved()
        {
            var factory = TestConnectionFactory.Create();
            var familyRepository = new FamilyRepository(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var registration = await TestDataHelper.CreateFamilyAsync(factory, thaaliSizeId);
            var adminUserId = await TestDataHelper.CreateUserAsync(factory, RoleIds.Admin);
            try
            {
                var approved = await familyRepository.ApproveAsync(registration.FamilyId, adminUserId);

                Assert.True(approved);
                var family = await familyRepository.GetByIdAsync(registration.FamilyId);
                Assert.Equal(RegistrationStatus.Approved, family!.RegistrationStatus);
                Assert.Equal(adminUserId, family.ApprovedByAdminUserId);
                Assert.True(family.IsActive);
            }
            finally
            {
                await TestDataHelper.DeleteFamilyAsync(factory, registration.FamilyId, registration.UserId);
                await TestDataHelper.DeleteUserAsync(factory, adminUserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
            }
        }

        [Fact]
        public async Task ChangeThaaliSizeAsync_UpdatesCurrentSizeAndAddsHistoryRow()
        {
            var factory = TestConnectionFactory.Create();
            var familyRepository = new FamilyRepository(factory);
            var historyRepository = new FamilySizeHistoryRepository(factory);
            var originalSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var newSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var registration = await TestDataHelper.CreateFamilyAsync(factory, originalSizeId);
            try
            {
                var changed = await familyRepository.ChangeThaaliSizeAsync(registration.FamilyId, new ChangeThaaliSizeRequest
                {
                    NewThaaliSizeId = newSizeId,
                    ChangedByUserId = registration.UserId,
                    EffectiveFromDate = DateOnly.FromDateTime(DateTime.UtcNow)
                });

                Assert.True(changed);
                var family = await familyRepository.GetByIdAsync(registration.FamilyId);
                Assert.Equal(newSizeId, family!.ThaaliSizeId);

                var history = await historyRepository.GetByFamilyIdAsync(registration.FamilyId);
                Assert.Equal(2, history.Count());
            }
            finally
            {
                await TestDataHelper.DeleteFamilyAsync(factory, registration.FamilyId, registration.UserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, originalSizeId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, newSizeId);
            }
        }

        [Fact]
        public async Task GetAllAsync_FilteredByStatus_IncludesTheRegisteredFamily()
        {
            var factory = TestConnectionFactory.Create();
            var familyRepository = new FamilyRepository(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var registration = await TestDataHelper.CreateFamilyAsync(factory, thaaliSizeId);
            try
            {
                var pending = await familyRepository.GetAllAsync(RegistrationStatus.Pending);

                Assert.Contains(pending, f => f.Id == registration.FamilyId);
            }
            finally
            {
                await TestDataHelper.DeleteFamilyAsync(factory, registration.FamilyId, registration.UserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
            }
        }
        [Fact]
        public async Task CreateSubFamilyAsync_LinksASubFamilyToItsParent()
        {
            var factory = TestConnectionFactory.Create();
            var familyRepository = new FamilyRepository(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var parent = await TestDataHelper.CreateFamilyAsync(factory, thaaliSizeId);
            var adminUserId = await TestDataHelper.CreateUserAsync(factory, RoleIds.Admin);
            ulong subFamilyId = 0;
            try
            {
                subFamilyId = await familyRepository.CreateSubFamilyAsync(parent.FamilyId, new CreateSubFamilyRequest
                {
                    SubFamilyLabel = "UT Sub-Family",
                    ThaaliSizeId = thaaliSizeId,
                    CreatedByAdminUserId = adminUserId
                });

                var subFamily = await familyRepository.GetByIdAsync(subFamilyId);
                Assert.NotNull(subFamily);
                Assert.Equal(parent.FamilyId, subFamily!.ParentFamilyId);
                Assert.Null(subFamily.FamilyHeadUserId);
                Assert.Equal("UT Sub-Family", subFamily.SubFamilyLabel);
                Assert.Equal(RegistrationStatus.Approved, subFamily.RegistrationStatus);
                Assert.True(subFamily.IsActive);

                var subFamilies = await familyRepository.GetSubFamiliesAsync(parent.FamilyId);
                Assert.Contains(subFamilies, f => f.Id == subFamilyId);
            }
            finally
            {
                if (subFamilyId != 0)
                {
                    await TestDataHelper.DeleteFamilyAsync(factory, subFamilyId, null);
                }
                await TestDataHelper.DeleteFamilyAsync(factory, parent.FamilyId, parent.UserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
                await TestDataHelper.DeleteUserAsync(factory, adminUserId);
            }
        }

        [Fact]
        public async Task GetAllAsync_ExcludesSubFamilies()
        {
            var factory = TestConnectionFactory.Create();
            var familyRepository = new FamilyRepository(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var parent = await TestDataHelper.CreateFamilyAsync(factory, thaaliSizeId);
            var adminUserId = await TestDataHelper.CreateUserAsync(factory, RoleIds.Admin);
            var subFamilyId = await familyRepository.CreateSubFamilyAsync(parent.FamilyId, new CreateSubFamilyRequest
            {
                SubFamilyLabel = "UT Sub-Family 2",
                ThaaliSizeId = thaaliSizeId,
                CreatedByAdminUserId = adminUserId
            });
            try
            {
                var all = await familyRepository.GetAllAsync();

                Assert.DoesNotContain(all, f => f.Id == subFamilyId);
                Assert.Contains(all, f => f.Id == parent.FamilyId);
            }
            finally
            {
                await TestDataHelper.DeleteFamilyAsync(factory, subFamilyId, null);
                await TestDataHelper.DeleteFamilyAsync(factory, parent.FamilyId, parent.UserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
                await TestDataHelper.DeleteUserAsync(factory, adminUserId);
            }
        }
    }
}
