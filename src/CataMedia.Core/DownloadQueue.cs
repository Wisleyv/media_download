using System.ComponentModel;

namespace CataMedia.Core;

public sealed record QueueItem(Guid Id, string Title, MediaDownloadRequest Request);
public enum QueueItemState { Pending, Running, Completed, Failed, Cancelled }
public sealed record QueueUpdate(Guid Id, QueueItemState State, DownloadProgress? Progress = null,
    MediaDownloadResult? Result = null, string? Error = null);
public sealed record QueueRunResult(IReadOnlyList<QueueUpdate> Results, bool Cancelled);

public sealed class DownloadQueue(IMediaDownloadService service)
{
    public async Task<QueueRunResult> RunAsync(IReadOnlyList<QueueItem> items, DownloadTools tools,
        IProgress<QueueUpdate>? updates, CancellationToken cancellationToken)
    {
        List<QueueUpdate> results = [];
        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested) return new(results, true);
            updates?.Report(new(item.Id, QueueItemState.Running));
            QueueUpdate outcome;
            try
            {
                var result = await service.DownloadAsync(item.Request, tools,
                    new ForwardProgress(progress => updates?.Report(new(item.Id, QueueItemState.Running, progress))), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                outcome = new(item.Id, QueueItemState.Completed, Result: result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                outcome = new(item.Id, QueueItemState.Cancelled);
                results.Add(outcome);
                updates?.Report(outcome);
                return new(results, true);
            }
            catch (Exception error) when (IsExpectedFailure(error))
            {
                outcome = new(item.Id, QueueItemState.Failed, Error: error.Message);
            }
            results.Add(outcome);
            updates?.Report(outcome);
        }
        return new(results, false);
    }

    public static bool IsExpectedFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        ArgumentException or InvalidDataException or Win32Exception;

    private sealed class ForwardProgress(Action<DownloadProgress> forward) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => forward(value);
    }
}
