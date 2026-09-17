using System.Text.Json;

namespace Dreamness.RA3.Map.Automation.Storage;

/// <summary>
/// Stages files before replacing them and rolls back completed replacements on failure.
/// This provides in-process rollback, not crash recovery across multiple files.
/// </summary>
internal sealed class FileTransaction
{
    private readonly List<(string Path, byte[] Bytes, bool CheckHash, string? ExpectedHash)> _files = new();

    public void Add(string path, byte[] bytes) => _files.Add((path, bytes, false, null));

    public void AddChecked(string path, byte[] bytes, string? expectedHash) =>
        _files.Add((path, bytes, true, expectedHash));

    public void AddJson<T>(string path, T value) =>
        Add(path, JsonSerializer.SerializeToUtf8Bytes(value, AutomationJson.Options));

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        var prepared = new List<(string Path, string Temp, string Backup, bool Existed)>();
        var replaced = 0;
        var rollbackFailed = false;
        try
        {
            foreach (var file in _files)
            {
                CheckExpectedFile(file.Path, file.CheckHash, file.ExpectedHash);
                Directory.CreateDirectory(Path.GetDirectoryName(file.Path)!);
                var suffix = "." + Guid.NewGuid().ToString("N");
                var entry = (file.Path, Temp: file.Path + suffix + ".tmp",
                    Backup: file.Path + suffix + ".bak", Existed: File.Exists(file.Path));
                prepared.Add(entry);
                await File.WriteAllBytesAsync(entry.Temp, file.Bytes, cancellationToken).ConfigureAwait(false);
                if (entry.Existed)
                    File.Copy(entry.Path, entry.Backup);
            }

            foreach (var entry in prepared)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = _files[replaced];
                CheckExpectedFile(file.Path, file.CheckHash, file.ExpectedHash);
                File.Move(entry.Temp, entry.Path, overwrite: entry.Existed);
                replaced++;
            }
            // No cancellable operations after the final replacement (the commit point).
        }
        catch (Exception failure)
        {
            var errors = new List<Exception>();
            for (var i = replaced - 1; i >= 0; i--)
            {
                var entry = prepared[i];
                try
                {
                    if (entry.Existed) File.Move(entry.Backup, entry.Path, overwrite: true);
                    else File.Delete(entry.Path);
                }
                catch (Exception rollback) { errors.Add(rollback); }
            }
            if (errors.Count > 0)
            {
                rollbackFailed = true;
                errors.Insert(0, failure);
                throw new AutomationException("IO_ERROR", "文件回滚失败，已保留备份文件，请检查工作区。",
                    innerException: new AggregateException(errors));
            }
            throw;
        }
        finally
        {
            foreach (var entry in prepared)
            {
                TryDelete(entry.Temp);
                if (!rollbackFailed) TryDelete(entry.Backup);
            }
        }
    }

    private static void CheckExpectedFile(string path, bool check, string? expectedHash)
    {
        if (!check) return;
        var actual = File.Exists(path) ? ContentHasher.HashBytes(File.ReadAllBytes(path)) : null;
        if (actual != expectedHash)
            throw new AutomationException("WORKSPACE_CONFLICT", $"保存目标已被外部修改: {path}");
    }

    internal static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
