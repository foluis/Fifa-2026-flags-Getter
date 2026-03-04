using FromGoogleDefaultDashBoard.Services;

namespace FromGoogleDefaultDashBoard;

internal class Program
{
    static async Task Main(string[] args)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FifaFlagDownloader/1.0 (Console App)");
        var parser = new WikipediaParser(httpClient);
        // Place flags/ folder at the project level (same level as Services/)
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var outputDir = Path.Combine(projectDir, "flags");

        Console.WriteLine("=== FIFA 2026 World Cup - Team Flag Downloader ===\n");

        while (true)
        {
            Console.WriteLine("Select an option:");
            Console.WriteLine("  1. Validate page structure");
            Console.WriteLine("  2. Download team flags");
            Console.WriteLine("  3. Exit");
            Console.Write("\nOption: ");

            var choice = Console.ReadLine()?.Trim();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    await ValidateStructureAsync(parser);
                    break;

                case "2":
                    await DownloadFlagsAsync(parser, httpClient, outputDir);
                    break;

                case "3":
                    Console.WriteLine("Goodbye!");
                    return;

                default:
                    Console.WriteLine("Invalid option. Please enter 1, 2, or 3.");
                    break;
            }

            Console.WriteLine();
        }
    }

    static async Task ValidateStructureAsync(WikipediaParser parser)
    {
        Console.WriteLine("Validating Wikipedia page structure...\n");

        var result = await parser.ValidateStructureAsync();

        Console.WriteLine(result.IsValid
            ? $"  \u2713 {result.Message}"
            : $"  \u2717 {result.Message}");

        if (result.IsValid)
            Console.WriteLine("  Ready to download images.");
    }

    static async Task DownloadFlagsAsync(WikipediaParser parser, HttpClient httpClient, string outputDir)
    {
        Console.WriteLine("Fetching team list from Wikipedia...\n");

        var teams = await parser.ParseTeamFlagsAsync();

        if (teams.Count == 0)
        {
            Console.WriteLine("No teams found. Run option 1 to validate the page structure first.");
            return;
        }

        Console.WriteLine($"Found {teams.Count} teams.\n");

        var downloader = new FlagDownloader(httpClient, outputDir);
        await downloader.DownloadAllAsync(teams);
    }
}
