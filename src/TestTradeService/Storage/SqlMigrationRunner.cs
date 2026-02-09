using System.Reflection;
using Dapper;
using Npgsql;

namespace TestTradeService.Storage;

/// <summary>
/// Применяет SQL-миграции к PostgreSQL и TimescaleDB.
/// </summary>
public sealed class SqlMigrationRunner
{
    private readonly MetadataDataSource _metadataDataSource;
    private readonly TimeseriesDataSource _timeseriesDataSource;

    /// <summary>
    /// Инициализирует раннер миграций.
    /// </summary>
    /// <param name="metadataDataSource">Источник подключений к БД метаданных.</param>
    /// <param name="timeseriesDataSource">Источник подключений к БД таймсерий.</param>
    public SqlMigrationRunner(MetadataDataSource metadataDataSource, TimeseriesDataSource timeseriesDataSource)
    {
        _metadataDataSource = metadataDataSource;
        _timeseriesDataSource = timeseriesDataSource;
    }

    /// <summary>
    /// Применяет все непримененные миграции.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача выполнения миграций.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var migrationScripts = LoadScripts();

        await EnsureMigrationTableAsync(cancellationToken);

        foreach (var migration in migrationScripts)
        {
            var alreadyApplied = await IsAppliedAsync(migration.Version, cancellationToken);
            if (alreadyApplied)
                continue;

            var dataSource = GetTargetDataSource(migration.Version);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(migration.Script, cancellationToken: cancellationToken));
            await MarkAppliedAsync(migration.Version, cancellationToken);
        }
    }

    private static IReadOnlyCollection<MigrationScript> LoadScripts()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly
            .GetManifestResourceNames()
            .Where(name => name.Contains("Storage.Migrations.", StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Не найден ресурс миграции: {name}");
                using var reader = new StreamReader(stream);
                var script = reader.ReadToEnd();
                var version = name[(name.LastIndexOf("V", StringComparison.Ordinal) + 1)..];
                version = version[..(version.Length - 4)];
                return new MigrationScript(version, script);
            })
            .ToArray();
    }

    private NpgsqlDataSource GetTargetDataSource(string version)
    {
        return version.StartsWith("001", StringComparison.Ordinal)
            || version.StartsWith("004", StringComparison.Ordinal)
            || version.StartsWith("005", StringComparison.Ordinal)
            || version.StartsWith("006", StringComparison.Ordinal)
            || version.StartsWith("008", StringComparison.Ordinal)
            ? _metadataDataSource.DataSource
            : _timeseriesDataSource.DataSource;
    }

    private async Task EnsureMigrationTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            create schema if not exists meta;

            create table if not exists meta.schema_migrations
            (
                version text primary key,
                applied_at timestamptz not null default now()
            );
            """;

        await using var connection = await _metadataDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private async Task<bool> IsAppliedAsync(string version, CancellationToken cancellationToken)
    {
        const string sql = """
            select exists(select 1 from meta.schema_migrations where version = @Version)
            """;

        await using var connection = await _metadataDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { Version = version }, cancellationToken: cancellationToken));
    }

    private async Task MarkAppliedAsync(string version, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into meta.schema_migrations(version) values (@Version)
            """;

        await using var connection = await _metadataDataSource.DataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Version = version }, cancellationToken: cancellationToken));
    }

    private readonly record struct MigrationScript(string Version, string Script);
}
