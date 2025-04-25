using ArandanoIRT_Backend.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Tests.Integration.Common
{
    /// <summary>
    /// Provides helper methods for creating DbContext instances for integration tests
    /// using an SQLite in-memory database.
    /// </summary>
    public static class DbContextTestHelper
    {
        /// <summary>
        /// Creates a new instance of ApplicationDbContext configured for SQLite in-memory.
        /// Requires the caller to dispose the returned DbContext and keep the SqliteConnection
        /// alive for the duration of the DbContext's use.
        /// </summary>
        /// <returns>A tuple containing the configured ApplicationDbContext and the underlying SqliteConnection.</returns>
        public static (ApplicationDbContext Context, SqliteConnection Connection) CreateInMemoryDbContext()
        {
            // SQLite in-memory requires an open connection for the database to persist.
            // "DataSource=:memory:" creates a private in-memory database.
            var connection = new SqliteConnection("DataSource=:memory:");
            // Keeps the connection open.
            connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new ApplicationDbContext(options);

            // Ensures the database schema is created based on the model.
            context.Database.EnsureCreated();

            // Returns both context and connection, allowing the caller to manage the connection lifetime.
            return (context, connection);
        }

        /// <summary>
        /// Creates a new instance of ApplicationDbContext configured for SQLite in-memory.
        /// This version is suitable for scenarios where the connection lifetime does not need separate management.
        /// </summary>
        /// <returns>A configured ApplicationDbContext instance.</returns>
        /// <remarks>
        /// This overload offers simpler usage for basic test cases.
        /// The underlying connection might close when the context is disposed, potentially dropping the database.
        /// The overload returning a tuple <c>(ApplicationDbContext, SqliteConnection)</c> provides more control over the connection lifetime
        /// for tests requiring persistence across multiple context operations.
        /// </remarks>
        public static ApplicationDbContext CreateInMemoryDbContextSimple()
        {
            // Creates and opens a connection specifically for this context instance.
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
               .UseSqlite(connection)
               .Options;

            var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();

            // The associated connection might be closed when this context instance is disposed.
            return context;
        }

        /// <summary>
        /// Creates DbContextOptions for ApplicationDbContext using an existing open SQLite connection.
        /// </summary>
        /// <param name="connection">An existing, open SqliteConnection to be used by the context.</param>
        /// <returns>DbContextOptions configured to use the provided connection.</returns>
        public static DbContextOptions<ApplicationDbContext> CreateOptionsForExistingConnection(SqliteConnection connection)
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
        }
    }
}