using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AutoBackup.Services;

public sealed class RarRunResult
{
    public int ExitCode { get; set; }
    public string Output { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Warning
    {
        get { return ExitCode == 1; }
    }
}

public sealed class AutoBackupException : Exception
{
    public AutoBackupException(string message) : base(message) { }
    public AutoBackupException(string message, Exception inner) : base(message, inner) { }
}

public static class RarRunner
{
    public static async Task<RarRunResult> RunAsync(
        string rarPath,
        string arguments,
        string workingDirectory,
        Action<double, string> onProgress,
        CancellationToken token)
    {
        if (!File.Exists(rarPath))
        {
            throw new AutoBackupException("WinRAR's Rar.exe was not found at:\n" + rarPath);
        }
        if (!Directory.Exists(workingDirectory))
        {
            throw new AutoBackupException("The working folder does not exist:\n" + workingDirectory);
        }

        var sync = SynchronizationContext.Current;
        var output = new StringBuilder();
        var gate = new object();
        var state = new ParseState();
        var stopwatch = Stopwatch.StartNew();
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = rarPath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Default,
                StandardErrorEncoding = Encoding.Default,
                WindowStyle = ProcessWindowStyle.Hidden
            },
            EnableRaisingEvents = true
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            process.Dispose();
            throw new AutoBackupException("WinRAR could not be started. " + ex.Message, ex);
        }

        using (token.Register(delegate
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        }))
        {
            var stdout = PumpAsync(process.StandardOutput, output, gate, sync, onProgress, state);
            var stderr = PumpAsync(process.StandardError, output, gate, sync, onProgress, state);
            await Task.Run(delegate { process.WaitForExit(); }).ConfigureAwait(true);
            try { await Task.WhenAll(stdout, stderr).ConfigureAwait(true); } catch { }
            stopwatch.Stop();

            var exitCode = -1;
            try { exitCode = process.ExitCode; } catch { }
            process.Dispose();

            if (token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }

            var text = string.Empty;
            lock (gate) { text = output.ToString(); }

            if (exitCode != 0 && exitCode != 1)
            {
                throw new AutoBackupException("WinRAR failed with exit code " + exitCode + "." + FormatTail(text));
            }

            if (onProgress != null)
            {
                Report(sync, onProgress, 100, null);
            }

            return new RarRunResult
            {
                ExitCode = exitCode,
                Output = text,
                Duration = stopwatch.Elapsed
            };
        }
    }

    private static async Task PumpAsync(
        StreamReader reader,
        StringBuilder output,
        object gate,
        SynchronizationContext sync,
        Action<double, string> onProgress,
        ParseState state)
    {
        var buffer = new char[2048];
        while (true)
        {
            int read;
            try
            {
                read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }
            if (read <= 0)
            {
                break;
            }

            var chunk = new string(buffer, 0, read);
            lock (gate)
            {
                if (output.Length < 300000)
                {
                    output.Append(chunk);
                }
            }

            if (onProgress == null)
            {
                continue;
            }

            var clean = chunk.Replace("\b", " ").Replace('\r', ' ');
            foreach (Match percent in Regex.Matches(clean, @"(\d{1,3})\s*%"))
            {
                int value;
                if (int.TryParse(percent.Groups[1].Value, out value))
                {
                    value = Math.Max(0, Math.Min(100, value));
                    if (value > state.Percent)
                    {
                        state.Percent = value;
                        Report(sync, onProgress, value, null);
                    }
                }
            }

            var statusMatch = Regex.Match(clean, @"(Adding|Extracting|Creating|Updating|Replacing)\s{2,}([^\n]+)");
            if (statusMatch.Success)
            {
                var detail = statusMatch.Groups[2].Value.Trim();
                var cut = Regex.Match(detail, @"\s{3,}\d{1,3}%");
                if (cut.Success)
                {
                    detail = detail.Substring(0, cut.Index).TrimEnd();
                }
                detail = detail.Trim();
                if (detail.Length > 0 && !string.Equals(detail, state.Status, StringComparison.Ordinal))
                {
                    state.Status = detail;
                    var label = statusMatch.Groups[1].Value + " " + detail;
                    if (label.Length > 120)
                    {
                        label = label.Substring(0, 117) + "...";
                    }
                    Report(sync, onProgress, state.Percent, label);
                }
            }
        }
    }

    private static void Report(SynchronizationContext sync, Action<double, string> callback, double percent, string status)
    {
        if (callback == null)
        {
            return;
        }
        if (sync != null)
        {
            sync.Post(delegate
            {
                try { callback(percent, status); } catch { }
            }, null);
        }
        else
        {
            try { callback(percent, status); } catch { }
        }
    }

    private static string FormatTail(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "\n\nNo details were returned by WinRAR.";
        }
        var cleaned = text.Replace("\b", string.Empty).Replace("\r", string.Empty).Trim();
        if (cleaned.Length > 2200)
        {
            cleaned = cleaned.Substring(cleaned.Length - 2200);
        }
        return "\n\n" + cleaned;
    }

    private sealed class ParseState
    {
        public double Percent;
        public string Status;
    }
}
