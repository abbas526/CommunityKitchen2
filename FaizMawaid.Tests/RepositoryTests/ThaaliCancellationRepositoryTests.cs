using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories;
using Xunit;

namespace FaizMawaid.Tests.RepositoryTests
{
    public class ThaaliCancellationRepositoryTests
    {
        [Fact]
        public async Task CreateAsync_ThenHasActiveCancellationOnDateAsync_ReturnsTrueWithinRange()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new ThaaliCancellationRepository(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var registration = await TestDataHelper.CreateFamilyAsync(factory, thaaliSizeId);
            var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
            var end = start.AddDays(1);
            try
            {
                var cancellationId = await repository.CreateAsync(new CreateThaaliCancellationRequest
                {
                    FamilyId = registration.FamilyId,
                    StartDate = start,
                    EndDate = end,
                    Reason = "Unit test cancellation",
                    CreatedByUserId = registration.UserId
                });

                var hasActive = await repository.HasActiveCancellationOnDateAsync(registration.FamilyId, start);
                Assert.True(hasActive);

                var fetched = await repository.GetByIdAsync(cancellationId);
                Assert.Equal(ThaaliCancellationStatus.Active, fetched!.Status);
            }
            finally
            {
                await TestDataHelper.DeleteFamilyAsync(factory, registration.FamilyId, registration.UserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
            }
        }

        [Fact]
        public async Task ReinstateAsync_SetsStatusToReinstated()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new ThaaliCancellationRepository(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var registration = await TestDataHelper.CreateFamilyAsync(factory, thaaliSizeId);
            var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
            var cancellationId = await repository.CreateAsync(new CreateThaaliCancellationRequest
            {
                FamilyId = registration.FamilyId,
                StartDate = start,
                EndDate = start,
                CreatedByUserId = registration.UserId
            });
            try
            {
                var reinstated = await repository.ReinstateAsync(cancellationId, registration.UserId);

                Assert.True(reinstated);
                var fetched = await repository.GetByIdAsync(cancellationId);
                Assert.Equal(ThaaliCancellationStatus.Reinstated, fetched!.Status);
                Assert.NotNull(fetched.ReinstatedAt);
                Assert.Equal(registration.UserId, fetched.ReinstatedByUserId);
            }
            finally
            {
                await TestDataHelper.DeleteFamilyAsync(factory, registration.FamilyId, registration.UserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
            }
        }

        [Fact]
        public async Task GetByFamilyIdAsync_ReturnsTheCreatedCancellation()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new ThaaliCancellationRepository(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var registration = await TestDataHelper.CreateFamilyAsync(factory, thaaliSizeId);
            var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4));
            try
            {
                await repository.CreateAsync(new CreateThaaliCancellationRequest
                {
                    FamilyId = registration.FamilyId,
                    StartDate = start,
                    EndDate = start,
                    CreatedByUserId = registration.UserId
                });

                var byFamily = await repository.GetByFamilyIdAsync(registration.FamilyId);

                Assert.Single(byFamily);
            }
            finally
            {
                await TestDataHelper.DeleteFamilyAsync(factory, registration.FamilyId, registration.UserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
            }
        }
    }
}
