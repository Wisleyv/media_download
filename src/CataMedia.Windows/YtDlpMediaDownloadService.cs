using System.Globalization;
using System.Text;
using System.Text.Json;
using CataMedia.Core;

namespace CataMedia.Windows;

public sealed class YtDlpMediaDownloadService(IProcessRunner runner) : IMediaDownloadService
{
    public async Task<MediaDownloadResult> DownloadAsync(MediaDownloadRequest request, DownloadTools tools,
        IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var destination = Path.GetFullPath(request.DestinationFolder);
        if (!Directory.Exists(destination))
            throw new DirectoryNotFoundException("A pasta de destino não existe. / Destination folder does not exist.");
        foreach (var path in new[] { tools.YtDlpPath, Path.Combine(tools.FfmpegDirectory, "ffmpeg.exe"),
                     Path.Combine(tools.FfmpegDirectory, "ffprobe.exe") })
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
                throw new FileNotFoundException("Selecione yt-dlp, FFmpeg e FFprobe instalados. / Select installed yt-dlp, FFmpeg and FFprobe.");
        if (tools.NodePath is not null && (!Path.IsPathFullyQualified(tools.NodePath) || !File.Exists(tools.NodePath)))
            throw new FileNotFoundException("O Node.js selecionado não foi encontrado. / Selected Node.js was not found.");

        // Check permissions before any network transfer, without leaving a log or replacing user files.
        var writeCheck = Path.Combine(destination, ".catamedia-write-" + Guid.NewGuid().ToString("N"));
        using (new FileStream(writeCheck, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }

        progress?.Report(new(DownloadStage.Preparing));
        string? completedFile = null;
        var diagnostics = new Queue<string>();
        bool ConsumeProtocol(string line)
        {
            if (line.StartsWith("CATAMEDIA_RESULT:", StringComparison.Ordinal))
            {
                try { completedFile = JsonSerializer.Deserialize<string>(line[17..]); }
                catch (JsonException error) { throw new InvalidDataException("Invalid final-file response.", error); }
            }
            else if (line.StartsWith("CATAMEDIA_PROGRESS:", StringComparison.Ordinal))
                ReportProgress(line[19..], progress);
            else if (line.StartsWith("CATAMEDIA_PROCESSING", StringComparison.Ordinal))
                progress?.Report(new(DownloadStage.Processing));
            else return false;
            return true;
        }
        var exitCode = await runner.RunAsync(CreateCommand(request, tools, destination), line => { ConsumeProtocol(line); }, line =>
        {
            if (ConsumeProtocol(line)) return;
            // Session-only, bounded diagnostics; no file logging and no inference of success from text.
            if (diagnostics.Count == 8) diagnostics.Dequeue();
            diagnostics.Enqueue(line.Length > 1000 ? line[..1000] : line);
        }, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (exitCode != 0)
            throw new IOException("O download falhou. Confira o link, a rede e as dependências. / Download failed. Check link, network and dependencies.\n" +
                string.Join('\n', diagnostics));
        if (string.IsNullOrEmpty(completedFile))
            throw new InvalidDataException("O motor não confirmou um arquivo final. / Engine did not confirm a final file.");

        var file = Path.GetFullPath(completedFile);
        var relative = Path.GetRelativePath(destination, file);
        var extension = request.Mode == MediaMode.Video ? ".mp4" : "." + request.AudioFormat.ToString().ToLowerInvariant();
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relative) || !string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(file) || new FileInfo(file).Length == 0)
            throw new InvalidDataException("O arquivo MP4 final não pôde ser confirmado. / Final MP4 file could not be confirmed.");

        progress?.Report(new(DownloadStage.Verifying));
        var metadata = new StringBuilder();
        using var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var probeExit = await runner.RunAsync(new(Path.Combine(tools.FfmpegDirectory, "ffprobe.exe"),
                ["-v", "error", "-show_entries", "stream=codec_type,codec_name,height:format=format_name,duration", "-of", "json", file], destination),
                line =>
                {
                    if (metadata.Length + line.Length > 1_000_000) throw new InvalidDataException("Invalid media metadata.");
                    metadata.AppendLine(line);
                }, _ => { }, probeTimeout.Token).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (probeExit != 0) throw new InvalidDataException("Não foi possível validar o vídeo. / Could not validate video.");
            try { return ValidateMedia(file, metadata.ToString(), request); }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                throw new InvalidDataException("Metadados de vídeo inválidos. / Invalid video metadata.", error);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IOException("A verificação do vídeo demorou demais. / Video validation timed out.");
        }
    }

    public static ProcessCommand CreateCommand(MediaDownloadRequest request, DownloadTools tools, string destination)
    {
        request.Validate();
        var height = request.MaximumHeight.ToString(CultureInfo.InvariantCulture);
        List<string> arguments = ["--ignore-config", "--no-plugin-dirs", "--no-cache-dir", "--no-playlist", "--playlist-items", "1",
            "--no-overwrites", "--windows-filenames", "--encoding", "utf-8", "--newline", "--progress", "--no-simulate",
            "--socket-timeout", "15", "--retries", "3", "--no-js-runtimes", "--no-remote-components",
            "--ffmpeg-location", tools.FfmpegDirectory,
            "--progress-template", "download:CATAMEDIA_PROGRESS:%(progress)j",
            "--progress-template", "postprocess:CATAMEDIA_PROCESSING",
            "--print", "after_move:CATAMEDIA_RESULT:%(filepath)j"];
        var outputName = "%(title).150B [%(id)s]";
        if (request.Mode == MediaMode.Video)
            arguments.AddRange(["-f", $"bv[height<={height}][ext=mp4]+ba[ext=m4a]/b[height<={height}][ext=mp4]", "--merge-output-format", "mp4"]);
        else
        {
            arguments.AddRange(["-f", "ba/b", "-x", "--audio-format", request.AudioFormat.ToString().ToLowerInvariant()]);
            if (request.AudioFormat == AudioFormat.Mp3)
            {
                var quality = request.AudioQuality switch { AudioQuality.Low => "7", AudioQuality.High => "0", _ => "5" };
                arguments.AddRange(["--audio-quality", quality]);
                outputName += " - " + request.AudioQuality.ToString().ToLowerInvariant();
            }
        }
        arguments.AddRange(["-o", Path.Combine(destination, outputName + ".%(ext)s")]);
        if (request.CookiesBrowser is not null) arguments.AddRange(["--cookies-from-browser", request.CookiesBrowser]);
        if (tools.NodePath is not null) arguments.AddRange(["--js-runtimes", "node:" + tools.NodePath]);
        arguments.AddRange(["--", request.Url]);
        return new(tools.YtDlpPath, arguments, destination);
    }

    private static void ReportProgress(string json, IProgress<DownloadProgress>? progress)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var data = document.RootElement;
            if (data.ValueKind != JsonValueKind.Object) return;
            var downloaded = Number(data, "downloaded_bytes");
            var total = Number(data, "total_bytes");
            if (total <= 0) total = Number(data, "total_bytes_estimate");
            progress?.Report(new(DownloadStage.Downloading, total > 0 ? Math.Clamp(downloaded / total * 100, 0, 100) : null));
        }
        catch (JsonException) { progress?.Report(new(DownloadStage.Downloading)); }
    }

    private static double Number(JsonElement data, string key) =>
        data.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetDouble(out var value)
            ? value : 0;

    private static MediaDownloadResult ValidateMedia(string file, string metadata, MediaDownloadRequest request)
    {
        using var document = JsonDocument.Parse(metadata);
        var root = document.RootElement;
        var streams = root.GetProperty("streams").EnumerateArray().ToArray();
        var video = streams.FirstOrDefault(stream => stream.GetProperty("codec_type").GetString() == "video");
        var hasAudio = streams.Any(stream => stream.GetProperty("codec_type").GetString() == "audio");
        var format = root.GetProperty("format");
        if (request.Mode == MediaMode.Audio)
        {
            var audio = streams.FirstOrDefault(stream => stream.GetProperty("codec_type").GetString() == "audio");
            var expected = request.AudioFormat.ToString().ToLowerInvariant();
            var codec = audio.ValueKind == JsonValueKind.Object ? audio.GetProperty("codec_name").GetString() : null;
            var codecMatches = request.AudioFormat == AudioFormat.Wav ? codec?.StartsWith("pcm_", StringComparison.Ordinal) == true : codec == expected;
            if (!hasAudio || video.ValueKind == JsonValueKind.Object || !codecMatches ||
                format.GetProperty("format_name").GetString() != expected ||
                !double.TryParse(format.GetProperty("duration").GetString(), CultureInfo.InvariantCulture, out var audioDuration) ||
                !double.IsFinite(audioDuration) || audioDuration <= 0)
                throw new InvalidDataException("Audio format or content validation failed.");
            return new(file, 0, audioDuration) { Mode = MediaMode.Audio, AudioFormat = request.AudioFormat };
        }
        if (video.ValueKind != JsonValueKind.Object || !hasAudio || !video.TryGetProperty("height", out var height) ||
            !height.TryGetInt32(out var actualHeight) || actualHeight <= 0 || actualHeight > request.MaximumHeight ||
            !(format.GetProperty("format_name").GetString()?.Split(',').Contains("mp4") ?? false) ||
            !double.TryParse(format.GetProperty("duration").GetString(), CultureInfo.InvariantCulture, out var duration) ||
            !double.IsFinite(duration) || duration <= 0)
            throw new InvalidDataException("O arquivo não confirmou vídeo MP4 com áudio na resolução escolhida. / File did not confirm MP4 video with audio within selected resolution.");
        return new(file, actualHeight, duration);
    }
}
