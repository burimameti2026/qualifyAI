using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace QualifyAI.Api.Importing;

internal static class ProspectDatasetReader
{
    private const int MaximumRows = 10_000;
    private static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["companyName"] = ["companyname", "company", "account", "accountname", "organization", "organisation", "businessname"],
        ["domain"] = ["domain", "website", "websitedomain", "websiteurl", "url", "companywebsite"],
        ["contactName"] = ["contactname", "contact", "fullname", "decisionmaker", "buyername"],
        ["email"] = ["email", "emailaddress", "workemail", "businessemail"],
        ["jobTitle"] = ["jobtitle", "title", "role", "position", "suggestedbuyer", "buyerrole"],
        ["industry"] = ["industry", "segment", "sector", "vertical"],
        ["country"] = ["country", "market", "location", "hqcountry"],
        ["source"] = ["source", "datasource", "sourceurl", "provider"],
        ["fitScore"] = ["fitscore", "fit", "accountscore", "score"],
        ["intentScore"] = ["intentscore", "intent", "buyingintent", "signalscore"]
    };

    internal static async Task<ProspectDatasetPreview> ReadAsync(IFormFile file, string? requestedSheet, int? requestedHeaderRow, CancellationToken ct)
    {
        if (file.Length is <= 0 or > 15_000_000) throw new InvalidOperationException("Choose a non-empty CSV or XLSX file smaller than 15 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        IReadOnlyList<DatasetSheet> sheets = extension switch
        {
            ".csv" => [await ReadCsvAsync(file, ct)],
            ".xlsx" => ReadXlsx(file),
            _ => throw new InvalidOperationException("Supported file types are CSV and XLSX.")
        };
        if (sheets.Count == 0) throw new InvalidOperationException("No readable worksheet was found.");

        var candidates = sheets.Select(sheet => Analyze(sheet, requestedHeaderRow)).ToList();
        var selected = string.IsNullOrWhiteSpace(requestedSheet)
            ? candidates.OrderByDescending(x => x.RecognitionScore).ThenByDescending(x => x.DataRows.Count).First()
            : candidates.FirstOrDefault(x => string.Equals(x.Name, requestedSheet, StringComparison.OrdinalIgnoreCase))
              ?? throw new InvalidOperationException("The selected worksheet was not found.");
        if (selected.Headers.Count == 0) throw new InvalidOperationException("A header row could not be detected.");

        return new ProspectDatasetPreview(
            file.FileName,
            extension.TrimStart('.'),
            selected.Name,
            sheets.Select(x => x.Name).ToArray(),
            selected.HeaderRow,
            selected.Headers,
            selected.SuggestedMappings,
            selected.DataRows.Count,
            selected.DataRows.Take(8).ToArray(),
            selected.DataRows);
    }

    private static async Task<DatasetSheet> ReadCsvAsync(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var text = await reader.ReadToEndAsync(ct);
        return new DatasetSheet("CSV data", ParseDelimited(text));
    }

    private static IReadOnlyList<DatasetSheet> ReadXlsx(IFormFile file)
    {
        using var archive = new ZipArchive(file.OpenReadStream(), ZipArchiveMode.Read);
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
        var targets = relationships.Root!.Elements(packageRel + "Relationship").ToDictionary(x => (string)x.Attribute("Id")!, x => (string)x.Attribute("Target")!);
        var shared = ReadSharedStrings(archive, main);
        var result = new List<DatasetSheet>();
        foreach (var sheet in workbook.Descendants(main + "sheet"))
        {
            var name = (string?)sheet.Attribute("name") ?? "Worksheet";
            var relationshipId = (string?)sheet.Attribute(rel + "id");
            if (relationshipId is null || !targets.TryGetValue(relationshipId, out var target)) continue;
            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target.TrimStart('/');
            result.Add(new DatasetSheet(name, ReadWorksheet(archive, path.Replace("xl/../", string.Empty), main, shared)));
        }
        return result;
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidOperationException("The XLSX package is incomplete.");
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static string[] ReadSharedStrings(ZipArchive archive, XNamespace main)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];
        using var stream = entry.Open();
        return XDocument.Load(stream).Descendants(main + "si")
            .Select(item => string.Concat(item.Descendants(main + "t").Select(x => x.Value))).ToArray();
    }

    private static List<string[]> ReadWorksheet(ZipArchive archive, string path, XNamespace main, string[] shared)
    {
        var document = LoadXml(archive, path);
        var rows = new List<string[]>();
        foreach (var row in document.Descendants(main + "row").Take(MaximumRows + 30))
        {
            var values = new SortedDictionary<int, string>();
            foreach (var cell in row.Elements(main + "c"))
            {
                var reference = (string?)cell.Attribute("r") ?? string.Empty;
                var column = ColumnIndex(reference);
                var type = (string?)cell.Attribute("t") ?? string.Empty;
                var raw = type == "inlineStr" ? string.Concat(cell.Descendants(main + "t").Select(x => x.Value)) : cell.Element(main + "v")?.Value ?? string.Empty;
                if (type == "s" && int.TryParse(raw, out var index) && index >= 0 && index < shared.Length) raw = shared[index];
                values[column] = raw.Trim();
            }
            var width = values.Count == 0 ? 0 : values.Keys.Max() + 1;
            var cells = Enumerable.Range(0, width).Select(index => values.GetValueOrDefault(index, string.Empty)).ToArray();
            if (cells.Any(x => !string.IsNullOrWhiteSpace(x))) rows.Add(cells);
        }
        return rows;
    }

    private static DatasetAnalysis Analyze(DatasetSheet sheet, int? requestedHeaderRow)
    {
        if (sheet.Rows.Count == 0) return DatasetAnalysis.Empty(sheet.Name);
        var index = requestedHeaderRow.HasValue
            ? Math.Clamp(requestedHeaderRow.Value - 1, 0, sheet.Rows.Count - 1)
            : Enumerable.Range(0, Math.Min(20, sheet.Rows.Count)).OrderByDescending(i => HeaderScore(sheet.Rows[i])).First();
        var headers = UniqueHeaders(sheet.Rows[index]);
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var canonical in Aliases)
        {
            var match = headers.FirstOrDefault(header => canonical.Value.Contains(Normalize(header), StringComparer.OrdinalIgnoreCase));
            if (match is not null) mappings[canonical.Key] = match;
        }
        var rows = sheet.Rows.Skip(index + 1).Take(MaximumRows).Where(row => row.Any(x => !string.IsNullOrWhiteSpace(x)))
            .Select(row => headers.Select((header, column) => new { header, value = column < row.Length ? row[column] : string.Empty })
                .ToDictionary(x => x.header, x => x.value, StringComparer.OrdinalIgnoreCase)).ToList();
        return new DatasetAnalysis(sheet.Name, index + 1, headers, mappings, rows, HeaderScore(sheet.Rows[index]));
    }

    private static int HeaderScore(string[] row) => row.Count(value => Aliases.Values.Any(aliases => aliases.Contains(Normalize(value), StringComparer.OrdinalIgnoreCase))) * 100 + row.Count(value => !string.IsNullOrWhiteSpace(value));
    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static int ColumnIndex(string reference) { var result = 0; foreach (var c in reference.TakeWhile(char.IsLetter)) result = result * 26 + char.ToUpperInvariant(c) - 'A' + 1; return Math.Max(0, result - 1); }
    private static string[] UniqueHeaders(string[] row)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return row.Select((value, index) => { var name = string.IsNullOrWhiteSpace(value) ? $"Column {index + 1}" : value.Trim(); var candidate = name; var suffix = 2; while (!used.Add(candidate)) candidate = $"{name} ({suffix++})"; return candidate; }).ToArray();
    }

    private static List<string[]> ParseDelimited(string text)
    {
        var firstLine = text.Split('\n').FirstOrDefault() ?? string.Empty;
        var delimiter = firstLine.Count(x => x == ';') > firstLine.Count(x => x == ',') ? ';' : ',';
        var rows = new List<string[]>(); var row = new List<string>(); var value = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"' && quoted && i + 1 < text.Length && text[i + 1] == '"') { value.Append('"'); i++; }
            else if (c == '"') quoted = !quoted;
            else if (c == delimiter && !quoted) { row.Add(value.ToString().Trim()); value.Clear(); }
            else if ((c == '\r' || c == '\n') && !quoted) { if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; row.Add(value.ToString().Trim()); value.Clear(); if (row.Any(x => x.Length > 0)) rows.Add(row.ToArray()); row = []; if (rows.Count > MaximumRows + 30) break; }
            else value.Append(c);
        }
        if (value.Length > 0 || row.Count > 0) { row.Add(value.ToString().Trim()); rows.Add(row.ToArray()); }
        return rows;
    }

    private sealed record DatasetSheet(string Name, List<string[]> Rows);
    private sealed record DatasetAnalysis(string Name, int HeaderRow, string[] Headers, IReadOnlyDictionary<string, string> SuggestedMappings, List<Dictionary<string, string>> DataRows, int RecognitionScore)
    {
        internal static DatasetAnalysis Empty(string name) => new(name, 1, [], new Dictionary<string, string>(), [], 0);
    }
}

internal sealed record ProspectDatasetPreview(string FileName, string Format, string SelectedSheet, string[] Sheets, int HeaderRow, string[] Headers, IReadOnlyDictionary<string, string> SuggestedMappings, int TotalRows, Dictionary<string, string>[] SampleRows, List<Dictionary<string, string>> Rows);
