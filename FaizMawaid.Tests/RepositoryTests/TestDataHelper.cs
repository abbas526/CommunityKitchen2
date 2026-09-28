using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories;

namespace FaizMawaid.Tests.RepositoryTests
{
    /// <summary>
    /// Creates and removes throwaway prerequisite rows (unique names/emails, tagged
    /// "unittest.local"/"UT_") so each repository test is self-contained and doesn't
    /// disturb real data. Creation goes through the real repositories (dogfooding);
    /// cleanup uses raw SQL since the repositories deliberately don't expose hard
    /// deletes for Users/Families -- that's business-rule territory, not test plumbing.
    /// </summary>
    public static class TestDataHelper
    {
        public static async Task<byte> CreateThaaliSizeAsync(IDbConnectionFactory factory)
        {
            var repo = new ThaaliSizeRepository(factory);
            var name = "UT_" + Guid.NewGuid().ToString("N")[..10];
            return await repo.CreateAsync(new CreateThaaliSizeRequest { Name = name, SortOrder = 99 });
        }

        public static async Task DeleteThaaliSizeAsync(IDbConnectionFactory factory, byte id)
        {
            using var connection = factory.CreateConnection();
            await connection.ExecuteAsync("DELETE FROM ThaaliSizes WHERE Id = @Id;", new { Id = id });
        }

        public static async Task<ulong> CreateUserAsync(IDbConnectionFactory factory, byte roleId = RoleIds.Admin)
        {
            var repo = new UserRepository(factory);
            var email = $"ut_{Guid.NewGuid():N}@unittest.local";
            return await repo.CreateAsync(new CreateUserRequest
            {
                RoleId = roleId,
                Email = email,
                PasswordHash = "unit-test-hash",
                FullName = "Unit Test User",
                Phone = null
            });
        }

        public static async Task DeleteUserAsync(IDbConnectionFactory factory, ulong id)
        {
            using var connection = factory.CreateConnection();
            await connection.ExecuteAsync("DELETE FROM Users WHERE Id = @Id;", new { Id = id });
        }

        /// <summary>Generates a unique throwaway Sabil Number so parallel/repeated test runs never collide on UQ_Users_SabilNumber.</summary>
        public static string RandomSabilNumber()
        {
            return "UT_" + Guid.NewGuid().ToString("N")[..10];
        }

        public static async Task<RegisterFamilyResponse> CreateFamilyAsync(IDbConnectionFactory factory, byte thaaliSizeId)
        {
            var repo = new FamilyRepository(factory);
            var email = $"ut_family_{Guid.NewGuid():N}@unittest.local";
            return await repo.RegisterAsync(new RegisterFamilyRequest
            {
                Email = email,
                SabilNumber = RandomSabilNumber(),
                PasswordHash = "unit-test-hash",
                FullName = "Unit Test Family Head",
                Phone = null,
                Address = "123 Unit Test Street",
                NumberOfMembers = 4,
                ThaaliSizeId = thaaliSizeId
            });
        }

        /// <summary>Deletes a family and everything that hangs off it, in FK-safe (child-first) order.</summary>
        public static async Task DeleteFamilyAsync(IDbConnectionFactory factory, ulong familyId, ulong? familyHeadUserId)
        {
            using var connection = factory.CreateConnection();
            await connection.ExecuteAsync("DELETE FROM ThaaliCancellations WHERE FamilyId = @FamilyId;", new { FamilyId = familyId });
            await connection.ExecuteAsync("DELETE FROM FamilySizeHistory WHERE FamilyId = @FamilyId;", new { FamilyId = familyId });
            await connection.ExecuteAsync("DELETE FROM Families WHERE Id = @Id;", new { Id = familyId });
            if (familyHeadUserId is not null)
            {
                await connection.ExecuteAsync("DELETE FROM Users WHERE Id = @Id;", new { Id = familyHeadUserId });
            }
        }

        /// <summary>A future date far enough out, and randomized, to avoid colliding with real data or a previous failed test run's leftovers.</summary>
        public static DateOnly RandomFutureDate()
        {
            return DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3).AddDays(Random.Shared.Next(1, 3000)));
        }
    }
}
