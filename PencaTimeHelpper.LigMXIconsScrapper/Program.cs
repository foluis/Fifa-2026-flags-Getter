using Microsoft.Playwright;
using System.Text.RegularExpressions;

const string pageUrl = "https://mexico.as.com/resultados/futbol/mexico_clausura/equipos/";
const string imageSelector = ".a_bd_i";
const string outputFolder = "downloaded-images";

Directory.CreateDirectory(outputFolder);

using var playwright = await Playwright.CreateAsync();

await using var browser = await playwright.Chromium.LaunchAsync(new()
{
    Headless = true
});

var page = await browser.NewPageAsync();

await page.GotoAsync(pageUrl, new()
{
    WaitUntil = WaitUntilState.NetworkIdle,
    Timeout = 60_000
});

await page.WaitForSelectorAsync(imageSelector, new()
{
    Timeout = 30_000
});

var teamImages = await page.Locator(imageSelector).EvaluateAllAsync<TeamImage[]>(
"""
elements => {
    const results = [];
    const seen = new Set();

    for (const element of elements) {
        const img = element.tagName.toLowerCase() === "img"
            ? element
            : element.querySelector("img");

        if (!img) continue;

        const src =
            img.currentSrc ||
            img.src ||
            img.getAttribute("src") ||
            img.getAttribute("data-src") ||
            img.getAttribute("data-lazy-src");

        if (!src || seen.has(src)) continue;
        seen.add(src);

        // Walk up to the parent <a> and find the team name span
        const anchor = img.closest("a");
        const nameSpan = anchor ? anchor.querySelector(".a_tb_tn") : null;
        const name = nameSpan ? nameSpan.textContent.trim() : null;

        results.push({ url: src, name });
    }

    return results;
}
"""
);

Console.WriteLine($"Found {teamImages.Length} image(s).");

using var httpClient = new HttpClient();

httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
);

httpClient.DefaultRequestHeaders.Referrer = new Uri(pageUrl);

for (var index = 0; index < teamImages.Length; index++)
{
    var teamImage = teamImages[index];

    if (!Uri.TryCreate(teamImage.Url, UriKind.Absolute, out var imageUri))
    {
        imageUri = new Uri(new Uri(pageUrl), teamImage.Url);
    }

    try
    {
        using var response = await httpClient.GetAsync(imageUri);
        response.EnsureSuccessStatusCode();

        var extension = Path.GetExtension(imageUri.LocalPath);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".png";

        var fileName = BuildSafeFileName(teamImage.Name, extension, index + 1);
        var filePath = Path.Combine(outputFolder, fileName);

        await using var fileStream = File.Create(filePath);
        await response.Content.CopyToAsync(fileStream);

        Console.WriteLine($"Downloaded: {fileName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed: {imageUri}");
        Console.WriteLine(ex.Message);
    }
}

static string BuildSafeFileName(string? teamName, string extension, int index)
{
    var baseName = string.IsNullOrWhiteSpace(teamName)
        ? $"team-{index}"
        : teamName.Trim();

    baseName = Regex.Replace(baseName, @"[^\w\-]", "_");

    return $"{baseName}{extension}";
}

class TeamImage
{
    public string Url { get; set; } = string.Empty;
    public string? Name { get; set; }
}
