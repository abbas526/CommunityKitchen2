using System.Data;
using Dapper;
using FaizMawaid.Data;
using MySqlConnector;

namespace FaizMawaid.Tests.RepositoryTests
{
    /// <summary>
    /// These repository tests are integration tests against a real MySQL database --
    /// not isolated unit tests -- since the logic under test IS the SQL. They point at
    /// the same local CommKitchen database the app itself uses (see
    /// D:\CommKitchen\FaizMawaid\appsettings.json) by default. Your local MySQL must be
    /// running with the schema already created (migrations 000-011) for these to pass.
    /// Override the connection string with the COMMKITCHEN_TEST_CONNECTION_STRING
    /// environment variable if you'd rather point these at a separate database.
    /// </summary>
    public static class TestConnectionFactory
    {
        // Dapper's SqlMapper.AddTypeHandler registrations are process-wide (static) but only
        // happen automatically when the real app starts up (see Program.cs). The test project
        // never runs Program.cs, so we register the same handlers here. A static constructor
        // runs exactly once, the first time this class is touched -- which every repository
        // test does via Create() -- so this guarantees the handlers are in place before any
        // Dapper call happens.
        static TestConnectionFactory()
        {
            SqlMapper.AddTypeHandler(new BooleanTypeHandler());
            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        }

        public static IDbConnectionFactory Create()
        {
            var connectionString = Environment.GetEnvironmentVariable("COMMKITCHEN_TEST_CONNECTION_STRING")
                ?? "Server=localhost;Port=3306;Database=CommKitchen;User Id=root;Password=REPLACE_WITH_YOUR_MYSQL_PASSWORD;SslMode=Preferred;"; // placeholder -- set COMMKITCHEN_TEST_CONNECTION_STRING instead of editing this in, so a real password never lands in source control
            return new SimpleConnectionFactory(connectionString);
        }

        private class SimpleConnectionFactory : IDbConnectionFactory
        {
            private readonly string _connectionString;

            public SimpleConnectionFactory(string connectionString)
            {
                _connectionString = connectionString;
            }

            public IDbConnection CreateConnection()
            {
                var connection = new MySqlConnection(_connectionString);
                connection.Open();
                return connection;
            }
        }
    }
}
