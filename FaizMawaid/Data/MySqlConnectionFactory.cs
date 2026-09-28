using System.Data;
using MySqlConnector;

namespace FaizMawaid.Data
{
    public class MySqlConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public MySqlConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("CommKitchen")
                ?? throw new InvalidOperationException(
                    "Connection string 'CommKitchen' was not found in configuration (appsettings.json).");
        }

        public IDbConnection CreateConnection()
        {
            var connection = new MySqlConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}
