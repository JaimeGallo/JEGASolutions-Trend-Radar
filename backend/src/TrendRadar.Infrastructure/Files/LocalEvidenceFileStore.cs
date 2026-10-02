using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TrendRadar.Application.Signals;

namespace TrendRadar.Infrastructure.Files;

/// <summary>
/// Guarda archivos de evidencia en disco con su SHA-256 como nombre: el mismo archivo se guarda
/// una sola vez y cualquier alteración posterior se detecta al recalcular el hash.
/// </summary>
internal sealed partial class LocalEvidenceFileStore(IConfiguration configuration) : IEvidenceFileStore
{
    private readonly string _root = Path.GetFullPath(configuration["Evidence:StoragePath"] ?? Path.Combine("data", "evidence"));

    public async Task<(string Sha256, long Size)> SaveAsync(Stream content, CancellationToken ct)
    {
        var tmpDir = Path.Combine(_root, "tmp");
        Directory.CreateDirectory(tmpDir);
        var tmp = Path.Combine(tmpDir, Guid.NewGuid().ToString("N"));

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        await using (var output = File.Create(tmp))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                size += read;
            }
        }

        var sha = Convert.ToHexStringLower(hash.GetHashAndReset());
        var target = PathFor(sha);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target))
        {
            File.Delete(tmp);
        }
        else
        {
            File.Move(tmp, target);
        }

        return (sha, size);
    }

    public Stream? OpenRead(string sha256)
    {
        if (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
        {
            return null;
        }

        var path = PathFor(sha256);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    private string PathFor(string sha) => Path.Combine(_root, sha[..2], sha);
}
