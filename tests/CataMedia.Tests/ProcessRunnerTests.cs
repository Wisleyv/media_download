using System.Diagnostics;
using System.IO;
using CataMedia.Windows;

namespace CataMedia.Tests;

public sealed class ProcessRunnerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CataMedia-process-" + Guid.NewGuid().ToString("N"));
    private readonly string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");

    [Fact]
    public async Task ArgumentsArePassedVerbatimAndBothStreamsAreRead()
    {
        Directory.CreateDirectory(root);
        var script = Path.Combine(root, "fixture.ps1");
        File.WriteAllText(script, "param([string]$Text)\n[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)\n[Console]::WriteLine($Text)\n[Console]::Error.WriteLine('stderr')\nexit 7");
        var output = new List<string>();
        var error = new List<string>();
        const string argument = "Vídeo com espaços & \"aspas\" --exec";
        var exit = await new ExternalProcessRunner().RunAsync(new(powershell,
            ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, argument], root), output.Add, error.Add, default);
        Assert.Equal(7, exit);
        Assert.Contains(argument, output);
        Assert.Contains("stderr", error);
    }

    [Fact]
    public async Task CancellationTerminatesTheProcessAndItsChild()
    {
        Directory.CreateDirectory(root);
        var script = Path.Combine(root, "fixture.ps1");
        File.WriteAllText(script, "$child = Start-Process powershell.exe -ArgumentList '-NoProfile -Command Start-Sleep -Seconds 90' -WindowStyle Hidden -PassThru\n[Console]::WriteLine($child.Id)\nStart-Sleep -Seconds 90");
        var childStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var task = new ExternalProcessRunner().RunAsync(new(powershell,
            ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script], root),
            line => { if (int.TryParse(line, out var pid)) childStarted.TrySetResult(pid); }, _ => { }, cancellation.Token);
        try
        {
            // Slow CI startup is separate from the cancellation deadline below.
            var childId = await childStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(10)));
            for (var attempt = 0; attempt < 30 && IsRunning(childId); attempt++) await Task.Delay(100);
            Assert.False(IsRunning(childId));
        }
        finally
        {
            cancellation.Cancel();
            try { await task.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (OperationCanceledException) { }
            if (childStarted.Task.IsCompletedSuccessfully)
            {
                var childId = await childStarted.Task;
                if (IsRunning(childId))
                {
                    using var child = Process.GetProcessById(childId);
                    child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
            }
        }
    }

    private static bool IsRunning(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}
