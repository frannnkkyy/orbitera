using Microsoft.Data.Sqlite;
using Orbitera.Application;
using Orbitera.Domain;
using System.Globalization;
using System.IO;

namespace Orbitera.Infrastructure;

public sealed class SqliteSnapshotRepository : ISnapshotRepository
{
    private const string Schema = """
        PRAGMA foreign_keys = ON;
        CREATE TABLE IF NOT EXISTS snapshot_roots (
            id INTEGER PRIMARY KEY,
            root_path_key TEXT NOT NULL UNIQUE,
            root_path TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS snapshots (
            id INTEGER PRIMARY KEY,
            root_id INTEGER NOT NULL REFERENCES snapshot_roots(id),
            captured_utc TEXT NOT NULL,
            total_size_bytes INTEGER NOT NULL,
            file_count INTEGER NOT NULL,
            is_complete INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_snapshots_root_captured
            ON snapshots(root_id, captured_utc DESC);
        CREATE TABLE IF NOT EXISTS snapshot_files (
            snapshot_id INTEGER NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
            relative_path_key TEXT NOT NULL,
            relative_path TEXT NOT NULL,
            size_bytes INTEGER NOT NULL,
            last_modified_utc TEXT,
            PRIMARY KEY(snapshot_id, relative_path_key)
        ) WITHOUT ROWID;
        PRAGMA user_version = 1;
        """;

    private readonly string _databasePath;

