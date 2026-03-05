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

            if (!TryReadInput("\nOption: ", out var choice))
            {
                Console.Clear();
                Console.WriteLine("Goodbye!");
                return;
            }

            choice = choice.Trim();
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

            if (!TryReadInput("\nOption: ", out var choice))
            {
                Console.Clear();
                Console.WriteLine("Returning to main menu...");
                return false;
            }

            choice = choice.Trim();
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
        if (!TryPromptForBaseDate(out var baseDate))
        {
            Console.WriteLine("Returning to previous menu...");
            return;
        }

        if (!TryPromptForFirstStageDurationDays(out var firstStageDurationDays))
        {
            Console.WriteLine("Returning to previous menu...");
            return;
        }

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
        if (!TryPromptForInitialGroup(out var isNumeric, out var initialValue))
        {
            Console.WriteLine("Returning to previous menu...");
            return;
        }

        if (!TryPromptForGroupsCount(out var groupsCount))
        {
            Console.WriteLine("Returning to previous menu...");
            return;
        }

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

            if (!TryReadInput("\nOption: ", out var option))
            {
                Console.Clear();
                Console.WriteLine("Returning to previous menu...");
                return;
            }

            option = option.Trim();
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
        if (!TryPromptForMatchesCount(maxMatches, out var matchesCount))
        {
            Console.WriteLine("Returning to previous menu...");
            return;
        }

        if (!TryPromptForFirstMatchDate(groupsStartDate, groupsEndDate, out var firstMatchDate))
        {
            Console.WriteLine("Returning to previous menu...");
            return;
        }

        var matchTimes = await LoadMatchTimesAsync(matchTimesConfigPath);

        if (matchTimes.Count == 0)
        {
            Console.WriteLine("No valid match times found in configuration.");
            return;
        }

        var shuffledTeams = uniqueTeams.OrderBy(_ => Random.Shared.Next()).ToList();
        var lines = new List<string> { "HomeTeam,AwayTeam,Date,Time,Stage,Group" };
        var scheduledDate = firstMatchDate;

        for (var i = 0; i < matchesCount; i++)
        {
            var homeTeam = shuffledTeams[i * 2];
            var awayTeam = shuffledTeams[i * 2 + 1];
            var group = groups[Random.Shared.Next(groups.Count)];
            var time = matchTimes[Random.Shared.Next(matchTimes.Count)];

            lines.Add($"{homeTeam},{awayTeam},{scheduledDate:yyyy-MM-dd},{time},Groups Phase,{group}");

            scheduledDate = scheduledDate.AddDays(1);
            if (scheduledDate > groupsEndDate)
                scheduledDate = firstMatchDate;
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

    static bool TryPromptForMatchesCount(int maxMatches, out int matchesCount)
    {
        matchesCount = 0;

        while (true)
        {
            if (!TryReadInput($"How many matches should be created? (1-{maxMatches}): ", out var input))
                return false;

            input = input.Trim();

            if (int.TryParse(input, out var parsedMatchesCount) && parsedMatchesCount >= 1 && parsedMatchesCount <= maxMatches)
            {
                matchesCount = parsedMatchesCount;
                return true;
            }

            Console.WriteLine($"Invalid value. Enter an integer between 1 and {maxMatches}.");
        }
    }

    static bool TryPromptForFirstMatchDate(DateOnly groupsStartDate, DateOnly groupsEndDate, out DateOnly firstMatchDate)
    {
        firstMatchDate = default;

        while (true)
        {
            if (!TryReadInput(
                    $"Enter first match date (yyyy-MM-dd) between {groupsStartDate:yyyy-MM-dd} and {groupsEndDate:yyyy-MM-dd}: ", out var input))
                return false;

            input = input.Trim();

            if (!DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var parsedFirstMatchDate))
            {
                Console.WriteLine("Invalid date format. Please use yyyy-MM-dd.");
                continue;
            }

            if (parsedFirstMatchDate < groupsStartDate || parsedFirstMatchDate > groupsEndDate)
            {
                Console.WriteLine($"Invalid date. Valid range is {groupsStartDate:yyyy-MM-dd} to {groupsEndDate:yyyy-MM-dd}.");
                continue;
            }

            firstMatchDate = parsedFirstMatchDate;
            return true;
        }
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

    static bool TryPromptForInitialGroup(out bool isNumeric, out int initialValue)
    {
        isNumeric = false;
        initialValue = 0;

        while (true)
        {
            if (!TryReadInput("Enter initial group (single letter or integer): ", out var input))
                return false;

            input = input.Trim();

            if (int.TryParse(input, out var initialNumber) && initialNumber >= 1)
            {
                isNumeric = true;
                initialValue = initialNumber;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(input) && input.Length == 1 && char.IsLetter(input[0]))
            {
                var initialLetterIndex = char.ToUpperInvariant(input[0]) - 'A' + 1;
                isNumeric = false;
                initialValue = initialLetterIndex;
                return true;
            }

            Console.WriteLine("Invalid input. Enter a single letter (A-Z) or an integer greater than or equal to 1.");
        }
    }

    static bool TryPromptForGroupsCount(out int groupsCount)
    {
        groupsCount = 0;

        while (true)
        {
            if (!TryReadInput("Enter how many groups to create (> 0): ", out var input))
                return false;

            input = input.Trim();

            if (int.TryParse(input, out var parsedGroupsCount) && parsedGroupsCount > 0)
            {
                groupsCount = parsedGroupsCount;
                return true;
            }

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

    static bool TryPromptForFirstStageDurationDays(out int days)
    {
        days = 0;

        while (true)
        {
            if (!TryReadInput("Enter first stage duration in days (> 0): ", out var input))
                return false;

            input = input.Trim();

            if (int.TryParse(input, out var parsedDays) && parsedDays > 0)
            {
                days = parsedDays;
                return true;
            }

            Console.WriteLine("Invalid value. Please enter an integer greater than 0.");
        }
    }

    static bool TryPromptForBaseDate(out DateOnly baseDate)
    {
        baseDate = default;

        while (true)
        {
            if (!TryReadInput("Enter base date (yyyy-MM-dd): ", out var input))
                return false;

            input = input.Trim();

            if (DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsedBaseDate))
            {
                baseDate = parsedBaseDate;
                return true;
            }

            Console.WriteLine("Invalid date format. Please use yyyy-MM-dd.");
        }
    }

    static bool TryReadInput(string prompt, out string value)
    {
        value = string.Empty;
        Console.Write(prompt);

        var buffer = new List<char>();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine();
                return false;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                value = new string(buffer.ToArray());
                return true;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Count == 0)
                    continue;

                buffer.RemoveAt(buffer.Count - 1);
                Console.Write("\b \b");
                continue;
            }

            if (char.IsControl(key.KeyChar))
                continue;

            buffer.Add(key.KeyChar);
            Console.Write(key.KeyChar);
        }
    }
}
