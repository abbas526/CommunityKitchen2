using System.Data;

namespace FaizMawaid.Data
{
    /// <summary>
    /// Creates ADO.NET connections for repositories to use with Dapper.
    /// Injected as a singleton; each call returns a new, already-open connection
    /// that the caller is responsible for disposing (use a `using` block).
    /// </summary>
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }
}
