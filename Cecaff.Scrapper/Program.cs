using System.Text.RegularExpressions;
using System.Web;

var pageUrl = "https://cecaff.com/calendario-de-partidos/";
var downloadFolder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Roles");
Directory.CreateDirectory(downloadFolder);

var allowedExtensions = new[] { ".pdf", ".docx", ".xlsx", ".zip" };

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
    "Mozilla/5.0 (compatible; PublicDocumentDownloader/1.0)");

var html = await httpClient.GetStringAsync(pageUrl);

// Strategy 1: plain href/src/data attributes
var plainMatches = Regex.Matches(
    html,
    @"(?:href|src)\s*=\s*[""'](?<url>[^""']+)[""']",
    RegexOptions.IgnoreCase);

var pageBaseUri = new Uri(pageUrl);

var plainUrls = plainMatches
    .Select(m => m.Groups["url"].Value)
    .Where(url => !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(pageBaseUri, url, out _))
    .Select(url => new Uri(pageBaseUri, url))
    .Where(uri => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
    .Where(uri => allowedExtensions.Any(ext =>
        uri.AbsolutePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

// Strategy 2: Elfsight PDF embed plugin — PDFs are URL-encoded JSON in data-* attributes
// e.g. data-props="%7B%22files%22%3A%5B%7B%22link%22%3A%22https%3A%2F%2F...pdf%22..."
var elfsightMatches = Regex.Matches(
    html,
    @"data-[a-z\-]+=\s*""(?<encoded>[^""]+)""",
    RegexOptions.IgnoreCase);

var elfsightUrls = elfsightMatches
    .Select(m => HttpUtility.UrlDecode(m.Groups["encoded"].Value))
    .SelectMany(decoded => Regex.Matches(decoded, @"""link""\s*:\s*""(?<url>[^""]+)""")
        .Select(m => m.Groups["url"].Value.Replace(@"\/", "/")))
    .Where(url => !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out _))
    .Select(url => new Uri(url))
    .Where(uri => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
    .Where(uri => allowedExtensions.Any(ext =>
        uri.AbsolutePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

var documentUrls = plainUrls
    .Concat(elfsightUrls)
    .DistinctBy(uri => uri.ToString())
    .ToList();

Console.WriteLine($"Found {documentUrls.Count} document(s) to download.");

var downloadCount = 0;

foreach (var documentUri in documentUrls)
{
    var fileName = Path.GetFileName(documentUri.LocalPath);
    var outputPath = Path.Combine(downloadFolder, fileName);

    Console.WriteLine($"Downloading: {documentUri}");

    var bytes = await httpClient.GetByteArrayAsync(documentUri);
    await File.WriteAllBytesAsync(outputPath, bytes);
    downloadCount++;
}

Console.WriteLine($"\nDone. {downloadCount} file(s) downloaded to: {downloadFolder}");

