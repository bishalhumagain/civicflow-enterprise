using Microsoft.EntityFrameworkCore;
using Npgsql;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// Proves that the citizen tracking policies hold at the database boundary,
/// not only in application queries. These tests are opt-in because they need
/// a disposable PostgreSQL database whose administrator can seed data and a
/// separate, non-superuser runtime login that is subject to FORCE RLS.
/// </summary>
public sealed class CitizenCaseTrackingRlsTests
{
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool schemaReady;

    [PostgreSqlFact]
    public async Task CitizenContextSeesOnlyOwnVisibleTrackingData()
    {
        await EnsureSchemaAsync();
        var seed = await SeedAsync();

        await using var connection = new NpgsqlConnection(RuntimeConnectionString);
        await connection.OpenAsync();
        await AssertRoleIsSubjectToRlsAsync(connection);

        await using var transaction = await connection.BeginTransactionAsync();
        await SetConfigAsync(connection, transaction, "civicflow.user_id", seed.CitizenA);

        Assert.Equal(1, await CountAsync(connection, transaction,
            "select count(*) from cases"));
        Assert.Equal(0, await CountAsync(connection, transaction,
            "select count(*) from cases where \"Id\" = @id", ("id", seed.CaseC)));
        Assert.Equal(2, await CountAsync(connection, transaction,
            "select count(*) from case_status_history where \"CaseId\" = @id",
            ("id", seed.CaseA)));
        Assert.Equal(3, await CountAsync(connection, transaction,
            "select count(*) from case_updates where \"CaseId\" = @id",
            ("id", seed.CaseA)));
        Assert.Equal(0, await CountAsync(connection, transaction,
            "select count(*) from case_updates where \"Kind\" = 'InternalNote'"));
        Assert.Equal(1, await CountAsync(connection, transaction,
            "select count(*) from case_documents where \"CaseId\" = @id",
            ("id", seed.CaseA)));
        Assert.Equal(0, await CountAsync(connection, transaction,
            "select count(*) from case_documents where \"IsVisibleToCitizen\" = false"));
        Assert.Equal(1, await CountAsync(connection, transaction,
            "select count(*) from staff_members"));

        await transaction.RollbackAsync();

        // The owning organization can see the whole tenant, including internal
        // notes and internal documents, but never another organization.
        await using var organizationTransaction =
            await connection.BeginTransactionAsync();
        await SetConfigAsync(
            connection,
            organizationTransaction,
            "civicflow.organization_id",
            seed.OrganizationA);

        Assert.Equal(2, await CountAsync(connection, organizationTransaction,
            "select count(*) from cases"));
        Assert.Equal(5, await CountAsync(connection, organizationTransaction,
            "select count(*) from case_updates"));
        Assert.Equal(3, await CountAsync(connection, organizationTransaction,
            "select count(*) from case_documents"));
        Assert.Equal(1, await CountAsync(connection, organizationTransaction,
            "select count(*) from case_updates where \"Kind\" = 'InternalNote'"));

        await organizationTransaction.RollbackAsync();
    }

