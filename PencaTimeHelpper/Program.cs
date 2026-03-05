using PencaTimeHelpper.Services;
using System.Globalization;

namespace PencaTimeHelpper;

internal class Program
{
    static readonly string[] DefaultMatchTimes =
    [
        "04:00", "05:00", "07:00", "09:00", "11:00", "13:00", "15:00", "17:00", "19:00", "20:00", "21:00"
    ];

    static async Task Main(string[] args)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FifaFlagDownloader/1.0 (Console App)");
        var parser = new WikipediaParser(httpClient);
        // Place flags/ folder at the project level (same level as Services/)
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var outputDir = Path.Combine(projectDir, "flags");
        var templateDir = Path.Combine(projectDir, "template");
        var matchTimesConfigPath = Path.Combine(projectDir, "match-times.config.txt");

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
                    if (await ShowCsvTemplateMenuAsync(templateDir, outputDir, matchTimesConfigPath))
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

    static async Task<bool> ShowCsvTemplateMenuAsync(string templateDir, string flagsDir, string matchTimesConfigPath)
    {
        while (true)
        {
            Console.WriteLine("Generate CSV files for PencaTime testing:");
            Console.WriteLine("  1. Stage Template.csv");
            Console.WriteLine("  2. Team Template.csv");
            Console.WriteLine("  3. Match Template.csv");
            Console.WriteLine("  4. Groups Template.csv");
            Console.WriteLine("  5. Back to main menu");
            Console.WriteLine("  6. Exit");
            Console.Write("\nOption: ");

            var choice = Console.ReadLine()?.Trim();
            Console.Clear();

            switch (choice)
            {
                case "1":
                    await GenerateStageTemplateCsvAsync(templateDir);
                    break;

                case "2":
                    await GenerateTeamTemplateCsvAsync(templateDir, flagsDir);
                    break;

                case "3":
                    await GenerateMatchTemplateCsvAsync(templateDir, matchTimesConfigPath);
                    break;

                case "4":
                    await GenerateGroupsTemplateCsvAsync(templateDir);
                    break;

                case "5":
                    Console.WriteLine("Returning to main menu...");
                    return false;

                case "6":
                    Console.WriteLine("Goodbye!");
                    return true;

                default:
                    Console.WriteLine("Invalid option. Please enter 1, 2, 3, 4, 5, or 6.");
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

    static async Task GenerateTeamTemplateCsvAsync(string templateDir, string flagsDir)
    {
        if (!Directory.Exists(flagsDir))
        {
            Console.WriteLine("No team flag images found.");
            Console.WriteLine("Please run main menu option 2 (Download team flags) first.");
            return;
        }

        var imageFiles = Directory.GetFiles(flagsDir)
            .Where(file => string.Equals(Path.GetExtension(file), ".svg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetExtension(file), ".png", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (imageFiles.Count == 0)
        {
            Console.WriteLine("No team flag images found.");
            Console.WriteLine("Please run main menu option 2 (Download team flags) first.");
            return;
        }

        var teamNames = imageFiles
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Directory.CreateDirectory(templateDir);

        var lines = new List<string> { "Name" };
        lines.AddRange(teamNames);

        var filePath = Path.Combine(templateDir, "Team Template.csv");
        await File.WriteAllLinesAsync(filePath, lines);

        Console.WriteLine("Team Template.csv generated successfully.");
        Console.WriteLine($"Saved to: {filePath}");
    }

    static async Task GenerateGroupsTemplateCsvAsync(string templateDir)
    {
        var (isNumeric, initialValue) = PromptForInitialGroup();
        var groupsCount = PromptForGroupsCount();

        Directory.CreateDirectory(templateDir);

        var lines = new List<string> { "Name" };

        for (var i = 0; i < groupsCount; i++)
        {
            var groupLabel = isNumeric
                ? (initialValue + i).ToString(CultureInfo.InvariantCulture)
                : NumberToLetterLabel(initialValue + i);

            lines.Add($"Group {groupLabel}");
        }

        var filePath = Path.Combine(templateDir, "Stage 1 - Groups Template.csv");
        await File.WriteAllLinesAsync(filePath, lines);

        Console.WriteLine("Stage 1 - Groups Template.csv generated successfully.");
        Console.WriteLine($"Saved to: {filePath}");
    }

    static async Task GenerateMatchTemplateCsvAsync(string templateDir, string matchTimesConfigPath)
    {
        while (true)
        {
            Console.WriteLine("Match Template source:");
            Console.WriteLine("  1. Random");
            Console.WriteLine("  2. From Web site");
            Console.Write("\nOption: ");

            var option = Console.ReadLine()?.Trim();
            Console.Clear();

            switch (option)
            {
                case "1":
                    await GenerateRandomMatchTemplateCsvAsync(templateDir, matchTimesConfigPath);
                    return;
                case "2":
                    Console.WriteLine("From Web site generation is not implemented yet.");
                    return;
                default:
                    Console.WriteLine("Invalid option. Please enter 1 or 2.\n");
                    break;
            }
        }
    }

    static async Task GenerateRandomMatchTemplateCsvAsync(string templateDir, string matchTimesConfigPath)
    {
        var stageTemplatePath = Path.Combine(templateDir, "Stage Template.csv");
        var groupsTemplatePath = Path.Combine(templateDir, "Stage 1 - Groups Template.csv");
        var teamTemplatePath = Path.Combine(templateDir, "Team Template.csv");

        if (!File.Exists(stageTemplatePath))
        {
            Console.WriteLine("Missing template/Stage Template.csv.");
            Console.WriteLine("Please generate it first from CSV menu option 1 (Stage Template.csv).");
            return;
        }

        if (!File.Exists(groupsTemplatePath))
        {
            Console.WriteLine("Missing template/Stage 1 - Groups Template.csv.");
            Console.WriteLine("Please generate it first from CSV menu option 4 (Groups Template.csv).");
            return;
        }

        if (!File.Exists(teamTemplatePath))
        {
            Console.WriteLine("Missing template/Team Template.csv.");
            Console.WriteLine("Please generate it first from CSV menu option 2 (Team Template.csv).");
            return;
        }

        var (groupsStartDate, groupsEndDate) = await ReadGroupsPhaseDateRangeAsync(stageTemplatePath);
        var groups = await ReadSingleColumnValuesAsync(groupsTemplatePath);
        var teams = await ReadSingleColumnValuesAsync(teamTemplatePath);

        if (groups.Count == 0)
        {
            Console.WriteLine("No groups found in Stage 1 - Groups Template.csv.");
            return;
        }

        var uniqueTeams = teams
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (uniqueTeams.Count < 2)
        {
            Console.WriteLine("Not enough teams found in Team Template.csv.");
            return;
        }

        var maxMatches = uniqueTeams.Count / 2;
        var matchesCount = PromptForMatchesCount(maxMatches);
        var matchTimes = await LoadMatchTimesAsync(matchTimesConfigPath);

        if (matchTimes.Count == 0)
        {
            Console.WriteLine("No valid match times found in configuration.");
            return;
        }

        var shuffledTeams = uniqueTeams.OrderBy(_ => Random.Shared.Next()).ToList();
        var lines = new List<string> { "HomeTeam,AwayTeam,Date,Time,Stage,Group" };

        for (var i = 0; i < matchesCount; i++)
        {
            var homeTeam = shuffledTeams[i * 2];
            var awayTeam = shuffledTeams[i * 2 + 1];
            var group = groups[Random.Shared.Next(groups.Count)];
            var date = GetRandomDate(groupsStartDate, groupsEndDate);
            var time = matchTimes[Random.Shared.Next(matchTimes.Count)];

            lines.Add($"{homeTeam},{awayTeam},{date:yyyy-MM-dd},{time},Groups Phase,{group}");
        }

        var outputPath = Path.Combine(templateDir, "Match Template.csv");
        await File.WriteAllLinesAsync(outputPath, lines);

        Console.WriteLine("Match Template.csv generated successfully.");
        Console.WriteLine($"Saved to: {outputPath}");
    }

    static async Task<(DateOnly startDate, DateOnly endDate)> ReadGroupsPhaseDateRangeAsync(string stageTemplatePath)
    {
        var lines = await File.ReadAllLinesAsync(stageTemplatePath);

        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
                continue;

            if (!parts[0].Equals("Groups Phase", StringComparison.OrdinalIgnoreCase))
                continue;

            if (DateOnly.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var startDate) &&
                DateOnly.TryParseExact(parts[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var endDate))
            {
                return (startDate, endDate);
            }
        }

        throw new InvalidOperationException("Groups Phase date range not found in Stage Template.csv.");
    }

    static async Task<List<string>> ReadSingleColumnValuesAsync(string filePath)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        return [.. lines.Skip(1)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))];
    }

    static int PromptForMatchesCount(int maxMatches)
    {
        while (true)
        {
            Console.Write($"How many matches should be created? (1-{maxMatches}): ");
            var input = Console.ReadLine()?.Trim();

            if (int.TryParse(input, out var matchesCount) && matchesCount >= 1 && matchesCount <= maxMatches)
                return matchesCount;

            Console.WriteLine($"Invalid value. Enter an integer between 1 and {maxMatches}.");
        }
    }

    static DateOnly GetRandomDate(DateOnly startDate, DateOnly endDate)
    {
        var range = endDate.DayNumber - startDate.DayNumber;
        return startDate.AddDays(Random.Shared.Next(range + 1));
    }

    static async Task<List<string>> LoadMatchTimesAsync(string configPath)
    {
        if (!File.Exists(configPath))
            await File.WriteAllLinesAsync(configPath, DefaultMatchTimes);

        var lines = await File.ReadAllLinesAsync(configPath);
        return [.. lines
            .Select(line => line.Trim())
            .Where(line => TimeOnly.TryParseExact(line, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))];
    }

    static (bool isNumeric, int initialValue) PromptForInitialGroup()
    {
        while (true)
        {
            Console.Write("Enter initial group (single letter or integer): ");
            var input = Console.ReadLine()?.Trim();

            if (int.TryParse(input, out var initialNumber) && initialNumber >= 1)
                return (true, initialNumber);

            if (!string.IsNullOrWhiteSpace(input) && input.Length == 1 && char.IsLetter(input[0]))
            {
                var initialLetterIndex = char.ToUpperInvariant(input[0]) - 'A' + 1;
                return (false, initialLetterIndex);
            }

            Console.WriteLine("Invalid input. Enter a single letter (A-Z) or an integer greater than or equal to 1.");
        }
    }

    static int PromptForGroupsCount()
    {
        while (true)
        {
            Console.Write("Enter how many groups to create (> 0): ");
            var input = Console.ReadLine()?.Trim();

            if (int.TryParse(input, out var groupsCount) && groupsCount > 0)
                return groupsCount;

            Console.WriteLine("Invalid value. Please enter an integer greater than 0.");
        }
    }

    static string NumberToLetterLabel(int value)
    {
        var label = string.Empty;
        var current = value;

        while (current > 0)
        {
            current--;
            label = (char)('A' + current % 26) + label;
            current /= 26;
        }

        return label;
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
