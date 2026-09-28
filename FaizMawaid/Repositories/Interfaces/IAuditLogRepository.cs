using FaizMawaid.Models;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IAuditLogRepository
    {
        Task<ulong> AddAsync(AuditLog log);
        Task<IEnumerable<AuditLog>> GetAsync(string? entityType = null, ulong? entityId = null, DateOnly? from = null, DateOnly? to = null);
    }
}
