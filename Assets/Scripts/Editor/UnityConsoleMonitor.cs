using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Text;

namespace EthraClone.Editor
{
    [InitializeOnLoad]
    public static class UnityConsoleMonitor
    {
        private static readonly string LogPath = Path.Combine(
            Application.dataPath, "..", "Logs", "console_output.log"
        );

        private static StreamWriter writer;
        private static readonly object fileLock = new object();

        static UnityConsoleMonitor()
        {
            EnsureLogDirectory();
            OpenWriter();
            Application.logMessageReceived += OnLogMessage;
            EditorApplication.quitting += Cleanup;
            WriteHeader();
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (writer == null) return;

            string prefix = type switch
            {
                LogType.Error     => "[ERROR]",
                LogType.Assert    => "[ASSERT]",
                LogType.Warning   => "[WARN]",
                LogType.Exception => "[EXCEPTION]",
                _                 => "[LOG]"
            };

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string line = $"{timestamp} {prefix} {message}";

            lock (fileLock)
            {
                try
                {
                    writer.WriteLine(line);
                    if (!string.IsNullOrEmpty(stackTrace) && type is LogType.Error or LogType.Exception)
                    {
                        writer.WriteLine($"         {stackTrace.Replace("\n", "\n         ")}");
                    }
                    writer.Flush();
                }
                catch { }
            }
        }

        private static void EnsureLogDirectory()
        {
            string dir = Path.GetDirectoryName(LogPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private static void OpenWriter()
        {
            try
            {
                writer = new StreamWriter(LogPath, append: false, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConsoleMonitor] Could not open log file: {ex.Message}");
            }
        }

        private static void WriteHeader()
        {
            lock (fileLock)
            {
                writer?.WriteLine($"=== Unity Console Log — Session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                writer?.WriteLine($"=== Project: LIT-ISO | Log path: {LogPath} ===");
                writer?.WriteLine();
                writer?.Flush();
            }
        }

        private static void Cleanup()
        {
            Application.logMessageReceived -= OnLogMessage;
            lock (fileLock)
            {
                writer?.WriteLine($"\n=== Session ended {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                writer?.Flush();
                writer?.Close();
                writer = null;
            }
        }

        [MenuItem("Tools/LIT-ISO/Diagnostics/Console/Open Log File", false, 340)]
        private static void OpenLogFile()
        {
            if (File.Exists(LogPath))
                System.Diagnostics.Process.Start("notepad.exe", LogPath);
            else
                Debug.LogWarning("[ConsoleMonitor] Log file not yet created.");
        }

        [MenuItem("Tools/LIT-ISO/Diagnostics/Console/Show Log Path", false, 341)]
        private static void ShowLogPath()
        {
            Debug.Log($"[ConsoleMonitor] Log file: {Path.GetFullPath(LogPath)}");
        }

        [MenuItem("Tools/LIT-ISO/Diagnostics/Console/Clear Log", false, 342)]
        private static void ClearLog()
        {
            lock (fileLock)
            {
                writer?.Close();
                OpenWriter();
                WriteHeader();
            }
            Debug.Log("[ConsoleMonitor] Log cleared.");
        }
    }
}
