using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace FromGoogleDefaultDashBoard.Services;

/// <summary>
/// Parses the 2026 FIFA World Cup Wikipedia page to extract team names and flag image URLs.
/// </summary>
internal sealed partial class WikipediaParser
{
    const string PageUrl = "https://en.wikipedia.org/wiki/2026_FIFA_World_Cup";

    /// <summary>
    /// Represents a qualified team with its name and Wikimedia flag thumbnail URL.
    /// </summary>
    /// <param name="Name">Display name of the team (e.g. "Argentina").</param>
    /// <param name="ThumbnailUrl">Wikimedia thumbnail URL for the team's flag.</param>
    internal record TeamFlag(string Name, string ThumbnailUrl);

    /// <summary>
    /// Result of a page structure validation.
    /// </summary>
    /// <param name="IsValid">Whether the expected structure was found.</param>
    /// <param name="TeamsFound">Number of team flag entries detected.</param>
    /// <param name="ExpectedTeams">Number of teams expected (confirmed qualified).</param>
    /// <param name="Message">Human-readable summary.</param>
    internal record ValidationResult(bool IsValid, int TeamsFound, int ExpectedTeams, string Message);

    readonly HttpClient httpClient;

    internal WikipediaParser(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
    }

    /// <summary>
    /// Fetches the Wikipedia page and extracts team flags from the qualification/draw tables.
    /// </summary>
    internal async Task<List<TeamFlag>> ParseTeamFlagsAsync()
    {
        var html = await FetchPageHtmlAsync();
        return ExtractTeamFlags(html);
    }

    /// <summary>
    /// Validates that the Wikipedia page still has the expected structure for parsing.
    /// </summary>
    internal async Task<ValidationResult> ValidateStructureAsync()
    {
        const int expectedTeams = 42;

        try
        {
            var html = await FetchPageHtmlAsync();
            var teams = ExtractTeamFlags(html);

            if (teams.Count == 0)
            {
                return new ValidationResult(false, 0, expectedTeams,
                    "No team flags found. The page structure may have changed.");
            }

            if (teams.Count < 30)
            {
                return new ValidationResult(false, teams.Count, expectedTeams,
                    $"Only {teams.Count} teams found (expected ~{expectedTeams}). Structure may have changed.");
            }

            return new ValidationResult(true, teams.Count, expectedTeams,
                $"Valid structure! Found {teams.Count}/{expectedTeams} confirmed team flags.");
        }
        catch (HttpRequestException ex)
        {
            return new ValidationResult(false, 0, expectedTeams,
                $"Failed to fetch page: {ex.Message}");
        }
    }

