using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace QDND.Data
{
    internal static class RuntimeSafety
    {
        private static readonly bool _isLikelyTestHost = DetectTestHostProcess();

        public static bool ShouldUseGodotInterop => !_isLikelyTestHost;

        public static void Log(string message)
        {
            if (ShouldUseGodotInterop)
            {
                Godot.GD.Print(message);
                return;
            }

            Console.WriteLine(message);
        }

        public static void LogError(string message)
        {
            if (ShouldUseGodotInterop)
            {
                Godot.GD.PrintErr(message);
                return;
            }

            Console.Error.WriteLine(message);
        }

        public static bool ResourceFileExists(string path)
        {
            if (!path.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                return File.Exists(path);
            }

            if (ShouldUseGodotInterop)
            {
                return Godot.FileAccess.FileExists(path);
            }

            return File.Exists(ToAbsolutePath(path));
        }

        public static bool TryReadText(string path, out string text)
        {
            text = null;

            if (!path.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(path))
                    return false;

                text = File.ReadAllText(path);
                return true;
            }

            if (ShouldUseGodotInterop)
            {
                using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                if (file == null)
                    return false;

                text = Encoding.UTF8.GetString(file.GetBuffer((long)file.GetLength()));
                return true;
            }

            var absolutePath = ToAbsolutePath(path);
            if (!File.Exists(absolutePath))
                return false;

            text = File.ReadAllText(absolutePath);
            return true;
        }

        private static bool DetectTestHostProcess()
        {
            try
            {
                var processName = Process.GetCurrentProcess().ProcessName;
                if (processName.Contains("testhost", StringComparison.OrdinalIgnoreCase) ||
                    processName.Contains("vstest", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                foreach (var arg in Environment.GetCommandLineArgs())
                {
                    if (arg.Contains("testhost", StringComparison.OrdinalIgnoreCase) ||
                        arg.Contains("vstest", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore process detection failures and default to Godot behavior.
            }

            return false;
        }

        private static string ToAbsolutePath(string path)
        {
            var relativePath = path.Substring("res://".Length)
                .Replace('/', Path.DirectorySeparatorChar);

            var current = Directory.GetCurrentDirectory();
            var probe = current;

            while (!string.IsNullOrEmpty(probe))
            {
                if (File.Exists(Path.Combine(probe, "project.godot")))
                {
                    return Path.Combine(probe, relativePath);
                }

                var parent = Directory.GetParent(probe);
                if (parent == null)
                    break;

                probe = parent.FullName;
            }

            return Path.GetFullPath(Path.Combine(current, relativePath));
        }
    }
}