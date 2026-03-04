using PencaTimeHelpper.Services;
using System.Globalization;

namespace PencaTimeHelpper;

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
        var templateDir = Path.Combine(projectDir, "template");

        Console.WriteLine("=== FIFA 2026 World Cup - Team Flag Downloader ===\n");

        while (true)
        {
            Console.WriteLine("Select an option:");
            Console.WriteLine("  1. Validate page structure");
            Console.WriteLine("  2. Download team flags");
            Console.WriteLine("  3. Generate PencaTime test CSV files");
            Console.WriteLine("  4. Exit");
            Console.Write("\nOption: ");

            var choice = Console.ReadLine()?.Trim();
            Console.Clear();

            switch (choice)
            {
                case "1":
                    await ValidateStructureAsync(parser);
                    break;

                case "2":
                    await DownloadFlagsAsync(parser, httpClient, outputDir);
                    break;

                case "3":
                    if (await ShowCsvTemplateMenuAsync(templateDir))
                        return;
                    break;

                case "4":
                    Console.WriteLine("Goodbye!");
                    return;

                default:
                    Console.WriteLine("Invalid option. Please enter 1, 2, 3, or 4.");
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

    static async Task<bool> ShowCsvTemplateMenuAsync(string templateDir)
    {
        while (true)
        {
            Console.WriteLine("Generate CSV files for PencaTime testing:");
            Console.WriteLine("  1. Stage Template.csv");
            Console.WriteLine("  2. Team Template.csv");
            Console.WriteLine("  3. Match Template.csv");
            Console.WriteLine("  4. Back to main menu");
            Console.WriteLine("  5. Exit");
            Console.Write("\nOption: ");

            var choice = Console.ReadLine()?.Trim();
            Console.Clear();

            switch (choice)
            {
                case "1":
                    await GenerateStageTemplateCsvAsync(templateDir);
                    break;

                case "2":
                    Console.WriteLine("Team Template.csv generation is not implemented yet.");
                    break;

                case "3":
                    Console.WriteLine("Match Template.csv generation is not implemented yet.");
                    break;

                case "4":
                    Console.WriteLine("Returning to main menu...");
                    return false;

                case "5":
                    Console.WriteLine("Goodbye!");
                    return true;

                default:
                    Console.WriteLine("Invalid option. Please enter 1, 2, 3, 4, or 5.");
                    break;
            }

            Console.WriteLine();
        }
    }

    static async Task GenerateStageTemplateCsvAsync(string templateDir)
    {
        var baseDate = PromptForBaseDate();
        var firstStageDurationDays = PromptForFirstStageDurationDays();

        Directory.CreateDirectory(templateDir);

        var stageNames = new[]
        {
            "Groups Phase",
            "Round of 32",
            "Round of 16",
            "Quarter-finals",
            "Semi-finals",
            "Third-place match",
            "Final"
        };

        var lines = new List<string> { "Name,StartDate,EndDate" };

        var currentStartDate = baseDate;
        for (var i = 0; i < stageNames.Length; i++)
        {
            var durationDays = i == 0 ? firstStageDurationDays : 1;
            var currentEndDate = currentStartDate.AddDays(durationDays);
            lines.Add($"{stageNames[i]},{currentStartDate:yyyy-MM-dd},{currentEndDate:yyyy-MM-dd}");
            currentStartDate = currentEndDate.AddDays(1);
        }

        var filePath = Path.Combine(templateDir, "Stage Template.csv");
        await File.WriteAllLinesAsync(filePath, lines);

        Console.WriteLine("Stage Template.csv generated successfully.");
        Console.WriteLine($"Saved to: {filePath}");
    }

    static int PromptForFirstStageDurationDays()
    {
        while (true)
        {
            Console.Write("Enter first stage duration in days (> 0): ");
            var input = Console.ReadLine()?.Trim();

            if (int.TryParse(input, out var days) && days > 0)
                return days;

            Console.WriteLine("Invalid value. Please enter an integer greater than 0.");
        }
    }

    static DateOnly PromptForBaseDate()
    {
        while (true)
        {
            Console.Write("Enter base date (yyyy-MM-dd): ");
            var input = Console.ReadLine()?.Trim();

            if (DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var baseDate))
            {
                return baseDate;
            }

            Console.WriteLine("Invalid date format. Please use yyyy-MM-dd.");
        }
    }
}