    [PostgreSqlFact]
    public async Task CitizenWritesAreRestrictedToOwnVisibleCaseData()
    {
        await EnsureSchemaAsync();
        var seed = await SeedAsync();

        await using var connection = new NpgsqlConnection(RuntimeConnectionString);
        await connection.OpenAsync();
        await AssertRoleIsSubjectToRlsAsync(connection);

        Assert.Equal(1, await ExecuteCitizenWriteAsync(
            connection,
            seed.CitizenA,
            """
            insert into case_updates
                ("Id", "CaseId", "OrganizationId", "Kind", "Message",
                 "IsVisibleToCitizen", "AuthorUserId", "CreatedAt")
            values
                (@id, @caseId, @organizationId, 'CitizenResponse',
                 'Citizen supplied the requested information.', true,
                 @authorUserId, now())
            """,
            ("id", Guid.NewGuid()),
            ("caseId", seed.CaseA),
            ("organizationId", seed.OrganizationA),
            ("authorUserId", seed.CitizenA)));

        Assert.Equal(1, await ExecuteCitizenWriteAsync(
            connection,
            seed.CitizenA,
            """
            insert into case_status_history
                ("Id", "CaseId", "OrganizationId", "PreviousStatus",
                 "NewStatus", "ChangedAt", "ChangedByUserId", "Explanation",
                 "IsVisibleToCitizen")
            values
                (@id, @caseId, @organizationId, 'AwaitingInformation',
                 'UnderReview', now(), @changedByUserId,
                 'Citizen provided the requested information.', true)
            """,
            ("id", Guid.NewGuid()),
            ("caseId", seed.CaseA),
            ("organizationId", seed.OrganizationA),
            ("changedByUserId", seed.CitizenA)));

        var internalNote = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteCitizenWriteAsync(
                connection,
                seed.CitizenA,
                """
                insert into case_updates
                    ("Id", "CaseId", "OrganizationId", "Kind", "Message",
                     "IsVisibleToCitizen", "AuthorUserId", "CreatedAt")
                values
                    (@id, @caseId, @organizationId, 'InternalNote',
                     'Must not be writable from a citizen context.', false,
                     @authorUserId, now())
                """,
                ("id", Guid.NewGuid()),
                ("caseId", seed.CaseA),
                ("organizationId", seed.OrganizationA),
                ("authorUserId", seed.CitizenA)));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, internalNote.SqlState);

        var crossCitizenUpdate = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteCitizenWriteAsync(
                connection,
                seed.CitizenA,
                """
                insert into case_updates
                    ("Id", "CaseId", "OrganizationId", "Kind", "Message",
                     "IsVisibleToCitizen", "AuthorUserId", "CreatedAt")
                values
                    (@id, @caseId, @organizationId, 'CitizenResponse',
                     'Cross-citizen write attempt.', true, @authorUserId, now())
                """,
                ("id", Guid.NewGuid()),
                ("caseId", seed.CaseC),
                ("organizationId", seed.OrganizationA),
                ("authorUserId", seed.CitizenA)));
        Assert.Equal(
            PostgresErrorCodes.InsufficientPrivilege,
            crossCitizenUpdate.SqlState);

        var crossCitizenHistory = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteCitizenWriteAsync(
                connection,
                seed.CitizenA,
                """
                insert into case_status_history
                    ("Id", "CaseId", "OrganizationId", "PreviousStatus",
                     "NewStatus", "ChangedAt", "ChangedByUserId", "Explanation",
                     "IsVisibleToCitizen")
                values
                    (@id, @caseId, @organizationId, 'Submitted', 'UnderReview',
                     now(), @changedByUserId, 'Cross-citizen write attempt.', true)
                """,
                ("id", Guid.NewGuid()),
                ("caseId", seed.CaseC),
                ("organizationId", seed.OrganizationA),
                ("changedByUserId", seed.CitizenA)));
        Assert.Equal(
            PostgresErrorCodes.InsufficientPrivilege,
            crossCitizenHistory.SqlState);

        Assert.Equal(0, await ExecuteCitizenWriteAsync(
            connection,
            seed.CitizenA,
            "update cases set \"Title\" = 'Cross-citizen update' where \"Id\" = @id",
            ("id", seed.CaseC)));

        Assert.Equal(1, await ExecuteCitizenWriteAsync(
            connection,
            seed.CitizenA,
            "update cases set \"Title\" = 'Owner update' where \"Id\" = @id",
            ("id", seed.CaseA)));
    }

    [PostgreSqlFact]
    public async Task PlatformReadContextSeesAllTenantsButCannotWrite()
    {
        await EnsureSchemaAsync();
        var seed = await SeedAsync();

        await using var connection = new NpgsqlConnection(RuntimeConnectionString);
        await connection.OpenAsync();
        await AssertRoleIsSubjectToRlsAsync(connection);

        // With no context the runtime role sees nothing.
        Assert.Equal(0, await CountAsync(connection, null,
            "select count(*) from cases"));

        await using var transaction = await connection.BeginTransactionAsync();
        await SetConfigAsync(
            connection,
            transaction,
            "civicflow.platform_read",
            "on");

        Assert.Equal(3, await CountAsync(connection, transaction,
            """
            select count(*) from cases
            where "OrganizationId" in (@organizationA, @organizationB)
            """,
            ("organizationA", seed.OrganizationA),
            ("organizationB", seed.OrganizationB)));
        Assert.Equal(2, await CountAsync(connection, transaction,
            """
            select count(*) from case_status_history
            where "OrganizationId" in (@organizationA, @organizationB)
            """,
            ("organizationA", seed.OrganizationA),
            ("organizationB", seed.OrganizationB)));
        Assert.Equal(6, await CountAsync(connection, transaction,
            """
            select count(*) from case_updates
            where "OrganizationId" in (@organizationA, @organizationB)
            """,
            ("organizationA", seed.OrganizationA),
            ("organizationB", seed.OrganizationB)));
        Assert.Equal(3, await CountAsync(connection, transaction,
            """
            select count(*) from case_documents
            where "OrganizationId" in (@organizationA, @organizationB)
            """,
            ("organizationA", seed.OrganizationA),
            ("organizationB", seed.OrganizationB)));
        Assert.Equal(2, await CountAsync(connection, transaction,
            """
            select count(*) from staff_members
            where "OrganizationId" in (@organizationA, @organizationB)
            """,
            ("organizationA", seed.OrganizationA),
            ("organizationB", seed.OrganizationB)));

        // The platform policies are SELECT-only, so the existing tenant
        // policies still govern every write path.
        Assert.Equal(6, await CountAsync(
            connection,
            transaction,
            """
            select count(*) from pg_policies
            where "policyname" like '%_platform_read'
              and "cmd" = 'SELECT'
            """));

        Assert.Equal(0, await ExecuteAsync(
            connection,
            transaction,
            "update cases set \"Title\" = 'Platform read must not write'"));
        Assert.Equal(0, await ExecuteAsync(
            connection,
            transaction,
            "delete from cases"));

        var insert = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(
                connection,
                transaction,
                """
                insert into cases
                    ("Id", "OrganizationId", "ReferenceNumber", "Title",
                     "Description", "Status", "Priority", "CreatedAt",
                     "UpdatedAt")
                values
                    (@id, @organizationId, @reference, 'Platform read insert',
                     'A write attempt from the platform read context.',
                     'Submitted', 'Normal', now(), now())
                """,
                ("id", Guid.NewGuid()),
                ("organizationId", seed.OrganizationA),
                ("reference", $"CF-PLATFORM-{Guid.NewGuid():N}")));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, insert.SqlState);

        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    public async Task TrackingTablesForceRlsAndTrackingMigrationIsApplied()
    {
        await EnsureSchemaAsync();

        await using var connection = new NpgsqlConnection(RuntimeConnectionString);
        await connection.OpenAsync();
        await AssertRoleIsSubjectToRlsAsync(connection);

        await using var command = new NpgsqlCommand(
            """
            select "relname", "relrowsecurity", "relforcerowsecurity"
            from pg_class
            where "relname" = any (@tables)
            """,
            connection);
        command.Parameters.AddWithValue(
            "tables",
            new[]
            {
                "cases",
                "case_status_history",
                "case_updates",
                "case_documents",
                "staff_members"
            });

        var forced = new Dictionary<string, bool>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                forced[reader.GetString(0)] =
                    reader.GetBoolean(1) && reader.GetBoolean(2);
            }
        }

        Assert.Equal(5, forced.Count);
        Assert.All(forced, entry => Assert.True(
            entry.Value,
            $"{entry.Key} must have row-level security enabled and forced."));

        Assert.Equal(1, await CountAsync(
            connection,
            null,
            """
            select count(*) from pg_policies
            where "tablename" = 'case_updates'
              and "policyname" = 'case_updates_organization_isolation'
            """));
        Assert.Equal(1, await CountAsync(
            connection,
            null,
            """
            select count(*) from "__EFMigrationsHistory"
            where "MigrationId" = '20261025070043_AddCitizenCaseTrackingUpdates'
            """));
        Assert.Equal(1, await CountAsync(
            connection,
            null,
            """
            select count(*) from "__EFMigrationsHistory"
            where "MigrationId" = '20261025070100_AddCitizenTrackingRls'
            """));
        Assert.Equal(1, await CountAsync(
            connection,
            null,
            """
            select count(*) from "__EFMigrationsHistory"
            where "MigrationId" = '20261025150000_AddPlatformReadRls'
            """));
    }

    private static string AdminConnectionString =>
        Environment.GetEnvironmentVariable(
            PostgreSqlFactAttribute.AdminConnectionVariable)
        ?? throw new InvalidOperationException(
            $"{PostgreSqlFactAttribute.AdminConnectionVariable} is not set.");

    private static string RuntimeConnectionString =>
        Environment.GetEnvironmentVariable(
            PostgreSqlFactAttribute.RuntimeConnectionVariable)
        ?? throw new InvalidOperationException(
            $"{PostgreSqlFactAttribute.RuntimeConnectionVariable} is not set.");

    private static async Task EnsureSchemaAsync()
    {
        if (schemaReady)
            return;

        await SchemaLock.WaitAsync();

        try
        {
            if (schemaReady)
                return;

            await using (var connection = new NpgsqlConnection(AdminConnectionString))
            {
                await connection.OpenAsync();

                await using var roleCommand = new NpgsqlCommand(
                    "select rolsuper from pg_roles where rolname = current_user",
                    connection);
                var isSuperUser = (bool)(await roleCommand.ExecuteScalarAsync()
                    ?? false);

                if (!isSuperUser)
                {
                    throw new InvalidOperationException(
                        "The PostgreSQL admin test connection must be a " +
                        "superuser so it can bypass RLS while seeding.");
                }

                await using var dbContext = new CivicFlowDbContext(
                    new DbContextOptionsBuilder<CivicFlowDbContext>()
                        .UseNpgsql(AdminConnectionString)
                        .Options);

                await dbContext.Database.MigrateAsync();

                var runtimeUser = new NpgsqlConnectionStringBuilder(
                    RuntimeConnectionString).Username;

                if (string.IsNullOrWhiteSpace(runtimeUser))
                {
                    throw new InvalidOperationException(
                        "The runtime connection string must include a username.");
                }

                var quotedRuntimeUser = QuoteIdentifier(runtimeUser);

                await using var grantCommand = new NpgsqlCommand(
                    $"""
                    grant usage on schema public to {quotedRuntimeUser};
                    grant select, insert, update, delete
                        on all tables in schema public to {quotedRuntimeUser};
                    grant usage, select
                        on all sequences in schema public to {quotedRuntimeUser};
                    """,
                    connection);

                await grantCommand.ExecuteNonQueryAsync();
            }

            schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private static async Task AssertRoleIsSubjectToRlsAsync(
        NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            select rolsuper, rolbypassrls
            from pg_roles
            where rolname = current_user
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.False(
            reader.GetBoolean(0),
            "RLS tests must use a non-superuser runtime role.");
        Assert.False(
            reader.GetBoolean(1),
            "RLS tests must use a role without BYPASSRLS.");
    }

    private static async Task SetConfigAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string key,
        Guid value)
    {
        await using var command = new NpgsqlCommand(
            "select set_config(@key, @value, true)",
            connection,
            transaction);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("value", value.ToString());

        await command.ExecuteScalarAsync();
    }

    private static async Task SetConfigAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string key,
        string value)
    {
        await using var command = new NpgsqlCommand(
            "select set_config(@key, @value, true)",
            connection,
            transaction);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("value", value);

        await command.ExecuteScalarAsync();
    }

    private static async Task<int> ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(
            sql,
            connection,
            transaction);

        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(
            sql,
            connection,
            transaction);

        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ExecuteCitizenWriteAsync(
        NpgsqlConnection connection,
        Guid citizenUserId,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        await SetConfigAsync(
            connection,
            transaction,
            "civicflow.user_id",
            citizenUserId);

        try
        {
            await using var command = new NpgsqlCommand(
                sql,
                connection,
                transaction);

            foreach (var (name, value) in parameters)
                command.Parameters.AddWithValue(name, value);

            var affected = await command.ExecuteNonQueryAsync();
            await transaction.RollbackAsync();
            return affected;
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch
            {
                // Preserve the original failure, which the test asserts on.
            }

            throw;
        }
    }

    private static async Task<SeededData> SeedAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var organizationA = Organization.Create(
            "RLS Test Organization A",
            $"RLSA-{Guid.NewGuid():N}",
            now);
        var organizationB = Organization.Create(
            "RLS Test Organization B",
            $"RLSB-{Guid.NewGuid():N}",
            now);
        var staffA = StaffMember.Create(
            organizationA.Id,
            "Assigned Staff A",
            $"staff-a-{Guid.NewGuid():N}@example.test",
            null,
            now);
        var staffB = StaffMember.Create(
            organizationB.Id,
            "Assigned Staff B",
            $"staff-b-{Guid.NewGuid():N}@example.test",
            null,
            now);
        var citizenA = Guid.NewGuid();
        var citizenB = Guid.NewGuid();

        var caseA = Case.Create(
            organizationA.Id,
            $"CF-RLS-A-{Guid.NewGuid():N}",
            "Citizen A tracked case",
            "The first citizen's request.",
            now,
            citizenA);
        caseA.ChangeStatus(CaseStatus.UnderReview, now);
        caseA.ChangeStatus(CaseStatus.AwaitingInformation, now);
        caseA.AssignTo(staffA.Id, CaseAssignmentRole.CaseWorker, now);

        var caseB = Case.Create(
            organizationB.Id,
            $"CF-RLS-B-{Guid.NewGuid():N}",
            "Citizen B other-tenant case",
            "The second citizen's request in another organization.",
            now,
            citizenB);
        caseB.AssignTo(staffB.Id, CaseAssignmentRole.CaseWorker, now);

        var caseC = Case.Create(
            organizationA.Id,
            $"CF-RLS-C-{Guid.NewGuid():N}",
            "Citizen B same-tenant case",
            "Another citizen's request in the first organization.",
            now,
            citizenB);

        var historyA = CaseStatusHistory.Create(
            caseA.Id,
            organizationA.Id,
            CaseStatus.Submitted,
            CaseStatus.UnderReview,
            now.AddMinutes(1),
            staffA.Id,
            "Review started.",
            isVisibleToCitizen: true);
        var hiddenHistoryA = CaseStatusHistory.Create(
            caseA.Id,
            organizationA.Id,
            CaseStatus.UnderReview,
            CaseStatus.AwaitingInformation,
            now.AddMinutes(2),
            staffA.Id,
            "Internal triage note.",
            isVisibleToCitizen: false);

        var publicUpdateA = CaseUpdate.Create(
            caseA.Id,
            organizationA.Id,
            CaseUpdateKind.PublicUpdate,
            "A public update for citizen A.",
            staffA.Id,
            now.AddMinutes(3));
        var informationRequestA = CaseUpdate.Create(
            caseA.Id,
            organizationA.Id,
            CaseUpdateKind.InformationRequest,
            "Please provide the missing information.",
            staffA.Id,
            now.AddMinutes(4));
        var internalNoteA = CaseUpdate.Create(
            caseA.Id,
            organizationA.Id,
            CaseUpdateKind.InternalNote,
            "Internal note that citizens must never see.",
            staffA.Id,
            now.AddMinutes(5));
        var citizenResponseA = CaseUpdate.Create(
            caseA.Id,
            organizationA.Id,
            CaseUpdateKind.CitizenResponse,
            "Here is the requested information.",
            citizenA,
            now.AddMinutes(6));
        var updateB = CaseUpdate.Create(
            caseB.Id,
            organizationB.Id,
            CaseUpdateKind.PublicUpdate,
            "A public update for citizen B.",
            staffB.Id,
            now.AddMinutes(7));
        var updateC = CaseUpdate.Create(
            caseC.Id,
            organizationA.Id,
            CaseUpdateKind.PublicUpdate,
            "A public update for another citizen's case.",
            staffA.Id,
            now.AddMinutes(8));

        var documentA = CaseDocument.Create(
            caseA.Id,
            organizationA.Id,
            "citizen-a-evidence.pdf",
            $"rls-test/{Guid.NewGuid():N}",
            "application/pdf",
            128,
            new string('a', 64),
            citizenA,
            now.AddMinutes(9),
            isVisibleToCitizen: true);
        var internalDocumentA = CaseDocument.Create(
            caseA.Id,
            organizationA.Id,
            "staff-internal.pdf",
            $"rls-test/{Guid.NewGuid():N}",
            "application/pdf",
            256,
            new string('b', 64),
            staffA.Id,
            now.AddMinutes(10),
            isVisibleToCitizen: false);
        var documentC = CaseDocument.Create(
            caseC.Id,
            organizationA.Id,
            "other-citizen.pdf",
            $"rls-test/{Guid.NewGuid():N}",
            "application/pdf",
            512,
            new string('c', 64),
            citizenB,
            now.AddMinutes(11),
            isVisibleToCitizen: true);

        await using var dbContext = new CivicFlowDbContext(
            new DbContextOptionsBuilder<CivicFlowDbContext>()
                .UseNpgsql(AdminConnectionString)
                .Options);

        dbContext.Organizations.AddRange(organizationA, organizationB);
        dbContext.StaffMembers.AddRange(staffA, staffB);
        dbContext.Cases.AddRange(caseA, caseB, caseC);
        dbContext.CaseStatusHistories.AddRange(historyA, hiddenHistoryA);
        dbContext.CaseUpdates.AddRange(
            publicUpdateA,
            informationRequestA,
            internalNoteA,
            citizenResponseA,
            updateB,
            updateC);
        dbContext.CaseDocuments.AddRange(
            documentA,
            internalDocumentA,
            documentC);

        await dbContext.SaveChangesAsync();

        return new SeededData(
            organizationA.Id,
            organizationB.Id,
            citizenA,
            citizenB,
            caseA.Id,
            caseB.Id,
            caseC.Id,
            staffA.Id,
            staffB.Id);
    }

    private static string QuoteIdentifier(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private sealed record SeededData(
        Guid OrganizationA,
        Guid OrganizationB,
        Guid CitizenA,
        Guid CitizenB,
        Guid CaseA,
        Guid CaseB,
        Guid CaseC,
        Guid StaffA,
        Guid StaffB);
}
