using System;
using System.IO;

namespace SteamAccountManager.Core.System;

/// <summary>Writes a file atomically: write to a temp file in the same directory, then replace.</summary>
public interface IAtomicFile
{
    void Write(string path, Action<Stream> writeContent);
}

public sealed class AtomicFile : IAtomicFile
{
    public void Write(string path, Action<Stream> writeContent)
    {
        var fullPath = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        var temp = Path.Combine(dir, Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                writeContent(fs);
                fs.Flush(flushToDisk: true);
            }

            File.Move(temp, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); } catch { /* best effort */ }
            }
        }
    }
}
