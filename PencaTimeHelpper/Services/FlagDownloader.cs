using System.Diagnostics;

namespace PencaTimeHelpper.Services;

/// <summary>
/// Downloads team flag images from Wikimedia at a user-specified width.
/// </summary>
internal sealed class FlagDownloader
{
    // Wikimedia standard thumbnail sizes (non-standard sizes return HTTP 429)
    static readonly int[] SuggestedWidths = [60, 120, 250, 330, 500, 960, 1280];

    readonly HttpClient httpClient;
    readonly string outputDirectory;

    internal FlagDownloader(HttpClient httpClient, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        this.httpClient = httpClient;
        this.outputDirectory = outputDirectory;
    }

    /// <summary>
    /// Orchestrates the full download flow: size choice, optional preview, timed download.
    /// </summary>
    internal async Task DownloadAllAsync(List<WikipediaParser.TeamFlag> teams)
    {
        if (teams.Count == 0)
        {
            Console.WriteLine("No teams to download.");
            return;
        }

        Directory.CreateDirectory(outputDirectory);

        // Ask user: SVG or PNG?
        var useSvg = PromptForFormat();

        if (useSvg)
        {
            await DownloadSvgFlagsAsync(teams);
            return;
        }

        // PNG flow: ask for size
        var useOriginalSize = PromptForSizeMode();
        int? targetWidth = null;

        if (!useOriginalSize)
        {
            targetWidth = PromptForCustomSize();

            // Preview first image at chosen size
            var previewTeam = teams[0];
            Console.WriteLine($"\nPreviewing first flag: {previewTeam.Name} at {targetWidth}px width...");

            var previewInfo = await DownloadPreviewAsync(previewTeam, targetWidth.Value);
            if (previewInfo is null)
            {
                Console.WriteLine("Failed to download preview. Aborting.");
                return;
            }

            Console.WriteLine($"  Image: {previewTeam.Name}.png");
            Console.WriteLine($"  Size:  {previewInfo.Value.width}x{previewInfo.Value.height} pixels");
            Console.WriteLine($"  File:  {previewInfo.Value.fileSize:N0} bytes");
        }

        // Download all PNG flags with timing
        var sizeLabel = useOriginalSize ? "original" : $"{targetWidth}px";
        await DownloadWithTimingAsync(teams, sizeLabel, team =>
        {
            var url = useOriginalSize
                ? team.ThumbnailUrl
                : WikipediaParser.ResizeThumbnailUrl(team.ThumbnailUrl, targetWidth!.Value);
            return (url, $"{team.Name}.png");
        }, useOriginalSize ? 3000 : 3500);
    }

    async Task DownloadSvgFlagsAsync(List<WikipediaParser.TeamFlag> teams)
    {
        await DownloadWithTimingAsync(teams, "SVG (vector)", team =>
        {
            var svgUrl = WikipediaParser.ToSvgUrl(team.ThumbnailUrl);
            return (svgUrl, $"{team.Name}.svg");
        }, 3000);
    }

    async Task DownloadWithTimingAsync(
        List<WikipediaParser.TeamFlag> teams,
        string sizeLabel,
        Func<WikipediaParser.TeamFlag, (string url, string fileName)> urlResolver,
        int delayMs)
    {
        Console.WriteLine($"\nDownloading {teams.Count} flags ({sizeLabel})...\n");

        var startTime = DateTime.Now;
        var stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"  Started:  {startTime:hh:mm:ss tt}");

        var successCount = 0;
        var failCount = 0;

        foreach (var team in teams)
        {
            var (downloadUrl, fileName) = urlResolver(team);
            var filePath = Path.Combine(outputDirectory, fileName);

            var downloaded = await DownloadWithRetryAsync(downloadUrl, filePath, team.Name);
            if (downloaded)
                successCount++;
            else
                failCount++;

            await Task.Delay(delayMs);
        }

        stopwatch.Stop();
        var endTime = DateTime.Now;

