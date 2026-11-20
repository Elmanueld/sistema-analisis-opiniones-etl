using System.Runtime.CompilerServices;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Opiniones.ExtractionWorker.Configuration;
using Opiniones.ExtractionWorker.Models;

namespace Opiniones.ExtractionWorker.Extraction;

public sealed class CsvExtractor(IOptions<CsvSourceOptions> options, ILogger<CsvExtractor> logger) : ExtractorBase
{
    private readonly CsvSourceOptions _options = options.Value;
    public override string SourceName => "CsvSurveys";
    public override bool IsEnabled => _options.Enabled;

    public override async IAsyncEnumerable<ExtractedOpinion> ExtractAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var path = ResolveCsvPath(_options.Path);
        logger.LogInformation("Extrayendo encuestas CSV desde {Path}", path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        await using var rows = ReadCsvRowsAsync(reader, cancellationToken).GetAsyncEnumerator(cancellationToken);
        if (!await rows.MoveNextAsync()) throw new InvalidDataException("El CSV está vacío y no contiene encabezados.");
        var headers = rows.Current.Select(NormalizeHeader).ToArray();
        var index = headers.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i);
        string[] required = ["idopinion", "idcliente", "idproducto", "fecha", "comentario", "clasificacion", "puntajesatisfaccion", "fuente"];
        var missing = required.Where(x => !index.ContainsKey(x)).ToArray();
        if (missing.Length > 0) throw new InvalidDataException($"Faltan columnas requeridas: {string.Join(", ", missing)}");

        long rowNumber = 1;
        while (await rows.MoveNextAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            var values = rows.Current;
            if (values.Length != headers.Length)
                throw new InvalidDataException($"La fila {rowNumber} contiene {values.Length} valores; se esperaban {headers.Length}.");
            string Value(string name) => values[index[name]].Trim();
            DateTimeOffset? date = DateTimeOffset.TryParse(Value("fecha"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var parsedDate) ? parsedDate : null;
            decimal? score = decimal.TryParse(Value("puntajesatisfaccion"), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var parsedScore) ? parsedScore : null;
            yield return new ExtractedOpinion
            {
                Source = SourceName,
                SourceRecordId = Value("idopinion"),
                Channel = Value("fuente"),
                CustomerId = EmptyToNull(Value("idcliente")),
                ProductId = EmptyToNull(Value("idproducto")),
                OpinionDate = date,
                Comment = EmptyToNull(Value("comentario")),
                Sentiment = EmptyToNull(Value("clasificacion")),
                Score = score
            };
        }
    }

    private static string ResolveCsvPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath)) return configuredPath;
        var direct = Path.GetFullPath(configuredPath);
        if (File.Exists(direct)) return direct;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.GetFullPath(Path.Combine(directory.FullName, configuredPath));
            if (File.Exists(candidate)) return candidate;
            var projectCandidate = Path.Combine(directory.FullName, "DatosCSV", "surveys_part1.csv");
            if (File.Exists(projectCandidate)) return projectCandidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"No se encontró el archivo CSV configurado: {configuredPath}");
    }

    private static string NormalizeHeader(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static async IAsyncEnumerable<string[]> ReadCsvRowsAsync(StreamReader reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new char[8192];
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var pendingQuote = false;
        var fieldStarted = false;
        var sawAnyCharacter = false;
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            for (var i = 0; i < read; i++)
            {
                var ch = buffer[i]; sawAnyCharacter = true;
                if (inQuotes)
                {
                    if (pendingQuote)
                    {
                        if (ch == '"') { field.Append('"'); pendingQuote = false; continue; }
                        inQuotes = false;
                        pendingQuote = false;
                    }
                    else if (ch == '"') { pendingQuote = true; continue; }
                    else { field.Append(ch); continue; }
                }
                if (ch == '"' && !fieldStarted) { inQuotes = true; fieldStarted = true; }
                else if (ch == ',') { row.Add(field.ToString()); field.Clear(); fieldStarted = false; }
                else if (ch == '\n' || ch == '\r')
                {
                    if (ch == '\r' && i + 1 < read && buffer[i + 1] == '\n') i++;
                    row.Add(field.ToString()); field.Clear(); fieldStarted = false;
                    yield return row.ToArray(); row.Clear(); sawAnyCharacter = false;
                }
                else { field.Append(ch); fieldStarted = true; }
            }
        }
        if (inQuotes && !pendingQuote) throw new InvalidDataException("El CSV termina dentro de un campo entrecomillado.");
        if (sawAnyCharacter || row.Count > 0 || field.Length > 0)
        {
            row.Add(field.ToString());
            yield return row.ToArray();
        }
    }
}
