namespace Opiniones.ExtractionWorker.Configuration;

public sealed class ExtractionOptions
{
    public string StagingDirectory { get; set; } = "./staging";
}

public sealed class CsvSourceOptions
{
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "../DatosCSV/surveys_part1.csv";
}

public sealed class DatabaseSourceOptions
{
    public bool Enabled { get; set; } = true;
    public string ConnectionString { get; set; } = "";
    public string Query { get; set; } = "SELECT CAST('ReseniaWeb' AS varchar(30)) AS TipoOpinion, IdReview AS IdRegistro, IdCliente, IdProducto, IdFuente, FechaResenia AS FechaOpinion, Comentario, Rating AS Puntaje, CAST(NULL AS varchar(50)) AS Clasificacion FROM dbo.ReseniasWeb WHERE Estado = 1 ORDER BY FechaResenia, IdReview";
}

public sealed class ApiSourceOptions
{
    public bool Enabled { get; set; } = true;
    public string Endpoint { get; set; } = "http://localhost:5080/api/comments";
    public string BearerToken { get; set; } = "";
}
