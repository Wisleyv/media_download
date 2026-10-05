using System.Diagnostics;
using System.Text;

namespace CataMedia.Windows;

public sealed record ProcessCommand(string Executable, IReadOnlyList<string> Arguments, string WorkingDirectory);

public interface IProcessRunner
{
    Task<int> RunAsync(ProcessCommand command, Action<string> standardOutput,
        Action<string> standardError, CancellationToken cancellationToken);
}

public sealed class ExternalProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(ProcessCommand command, Action<string> standardOutput,
        Action<string> standardError, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(command.Executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = command.WorkingDirectory
        };
        foreach (var argument in command.Arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new IOException("Não foi possível iniciar o componente. / Could not start component.");
        using var registration = cancellationToken.Register(() => KillTree(process));
        try
        {
            await Task.WhenAll(ReadLinesAsync(process.StandardOutput, standardOutput, process),
                ReadLinesAsync(process.StandardError, standardError, process), process.WaitForExitAsync()).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return process.ExitCode;
        }
        catch
        {
            KillTree(process);
            await process.WaitForExitAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task ReadLinesAsync(StreamReader reader, Action<string> receive, Process process)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line) receive(line);
        }
        catch
        {
            KillTree(process);
            throw;
        }
    }

    private static void KillTree(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { } // Process already exited.
    }
}
