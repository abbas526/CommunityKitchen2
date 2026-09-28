using FaizMawaid.Models;
using Microsoft.AspNetCore.Identity;

namespace FaizMawaid.Services
{
    public interface IPasswordHasherService
    {
        string Hash(string password);
        bool Verify(string hash, string password);
    }

    /// <summary>
    /// Thin wrapper around ASP.NET Core Identity's PasswordHasher&lt;T&gt; (PBKDF2 under
    /// the hood), used standalone here without pulling in the rest of ASP.NET Core
    /// Identity (no IdentityUser, no EF user store, no cookie auth) -- this app's own
    /// Users table and JWT-based auth stay exactly as they are; only the hashing
    /// algorithm itself is reused rather than hand-rolled.
    /// </summary>
    public class PasswordHasherService : IPasswordHasherService
    {
        private readonly PasswordHasher<User> _hasher = new();

        /// <summary>
        /// The `user` parameter PasswordHasher&lt;T&gt; normally takes isn't used by its
        /// default implementation (it exists only so a custom IPasswordHasher could vary
        /// the hash per-user) -- passing null is the standard, documented pattern when
        /// using it standalone like this.
        /// </summary>
        public string Hash(string password)
        {
            return _hasher.HashPassword(null!, password);
        }

        public bool Verify(string hash, string password)
        {
            var result = _hasher.VerifyHashedPassword(null!, hash, password);
            return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
