using System.ComponentModel;
using CataMedia.Core;

namespace CataMedia.Desktop;

public sealed class QueueEntry(QueueItem item) : INotifyPropertyChanged
{
    public QueueItem Item { get; } = item;
    public bool RequiresInspection { get; init; }
    public string Title => Item.Title;
    public QueueItemState State { get; private set; } = QueueItemState.Pending;
    public MediaDownloadResult? Result { get; private set; }
    public string? Error { get; private set; }
    public string Format { get; private set; } = "";
    public string Status { get; private set; } = "";
    private DownloadProgress? progress;

    public void Update(QueueUpdate update, string language)
    {
        State = update.State;
        progress = update.Progress;
        Result = update.Result;
        Error = update.Error;
        Refresh(language);
    }

    public void Refresh(string language)
    {
        var request = Item.Request;
        Format = request.Mode == MediaMode.Video ? "MP4 · " + UiStrings.Get(request.MaximumHeight == 720 ? "UpTo720" : "UpTo1080", language)
            : request.AudioFormat.ToString().ToUpperInvariant() + (request.AudioFormat == AudioFormat.Mp3
                ? " · " + UiStrings.Get("Short" + request.AudioQuality, language) : "");
        Status = UiStrings.Get(State.ToString(), language);
        if (State == QueueItemState.Running && progress is not null)
        {
            Status = UiStrings.Get(progress.Stage.ToString(), language);
            if (progress.Percentage is { } percentage) Status += $" {percentage:0}%";
        }
        if (Result is { Mode: MediaMode.Video } result) Status += $" · {result.ActualHeight}p";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Format)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