    public SqliteSnapshotRepository()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Orbitera",
            "orbitera.db"))
    {
    }

    public SqliteSnapshotRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
    }

    public Task<StorageSnapshotSummary> SaveAsync(
        StorageNode root,
        DateTime capturedUtc,
        bool isComplete,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        return ExecuteSafelyAsync(() => SaveCore(root, capturedUtc, isComplete, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<SnapshotRoot>> GetRootsAsync(CancellationToken cancellationToken) =>
        ExecuteSafelyAsync<IReadOnlyList<SnapshotRoot>>(GetRootsCore, cancellationToken);

    public Task<IReadOnlyList<StorageSnapshotSummary>> GetSnapshotsAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        return ExecuteSafelyAsync(
            () => GetSnapshotsCore(rootPath, cancellationToken),
            cancellationToken);
    }

    public Task<StorageSnapshot> GetSnapshotAsync(long snapshotId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(snapshotId);
        return ExecuteSafelyAsync(
            () => GetSnapshotCore(snapshotId, cancellationToken),
            cancellationToken);
    }

    public Task<SnapshotComparison> CompareAsync(
        long previousSnapshotId,
        long currentSnapshotId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(previousSnapshotId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(currentSnapshotId);
        return ExecuteSafelyAsync(
            () => CompareCore(previousSnapshotId, currentSnapshotId, cancellationToken),
            cancellationToken);
    }

    private StorageSnapshotSummary SaveCore(
        StorageNode root,
        DateTime capturedUtc,
        bool isComplete,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var rootPath = Path.GetFullPath(root.FullPath);
        var rootPathKey = NormalizeRootKey(rootPath);

        using (var addRoot = connection.CreateCommand())
        {
            addRoot.Transaction = transaction;
            addRoot.CommandText = """
                INSERT INTO snapshot_roots(root_path_key, root_path)
                VALUES ($rootPathKey, $rootPath)
                ON CONFLICT(root_path_key) DO NOTHING;
                """;
            addRoot.Parameters.AddWithValue("$rootPathKey", rootPathKey);
            addRoot.Parameters.AddWithValue("$rootPath", rootPath);
            addRoot.ExecuteNonQuery();
        }

        long rootId;
        using (var findRoot = connection.CreateCommand())
        {
            findRoot.Transaction = transaction;
            findRoot.CommandText = "SELECT id FROM snapshot_roots WHERE root_path_key = $rootPathKey;";
            findRoot.Parameters.AddWithValue("$rootPathKey", rootPathKey);
            rootId = (long)findRoot.ExecuteScalar()!;
        }

        var captured = capturedUtc.ToUniversalTime();
        long snapshotId;
        using (var addSnapshot = connection.CreateCommand())
        {
            addSnapshot.Transaction = transaction;
            addSnapshot.CommandText = """
                INSERT INTO snapshots(root_id, captured_utc, total_size_bytes, file_count, is_complete)
                VALUES ($rootId, $capturedUtc, $totalSizeBytes, 0, $isComplete);
                SELECT last_insert_rowid();
                """;
            addSnapshot.Parameters.AddWithValue("$rootId", rootId);
            addSnapshot.Parameters.AddWithValue("$capturedUtc", FormatDate(captured));
            addSnapshot.Parameters.AddWithValue("$totalSizeBytes", root.SizeBytes);
            addSnapshot.Parameters.AddWithValue("$isComplete", isComplete ? 1 : 0);
            snapshotId = (long)addSnapshot.ExecuteScalar()!;
        }

        using var addFile = connection.CreateCommand();
        addFile.Transaction = transaction;
        addFile.CommandText = """
            INSERT INTO snapshot_files(
                snapshot_id, relative_path_key, relative_path, size_bytes, last_modified_utc)
            VALUES ($snapshotId, $relativePathKey, $relativePath, $sizeBytes, $lastModifiedUtc);
            """;
        var snapshotIdParameter = addFile.Parameters.Add("$snapshotId", SqliteType.Integer);
        var pathKeyParameter = addFile.Parameters.Add("$relativePathKey", SqliteType.Text);
        var pathParameter = addFile.Parameters.Add("$relativePath", SqliteType.Text);
        var sizeParameter = addFile.Parameters.Add("$sizeBytes", SqliteType.Integer);
        var modifiedParameter = addFile.Parameters.Add("$lastModifiedUtc", SqliteType.Text);
        snapshotIdParameter.Value = snapshotId;

        long fileCount = 0;
        var directories = new Stack<StorageNode>();
        directories.Push(root);

        while (directories.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var child in directory.Children)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (child.IsDirectory)
                {
                    directories.Push(child);
                    continue;
                }

                var relativePath = NormalizeRelativePath(Path.GetRelativePath(rootPath, child.FullPath));
                pathKeyParameter.Value = relativePath.ToUpperInvariant();
                pathParameter.Value = relativePath;
                sizeParameter.Value = child.SizeBytes;
                modifiedParameter.Value = child.LastModifiedUtc is { } modified
                    ? FormatDate(modified)
                    : DBNull.Value;
                addFile.ExecuteNonQuery();
                fileCount++;
            }
        }

        using (var updateSnapshot = connection.CreateCommand())
        {
            updateSnapshot.Transaction = transaction;
            updateSnapshot.CommandText = "UPDATE snapshots SET file_count = $fileCount WHERE id = $snapshotId;";
            updateSnapshot.Parameters.AddWithValue("$fileCount", fileCount);
            updateSnapshot.Parameters.AddWithValue("$snapshotId", snapshotId);
            updateSnapshot.ExecuteNonQuery();
        }

        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();

        return new StorageSnapshotSummary(snapshotId, rootPath, captured, root.SizeBytes, fileCount, isComplete);
    }

    private IReadOnlyList<SnapshotRoot> GetRootsCore()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT roots.root_path
            FROM snapshot_roots AS roots
            INNER JOIN snapshots ON snapshots.root_id = roots.id
            GROUP BY roots.id, roots.root_path
            ORDER BY roots.root_path COLLATE NOCASE;
            """;
        using var reader = command.ExecuteReader();
        var roots = new List<SnapshotRoot>();

        while (reader.Read())
        {
            var path = reader.GetString(0);
            roots.Add(new SnapshotRoot(path, path));
        }

        return roots;
    }

    private IReadOnlyList<StorageSnapshotSummary> GetSnapshotsCore(
        string rootPath,
        CancellationToken cancellationToken)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT snapshots.id, roots.root_path, snapshots.captured_utc,
                   snapshots.total_size_bytes, snapshots.file_count, snapshots.is_complete
            FROM snapshots
            INNER JOIN snapshot_roots AS roots ON roots.id = snapshots.root_id
            WHERE roots.root_path_key = $rootPathKey
            ORDER BY snapshots.captured_utc DESC, snapshots.id DESC;
            """;
        command.Parameters.AddWithValue("$rootPathKey", NormalizeRootKey(rootPath));
        using var reader = command.ExecuteReader();
        var snapshots = new List<StorageSnapshotSummary>();

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            snapshots.Add(ReadSummary(reader));
        }

        return snapshots;
    }

    private StorageSnapshot GetSnapshotCore(long snapshotId, CancellationToken cancellationToken)
    {
        using var connection = OpenConnection();
        StorageSnapshotSummary summary;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT snapshots.id, roots.root_path, snapshots.captured_utc,
                       snapshots.total_size_bytes, snapshots.file_count, snapshots.is_complete
                FROM snapshots
                INNER JOIN snapshot_roots AS roots ON roots.id = snapshots.root_id
                WHERE snapshots.id = $snapshotId;
                """;
            command.Parameters.AddWithValue("$snapshotId", snapshotId);
            using var reader = command.ExecuteReader();

            if (!reader.Read())
            {
                throw new KeyNotFoundException($"Snapshot {snapshotId} does not exist.");
            }

            summary = ReadSummary(reader);
        }

        var files = new List<SnapshotFileEntry>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT relative_path, size_bytes, last_modified_utc
                FROM snapshot_files
                WHERE snapshot_id = $snapshotId
                ORDER BY relative_path_key;
                """;
            command.Parameters.AddWithValue("$snapshotId", snapshotId);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                files.Add(new SnapshotFileEntry(
                    reader.GetString(0),
                    reader.GetInt64(1),
                    reader.IsDBNull(2) ? null : ParseDate(reader.GetString(2))));
            }
        }

        return new StorageSnapshot(summary, files);
    }

    private SnapshotComparison CompareCore(
        long previousSnapshotId,
        long currentSnapshotId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StorageSnapshotSummary previous;
        StorageSnapshotSummary current;

        using (var connection = OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT snapshots.id, roots.root_path, snapshots.captured_utc,
                       snapshots.total_size_bytes, snapshots.file_count, snapshots.is_complete
                FROM snapshots
                INNER JOIN snapshot_roots AS roots ON roots.id = snapshots.root_id
                WHERE snapshots.id IN ($previousId, $currentId);
                """;
            command.Parameters.AddWithValue("$previousId", previousSnapshotId);
            command.Parameters.AddWithValue("$currentId", currentSnapshotId);
            using var reader = command.ExecuteReader();
            var found = new Dictionary<long, StorageSnapshotSummary>();

            while (reader.Read())
            {
                var summary = ReadSummary(reader);
                found.Add(summary.Id, summary);
            }

            if (!found.TryGetValue(previousSnapshotId, out previous!) ||
                !found.TryGetValue(currentSnapshotId, out current!))
            {
                throw new KeyNotFoundException("One or both selected snapshots no longer exist.");
            }
        }

        if (!previous.IsComplete || !current.IsComplete)
        {
            throw new InvalidOperationException(
                "Snapshots with inaccessible directories or skipped reparse points cannot be compared reliably.");
        }

        var previousFiles = ReadOrderedFiles(previousSnapshotId, cancellationToken);
        var currentFiles = ReadOrderedFiles(currentSnapshotId, cancellationToken);
        return StorageSnapshotComparer.CompareSorted(previous, current, previousFiles, currentFiles);
    }

    private IEnumerable<SnapshotFileEntry> ReadOrderedFiles(
        long snapshotId,
        CancellationToken cancellationToken)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT relative_path, size_bytes, last_modified_utc
            FROM snapshot_files
            WHERE snapshot_id = $snapshotId
            ORDER BY relative_path_key;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new SnapshotFileEntry(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.IsDBNull(2) ? null : ParseDate(reader.GetString(2)));
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 10
        }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = Schema;
        command.ExecuteNonQuery();
        return connection;
    }

    private static StorageSnapshotSummary ReadSummary(SqliteDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetString(1),
            ParseDate(reader.GetString(2)),
            reader.GetInt64(3),
            reader.GetInt64(4),
            reader.GetInt64(5) != 0);

    private static string NormalizeRelativePath(string path) =>
        path.Replace(Path.AltDirectorySeparatorChar, '\\')
            .Replace(Path.DirectorySeparatorChar, '\\')
            .TrimStart('\\');

    private static string NormalizeRootKey(string rootPath) =>
        Path.GetFullPath(rootPath)
            .Replace(Path.AltDirectorySeparatorChar, '\\')
            .TrimEnd('\\')
            .ToUpperInvariant();

    private static string FormatDate(DateTime value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTime ParseDate(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();

    private static async Task<T> ExecuteSafelyAsync<T>(
        Func<T> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(operation, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            throw new SnapshotPersistenceException("Orbit History could not access its local SQLite database.", exception);
        }
    }
}
