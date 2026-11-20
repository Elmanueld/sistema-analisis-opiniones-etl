using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/comments", async (IConfiguration configuration, IWebHostEnvironment environment, HttpContext context, CancellationToken cancellationToken) =>
{
    if (!environment.IsDevelopment())
    {
        var expectedToken = configuration["Api:BearerToken"] ?? Environment.GetEnvironmentVariable("Api__BearerToken");
        if (string.IsNullOrWhiteSpace(expectedToken))
            return Results.Problem("Configure Api:BearerToken antes de publicar la API.", statusCode: 503);

        var authorization = context.Request.Headers.Authorization.ToString();
        var suppliedToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..]
            : string.Empty;
        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        if (expectedBytes.Length != suppliedBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes))
            return Results.Unauthorized();
    }

    var connectionString = configuration.GetConnectionString("OperationalDb")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__OperationalDb");
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("Configure la conexión a SistemaBDTransaccional.", statusCode: 503);

    const string sql = """
        SELECT s.IdComment, s.IdCliente, s.IdProducto, s.RedSocial, s.Comentario,
               s.FechaComentario, s.LikesRetweets
        FROM dbo.ComentariosSociales AS s
        WHERE s.Estado = 1
        ORDER BY s.IdComment;
        """;
    var comments = new List<CommentDto>();
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);
    await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
    await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        comments.Add(new CommentDto(
            reader.GetInt32(0).ToString(),
            reader.IsDBNull(1) ? null : reader.GetInt32(1).ToString(),
            reader.GetInt32(2).ToString(),
            reader.GetString(3),
            reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            reader.GetString(4),
            reader.IsDBNull(6) ? null : reader.GetInt32(6)));
    }
    return Results.Ok(comments);
});

app.Run();

internal sealed record CommentDto(
    string IdComment,
    string? IdClient,
    string IdProduct,
    string Channel,
    DateTime? Date,
    string Comment,
    int? Engagement);
