using FaizMawaid.Services;
using Xunit;

namespace FaizMawaid.Tests.ServiceTests
{
    public class PasswordHasherServiceTests
    {
        [Fact]
        public void Hash_ThenVerify_ReturnsTrueForCorrectPassword()
        {
            var hasher = new PasswordHasherService();
            var hash = hasher.Hash("CorrectHorseBattery1");

            var result = hasher.Verify(hash, "CorrectHorseBattery1");

            Assert.True(result);
        }

        [Fact]
        public void Verify_ReturnsFalseForWrongPassword()
        {
            var hasher = new PasswordHasherService();
            var hash = hasher.Hash("CorrectHorseBattery1");

            var result = hasher.Verify(hash, "SomethingElse123");

            Assert.False(result);
        }

        [Fact]
        public void Hash_ProducesDifferentHashesForTheSamePassword()
        {
            var hasher = new PasswordHasherService();

            var hash1 = hasher.Hash("SamePassword123");
            var hash2 = hasher.Hash("SamePassword123");

            // PasswordHasher<T> salts each hash, so two hashes of the same input differ,
            // yet both still verify successfully against the original password.
            Assert.NotEqual(hash1, hash2);
            Assert.True(hasher.Verify(hash1, "SamePassword123"));
            Assert.True(hasher.Verify(hash2, "SamePassword123"));
        }
    }
}