    async Task<string> FetchPageHtmlAsync()
    {
        var response = await httpClient.GetAsync(PageUrl);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Parses the HTML and extracts unique team names with their flag URLs.
    /// Wikipedia structure: span[nowrap] > span.flagicon > span > span > img + a[football_team]
    /// </summary>
    static List<TeamFlag> ExtractTeamFlags(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var results = new Dictionary<string, TeamFlag>(StringComparer.OrdinalIgnoreCase);

        // Wikipedia wraps each flag+team in: <span class="flagicon">...<img>...</span>
        var flagIcons = doc.DocumentNode.SelectNodes("//span[contains(@class, 'flagicon')]");
        if (flagIcons is null)
        {
            Console.WriteLine("  [debug] No flagicon spans found in page.");
            return [];
        }

        Console.WriteLine($"  [debug] Found {flagIcons.Count} flagicon spans.");

        foreach (var flagSpan in flagIcons)
        {
            // Get the flag image inside the flagicon span
            var img = flagSpan.SelectSingleNode(".//img[contains(@src, 'Flag_of_')]");
            if (img is null)
                continue;

            var src = img.GetAttributeValue("src", "");
            if (!FlagThumbnailPattern().IsMatch(src))
                continue;

            // The team <a> is a sibling of the flagicon span, inside the parent nowrap span
            var teamName = ResolveTeamName(flagSpan);
            if (string.IsNullOrEmpty(teamName))
                continue;

            // Skip placeholder entries (TBD paths)
            if (teamName.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
                teamName.Contains("winner", StringComparison.OrdinalIgnoreCase) ||
                teamName.Contains("TBD", StringComparison.OrdinalIgnoreCase))
                continue;

            var flagUrl = src.StartsWith("//") ? $"https:{src}" : src;
            results.TryAdd(teamName, new TeamFlag(teamName, flagUrl));
        }

        Console.WriteLine($"  [debug] Extracted {results.Count} unique teams.");
        return [.. results.Values.OrderBy(t => t.Name)];
    }

    /// <summary>
    /// Resolves the team name by looking for an anchor near the flagicon span.
    /// Only accepts anchors linking to national football/soccer team pages.
    /// </summary>
    static string? ResolveTeamName(HtmlNode flagSpan)
    {
        // Strategy 1: look for <a> sibling right after the flagicon span
        for (var sibling = flagSpan.NextSibling; sibling is not null; sibling = sibling.NextSibling)
        {
            if (sibling.Name == "a" && IsNationalTeamAnchor(sibling))
                return CleanTeamName(sibling.InnerText.Trim());

            // Stop if we hit another flagicon or block element
            if (sibling.Name == "span" && sibling.GetAttributeValue("class", "").Contains("flagicon"))
                break;
            if (sibling.Name is "br" or "div" or "p" or "table" or "li")
                break;
        }

        // Strategy 2: walk up to nearest container and search for a football_team anchor
        for (var node = flagSpan.ParentNode; node is not null; node = node.ParentNode)
        {
            if (node.Name is "li" or "td" or "span")
            {
                var anchor = node.SelectSingleNode(
                    ".//a[contains(@href, 'football_team') or contains(@href, 'soccer_team')]");
                if (anchor is not null)
                    return CleanTeamName(anchor.InnerText.Trim());
            }

            // Don't go above list/table boundaries
            if (node.Name is "ul" or "ol" or "table" or "div")
                break;
        }

        return null;
    }

    static bool IsNationalTeamAnchor(HtmlNode anchor)
    {
        var href = anchor.GetAttributeValue("href", "");
        return !string.IsNullOrWhiteSpace(anchor.InnerText) &&
               (href.Contains("football_team") || href.Contains("soccer_team"));
    }

    static string CleanTeamName(string raw)
    {
        // Remove annotations like "(co-host)" and extra whitespace
        var cleaned = AnnotationPattern().Replace(raw, "").Trim();
        return cleaned;
    }

    /// <summary>
    /// Replaces the width in a Wikimedia thumbnail URL to resize the image.
    /// </summary>
    internal static string ResizeThumbnailUrl(string thumbnailUrl, int targetWidth)
    {
        return WidthPattern().Replace(thumbnailUrl, $"{targetWidth}px-");
    }

    /// <summary>
    /// Converts a Wikimedia thumbnail URL to the raw SVG source URL.
    /// Removes /thumb/ and the trailing size segment (e.g. /40px-Flag_of_X.svg.png).
    /// </summary>
    internal static string ToSvgUrl(string thumbnailUrl)
    {
        // Remove /thumb/ from path
        var svgUrl = thumbnailUrl.Replace("/thumb/", "/");

        // Remove the last segment (e.g. /40px-Flag_of_Argentina.svg.png)
        var lastSlash = svgUrl.LastIndexOf('/');
        if (lastSlash > 0)
            svgUrl = svgUrl[..lastSlash];

        return svgUrl;
    }

    [GeneratedRegex(@"\d+px-")]
    private static partial Regex WidthPattern();

    [GeneratedRegex(@"\(.*?\)")]
    private static partial Regex AnnotationPattern();

    [GeneratedRegex(@"upload\.wikimedia\.org/wikipedia/(commons|en)/thumb/.+/Flag_of_.+\.svg/\d+px-")]
    private static partial Regex FlagThumbnailPattern();
}