        Console.WriteLine($"\n  Started:  {startTime:hh:mm:ss tt}");
        Console.WriteLine($"  Finished: {endTime:hh:mm:ss tt}");
        Console.WriteLine($"  Elapsed:  {stopwatch.Elapsed:mm\\:ss\\.ff}");
        Console.WriteLine($"\n  Result: {successCount} downloaded, {failCount} failed.");
        Console.WriteLine($"  Saved to: {Path.GetFullPath(outputDirectory)}");
    }

    static bool PromptForFormat()
    {
        Console.WriteLine("Download format:");
        Console.WriteLine("  1. SVG (vector, fastest — no rate limiting)");
        Console.WriteLine("  2. PNG (raster, choose size)");
        Console.Write("\nOption: ");

        var input = Console.ReadLine()?.Trim();
        return input != "2";
    }

    static bool PromptForSizeMode()
    {
        Console.WriteLine("Download size:");
        Console.WriteLine("  1. Original size (fastest — pre-cached, ~15 seconds)");
        Console.WriteLine("  2. Custom size (choose from Wikimedia standard sizes)");
        Console.Write("\nOption: ");

        var input = Console.ReadLine()?.Trim();
        return input != "2";
    }

    static int PromptForCustomSize()
    {
        Console.WriteLine("\nSelect a size:");

        for (var i = 0; i < SuggestedWidths.Length; i++)
        {
            Console.WriteLine($"  {i + 1}. {SuggestedWidths[i]}px");
        }

        Console.Write($"\nEnter number (1-{SuggestedWidths.Length}): ");
        var input = Console.ReadLine()?.Trim();

        if (int.TryParse(input, out var choice) && choice >= 1 && choice <= SuggestedWidths.Length)
            return SuggestedWidths[choice - 1];

        Console.WriteLine("Invalid selection. Using 330px.");
        return 330;
    }

    async Task<bool> DownloadWithRetryAsync(string url, string filePath, string teamName, int maxRetries = 3)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var retryDelay = attempt * 3000;
                    Console.WriteLine($"  ~ {teamName}: rate limited, retrying in {retryDelay / 1000}s... ({attempt}/{maxRetries})");
                    await Task.Delay(retryDelay);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(filePath, bytes);
                Console.WriteLine($"  \u2713 {teamName}");
                return true;
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                var retryDelay = attempt * 3000;
                Console.WriteLine($"  ~ {teamName}: failed, retrying in {retryDelay / 1000}s... ({attempt}/{maxRetries})");
                await Task.Delay(retryDelay);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  \u2717 {teamName}: {ex.Message}");
                return false;
            }
        }

        Console.WriteLine($"  \u2717 {teamName}: failed after {maxRetries} retries.");
        return false;
    }

    async Task<(int width, int height, long fileSize)?> DownloadPreviewAsync(
        WikipediaParser.TeamFlag team, int width)
    {
        var resizedUrl = WikipediaParser.ResizeThumbnailUrl(team.ThumbnailUrl, width);
        var filePath = Path.Combine(outputDirectory, $"{team.Name}.png");

        try
        {
            var bytes = await httpClient.GetByteArrayAsync(resizedUrl);
            await File.WriteAllBytesAsync(filePath, bytes);

            var dimensions = ReadPngDimensions(bytes);
            return (dimensions.width, dimensions.height, bytes.LongLength);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Preview error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Reads width and height from PNG file header bytes (IHDR chunk).
    /// </summary>
    static (int width, int height) ReadPngDimensions(byte[] pngBytes)
    {
        // PNG IHDR: width at offset 16 (4 bytes big-endian), height at offset 20
        if (pngBytes.Length < 24)
            return (0, 0);

        var width = (pngBytes[16] << 24) | (pngBytes[17] << 16) | (pngBytes[18] << 8) | pngBytes[19];
        var height = (pngBytes[20] << 24) | (pngBytes[21] << 16) | (pngBytes[22] << 8) | pngBytes[23];

        return (width, height);
    }
}
