using System.Runtime.CompilerServices;
using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Opiniones.ExtractionWorker.Configuration;
using Opiniones.ExtractionWorker.Models;

namespace Opiniones.ExtractionWorker.Extraction;

public sealed class DatabaseExtractor(IOptions<DatabaseSourceOptions> options, ILogger<DatabaseExtractor> logger) : ExtractorBase
{
    private readonly DatabaseSourceOptions _options = options.Value;
    public override string SourceName => "OperationalDatabase";
    public override bool IsEnabled => _options.Enabled;

    public override async IAsyncEnumerable<ExtractedOpinion> ExtractAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connectionString = _options.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OperationalDb") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure Sources:Database:ConnectionString o ConnectionStrings__OperationalDb.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = _options.Query;
        command.CommandTimeout = 120;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        logger.LogInformation("Consulta de extracción ejecutada sobre la fuente relacional.");
        var tipo = reader.GetOrdinal("TipoOpinion");
        var id = reader.GetOrdinal("IdRegistro");
        var cliente = reader.GetOrdinal("IdCliente");
        var producto = reader.GetOrdinal("IdProducto");
        var fuente = reader.GetOrdinal("IdFuente");
        var fecha = reader.GetOrdinal("FechaOpinion");
        var comentario = reader.GetOrdinal("Comentario");
        var puntaje = reader.GetOrdinal("Puntaje");
        var sentimiento = reader.GetOrdinal("Clasificacion");

        while (await reader.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ExtractedOpinion
            {
                Source = SourceName,
                SourceRecordId = $"{reader.GetString(tipo)}:{Convert.ToString(reader.GetValue(id), CultureInfo.InvariantCulture)}",
                Channel = reader.GetString(tipo),
                CustomerId = ReadText(reader, cliente),
                ProductId = ReadText(reader, producto),
                DataSourceId = ReadText(reader, fuente),
                OpinionDate = ReadDate(reader, fecha),
                Comment = ReadText(reader, comentario),
                Score = ReadDecimal(reader, puntaje),
                Sentiment = ReadText(reader, sentimiento)
            };
        }
    }

    private static string? ReadText(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    private static DateTimeOffset? ReadDate(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc));
    private static decimal? ReadDecimal(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
}
