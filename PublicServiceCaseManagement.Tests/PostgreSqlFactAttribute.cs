namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// A fact that only runs when disposable PostgreSQL connection strings are
/// supplied. The default test suite runs entirely against the in-memory
/// provider, so tests that prove row-level security must be opt-in and must
/// never run against a shared or production database.
/// </summary>
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public const string AdminConnectionVariable =
        "CIVICFLOW_TEST_POSTGRES_ADMIN";

    public const string RuntimeConnectionVariable =
        "CIVICFLOW_TEST_POSTGRES_RUNTIME";

    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(AdminConnectionVariable)) ||
            string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(RuntimeConnectionVariable)))
        {
            Skip =
                $"Set {AdminConnectionVariable} and {RuntimeConnectionVariable} " +
                "to disposable PostgreSQL connection strings to run this test.";
        }
    }
}
