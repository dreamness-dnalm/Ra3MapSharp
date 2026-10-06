using System.Security.Cryptography;

namespace Dreamness.RA3.Map.Automation.Storage;

internal static class ContentHasher
{
    public static string HashBytes(byte[] bytes) => Format(SHA256.HashData(bytes));

    public static string HashMap(Dreamness.Ra3.Map.Facade.Core.Ra3MapFacade map) =>
        HashBytes(map.ra3Map.Context.ToBytes());

    public static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                   .ConfigureAwait(false)) > 0)
        {
            sha.AppendData(buffer.AsSpan(0, read));
        }

        return Format(sha.GetHashAndReset());
    }

    public static async Task<string> CopyAndHashAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                   .ConfigureAwait(false)) > 0)
        {
            sha.AppendData(buffer.AsSpan(0, read));
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
        }

        return Format(sha.GetHashAndReset());
    }

    private static string Format(byte[] hash)
    {
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
