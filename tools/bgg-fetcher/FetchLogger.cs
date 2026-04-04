using System.Text;

namespace BggFetcher;

public sealed class FetchLogger : IDisposable
{
    private readonly StringBuilder _log = new();
    private readonly string _logDir;
    private readonly DateTime _startTime;
    private int _success, _warnings, _failures, _skipped;

    public FetchLogger(string logDir, string manifestName, bool forceMode)
    {
        _logDir = logDir;
        _startTime = DateTime.UtcNow;
        _log.AppendLine($"=== BGG Fetch Report - {_startTime:yyyy-MM-dd HH:mm:ss} ===");
        _log.AppendLine($"Manifest: {manifestName}.yml");
        _log.AppendLine($"Mode: {(forceMode ? "force" : "incremental")}");
        _log.AppendLine();
    }

    public void LogOk(int i, int total, string title, int bggId)
    { var l = $"[OK]   [{i,3}/{total}] {title} (bggId: {bggId})"; _log.AppendLine(l); Console.WriteLine(l); _success++; }

    public void LogWarn(int i, int total, string title, int bggId, string reason)
    { var l = $"[WARN] [{i,3}/{total}] {title} (bggId: {bggId}) - {reason}"; _log.AppendLine(l); Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine(l); Console.ResetColor(); _warnings++; }

    public void LogFail(int i, int total, string title, int bggId, string reason)
    { var l = $"[FAIL] [{i,3}/{total}] {title} (bggId: {bggId}) - {reason}"; _log.AppendLine(l); Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine(l); Console.ResetColor(); _failures++; }

    public void LogSkip(int i, int total, string title, int bggId)
    { var l = $"[SKIP] [{i,3}/{total}] {title} (bggId: {bggId}) - already enhanced"; _log.AppendLine(l); Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine(l); Console.ResetColor(); _skipped++; }

    public void Dispose()
    {
        var dur = DateTime.UtcNow - _startTime;
        _log.AppendLine($"\n=== Summary ===\nTotal: {_success+_warnings+_failures+_skipped}\nSuccess: {_success}\nWarnings: {_warnings}\nFailed: {_failures}\nSkipped: {_skipped}\nDuration: {dur:m\\m\\ ss\\s}");
        Console.WriteLine($"\n=== Summary: {_success} OK, {_warnings} WARN, {_failures} FAIL, {_skipped} SKIP ({dur:m\\m\\ ss\\s}) ===");
        Directory.CreateDirectory(_logDir);
        var logFile = Path.Combine(_logDir, $"fetch-{_startTime:yyyy-MM-ddTHH-mm-ss}.log");
        File.WriteAllText(logFile, _log.ToString());
        Console.WriteLine($"Log: {logFile}");
    }
}
