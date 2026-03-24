using Microsoft.Data.SqlClient;

var connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING")
    ?? "Server=tcp:sliit.database.windows.net,1433;Initial Catalog=free-sql-db-3836547;Persist Security Info=False;User ID=sliit;Password=Omindu@2003;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

var sqlFile = args.Length > 0 ? args[0] : "Migrations/20260318_AddTournamentsTable.sql";

if (!File.Exists(sqlFile))
{
    Console.WriteLine($"❌ SQL file not found: {sqlFile}");
    Environment.Exit(1);
}

try
{
    var sqlContent = File.ReadAllText(sqlFile);
    Console.WriteLine($"📝 Executing migration: {sqlFile}");
    
    using (var connection = new SqlConnection(connectionString))
    {
        await connection.OpenAsync();
        Console.WriteLine("✅ Connected to Azure SQL Database");

        using (var command = connection.CreateCommand())
        {
            command.CommandText = sqlContent;
            command.CommandTimeout = 30;
            await command.ExecuteNonQueryAsync();
            Console.WriteLine("✅ Migration executed successfully");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Details: {ex.InnerException.Message}");
    }
    Environment.Exit(1);
}
