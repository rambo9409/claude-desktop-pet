using System;
using System.IO;
using System.Text;

namespace ClaudePet
{
    // Shared persistence helpers. Every settings file lives under
    // %LocalAppData%\KoloPet and is written through a temporary file so a
    // interrupted write does not replace the existing file with partial text.
    internal static class PetStorage
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private const long DiagnosticsLimit = 256 * 1024;
        private static readonly object DiagnosticsLock = new object();

        internal static string RootDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoloPet"); }
        }

        internal static string DiagnosticsPath { get { return Path.Combine(RootDirectory, "diagnostics.log"); } }

        // Writes text to a sibling temporary file, then swaps it into place.
        // Throws on failure; callers that must not fail use TryWriteAllText.
        internal static void WriteAllTextAtomic(string path, string text)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("path");
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, Utf8))
                {
                    writer.Write(text ?? "");
                    writer.Flush();
                    stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    // A failed replacement must preserve the old file. Copying
                    // over it here would discard the atomic-write guarantee.
                    File.Replace(temporary, path, null, true);
                }
                else File.Move(temporary, path);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            }
        }

        internal static bool TryWriteAllText(string path, string text, string context)
        {
            try { WriteAllTextAtomic(path, text); return true; }
            catch (Exception error) { Log(context, error); return false; }
        }

        internal static string TryReadAllText(string path, long maximumBytes, string context)
        {
            try
            {
                if (!File.Exists(path)) return null;
                if (new FileInfo(path).Length > maximumBytes) { Log(context, new IOException("File is larger than " + maximumBytes + " bytes: " + path)); return null; }
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception error) { Log(context, error); return null; }
        }

        // Keeps a copy of an unreadable settings file so the user's data is
        // never silently replaced by defaults. Returns the backup path or null.
        internal static string Quarantine(string path, string context)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string backup = path + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json";
                File.Copy(path, backup, false);
                Log(context, new InvalidDataException("Unreadable file preserved as " + backup));
                return backup;
            }
            catch (Exception error) { Log(context, error); return null; }
        }

        // Appends a short line to diagnostics.log. Never throws. The log is
        // rotated once to diagnostics.old.log when it reaches 256 KB.
        internal static void Log(string context, Exception error)
        {
            try
            {
                lock (DiagnosticsLock)
                {
                    Directory.CreateDirectory(RootDirectory);
                    string path = DiagnosticsPath;
                    if (File.Exists(path) && new FileInfo(path).Length > DiagnosticsLimit)
                    {
                        string old = Path.Combine(RootDirectory, "diagnostics.old.log");
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                    string line = DateTime.Now.ToString("s") + " [" + (context ?? "general") + "] "
                        + (error == null ? "" : error.GetType().Name + ": " + error.Message) + Environment.NewLine;
                    File.AppendAllText(path, line, Utf8);
                }
            }
            catch { }
        }
    }
}
