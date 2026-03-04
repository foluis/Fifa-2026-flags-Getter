# FIFA 2026 Flags Getter

A .NET 10 console application that automatically scrapes the [2026 FIFA World Cup Wikipedia page](https://en.wikipedia.org/wiki/2026_FIFA_World_Cup) and downloads the national flag image for every qualified team.

## Features

- **Wikipedia scraping** – Parses the live Wikipedia article using [HtmlAgilityPack](https://html-agility-pack.net/) to discover all 42 confirmed teams and their flag thumbnail URLs.
- **Structure validation** – Checks that the Wikipedia page still has the expected layout before downloading, so you know immediately if the page structure has changed.
- **SVG & PNG support** – Choose between lossless SVG vectors or raster PNG images.
- **Custom PNG sizes** – When downloading PNGs you can pick from Wikimedia's standard widths (60 px – 1280 px) and preview the first flag before committing to a full download.
- **Rate-limit handling** – Automatically retries on HTTP 429 (Too Many Requests) with incremental back-off. Wikimedia rate-limits all requests per-IP, so downloads take approximately 2 minutes regardless of format.
- **Progress reporting** – Displays per-team status (✓ / ✗), start/end timestamps, and elapsed time.

## Prerequisites

| Requirement | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0 or later |

No additional API keys or accounts are needed — the app reads publicly available Wikipedia content.

## Getting started

```bash
# Clone the repository
git clone https://github.com/foluis/Fifa-2026-flags-Getter.git
cd Fifa-2026-flags-Getter

# Restore packages and build
dotnet build

# Run the application
dotnet run --project FromGoogleDefaultDashBoard/FromGoogleDefaultDashBoard.csproj
```

## Usage

When the application starts you will see an interactive menu:

```
=== FIFA 2026 World Cup - Team Flag Downloader ===

Select an option:
  1. Validate page structure
  2. Download team flags
  3. Exit
```

### Option 1 – Validate page structure

Fetches the Wikipedia page and reports whether the expected HTML structure is still intact and how many teams were detected (expected: 42). Run this first to make sure the parser is compatible with the current page layout.

### Option 2 – Download team flags

1. **Choose a format** – SVG (vector) or PNG (raster).
2. If you chose **PNG**, select the download size:
   - *Original size* – uses the pre-cached thumbnail URL from Wikipedia.
   - *Custom size* – pick from standard Wikimedia widths (60, 120, 250, 330, 500, 960, or 1280 px). A preview of the first flag is shown before the full download begins.
3. Flags are saved to a `flags/` folder inside the project directory (`FromGoogleDefaultDashBoard/flags/`), named after each team (e.g. `Argentina.svg`, `Brazil.png`).

> **Note:** Wikimedia rate-limits all requests per-IP (~5 burst, then throttled). Both SVG and PNG downloads take approximately 2 minutes for all 42 teams. The retry logic handles this automatically.

### Option 3 – Exit

Closes the application.

## Project structure

```
Fifa-2026-flags-Getter/
├── FromGoogleDefaultDashBoard/
│   ├── Program.cs                      # Entry point & interactive menu
│   ├── Services/
│   │   ├── WikipediaParser.cs          # Scrapes Wikipedia, extracts team names & flag URLs
│   │   └── FlagDownloader.cs           # Downloads flag images with retry logic
│   ├── flags/                          # Downloaded flag images (created at runtime)
│   └── FromGoogleDefaultDashBoard.csproj
├── Fifa-2026-flags-Getter.slnx         # Solution file
├── .gitattributes
├── .gitignore
├── LICENSE.txt                         # Apache 2.0
└── README.md
```

## License

This project is licensed under the [Apache License 2.0](LICENSE.txt).
