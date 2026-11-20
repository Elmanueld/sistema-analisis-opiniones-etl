using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ProgramaETL
{
    internal static class Program
    {
        private static readonly string[] RequiredFiles = {
            "clients.csv", "products.csv", "fuente_datos.csv",
            "social_comments.csv", "surveys_part1.csv", "web_reviews.csv"
        };

        private static int Main(string[] args)
        {
            try
            {
                string inputDirectory = args.Length > 0 ? Path.GetFullPath(args[0]) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string reportDirectory = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(Environment.CurrentDirectory, "etl-output");
                string connectionString = Environment.GetEnvironmentVariable("SISTEMA_BD_CONNECTION_STRING");
                if (String.IsNullOrWhiteSpace(connectionString))
                {
                    DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                    while (directory != null && String.IsNullOrWhiteSpace(connectionString))
                    {
                        string configPath = Path.Combine(directory.FullName, "config-conexion.txt");
                        if (File.Exists(configPath)) connectionString = File.ReadAllText(configPath).Trim();
                        directory = directory.Parent;
                    }
                }
                if (String.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = @"Server=.\SQLEXPRESS;Database=SistemaBDTransaccional;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
                }

                Dictionary<string, List<CsvRow>> files = LoadAndValidateFiles(inputDirectory);
                Directory.CreateDirectory(reportDirectory);
                var loader = new Loader(connectionString);
                loader.Run(files);
                loader.WriteReports(reportDirectory);
                loader.PrintSummary(reportDirectory);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("No se pudo completar la carga: " + ex.Message);
                return 1;
            }
        }

        private static Dictionary<string, List<CsvRow>> LoadAndValidateFiles(string directory)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException("No existe la carpeta de entrada: " + directory);

            var result = new Dictionary<string, List<CsvRow>>(StringComparer.OrdinalIgnoreCase);
            foreach (string fileName in RequiredFiles)
            {
                string path = Path.Combine(directory, fileName);
                if (!File.Exists(path)) throw new FileNotFoundException("Falta el CSV requerido: " + fileName, path);
                result.Add(fileName, CsvFile.Read(path));
            }

            RequireHeaders(result["clients.csv"], "IdCliente", "Nombre", "Email");
            RequireHeaders(result["products.csv"], "IdProducto", "Nombre", "Categoría");
            RequireHeaders(result["fuente_datos.csv"], "IdFuente", "TipoFuente", "FechaCarga");
            RequireHeaders(result["social_comments.csv"], "IdComment", "IdCliente", "IdProducto", "Fuente", "Fecha", "Comentario");
            RequireHeaders(result["surveys_part1.csv"], "IdOpinion", "IdCliente", "IdProducto", "Fecha", "Comentario", "Clasificación", "PuntajeSatisfacción", "Fuente");
            RequireHeaders(result["web_reviews.csv"], "IdReview", "IdCliente", "IdProducto", "Fecha", "Comentario", "Rating");
            return result;
        }

        private static void RequireHeaders(List<CsvRow> rows, params string[] headers)
        {
            if (rows.Count == 0) throw new InvalidDataException("El CSV no contiene una fila de encabezados.");
            foreach (string header in headers)
                if (!rows[0].Values.ContainsKey(CsvFile.NormalizeHeader(header)))
                    throw new InvalidDataException("Falta la columna obligatoria '" + header + "'.");
        }
    }

    internal sealed class Loader
    {
        private readonly string connectionString;
        private readonly Dictionary<string, int> inserted = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<RejectedRow> rejected = new List<RejectedRow>();
        private readonly Dictionary<int, int> clientMap = new Dictionary<int, int>();
        private readonly Dictionary<int, int> productMap = new Dictionary<int, int>();
        private readonly Dictionary<string, int> categoryMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> classificationMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> typeMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> sourceByFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private SqlConnection connection;
        private SqlTransaction transaction;
        private int processed;
        private int duplicates;

        public Loader(string connectionString) { this.connectionString = connectionString; }

        public void Run(Dictionary<string, List<CsvRow>> files)
        {
            using (connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        LoadCatalog("SELECT IdCategoria, Nombre FROM dbo.Categoria WHERE Estado = 1", categoryMap);
                        LoadCatalog("SELECT IdClasificacion, Nombre FROM dbo.Clasificacion WHERE Estado = 1", classificationMap);
                        LoadCatalog("SELECT IdTipoFuente, Nombre FROM dbo.TipoFuente", typeMap);
                        EnsureTypeAlias("Red Social", "API");

                        LoadFileSources();
                        LoadClients(files["clients.csv"]);
                        LoadProducts(files["products.csv"]);
                        LoadDataSources(files["fuente_datos.csv"]);
                        LoadSocialComments(files["social_comments.csv"]);
                        LoadSurveys(files["surveys_part1.csv"]);
                        LoadWebReviews(files["web_reviews.csv"]);

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void LoadCatalog(string sql, Dictionary<string, int> map)
        {
            using (var command = Command(sql))
            using (SqlDataReader reader = command.ExecuteReader())
                while (reader.Read()) map[reader.GetString(1)] = reader.GetInt32(0);
        }

        private void EnsureTypeAlias(string alias, string canonical)
        {
            if (!typeMap.ContainsKey(canonical)) throw new InvalidOperationException("No existe el tipo fuente requerido: " + canonical);
            typeMap[alias] = typeMap[canonical];
        }

        private void LoadFileSources()
        {
            EnsureFileSource("clients.csv", "CSV");
            EnsureFileSource("products.csv", "CSV");
            EnsureFileSource("fuente_datos.csv", "CSV");
            EnsureFileSource("social_comments.csv", "API");
            EnsureFileSource("surveys_part1.csv", "CSV");
            EnsureFileSource("web_reviews.csv", "Web");
        }

        private void EnsureFileSource(string fileName, string typeName)
        {
            // La fila de procedencia generada también forma parte de los registros procesados.
            processed++;
            if (!typeMap.TryGetValue(typeName, out int typeId))
                throw new InvalidOperationException("No existe el tipo de fuente '" + typeName + "' en TipoFuente.");
            const string descriptionPrefix = "ETL archivo=";
            string description = descriptionPrefix + fileName;
            object existing = Scalar("SELECT TOP (1) IdFuente FROM dbo.FuenteDatos WHERE IdTipoFuente=@type AND DescripcionCarga=@description ORDER BY IdFuente",
                P("@type", SqlDbType.Int, typeId), P("@description", SqlDbType.VarChar, description, 500));
            int sourceId;
            if (existing != null && existing != DBNull.Value)
            {
                sourceId = Convert.ToInt32(existing, CultureInfo.InvariantCulture);
                duplicates++;
            }
            else
            {
                sourceId = InsertIdentity("INSERT dbo.FuenteDatos (IdTipoFuente, FechaCarga, Estado, DescripcionCarga) VALUES (@type, GETDATE(), 'Procesado', @description); SELECT CONVERT(int,SCOPE_IDENTITY());",
                    P("@type", SqlDbType.Int, typeId), P("@description", SqlDbType.VarChar, description, 500));
                AddInserted("FuenteDatos (origen ETL)");
            }
            sourceByFile[fileName] = sourceId;
        }

        private void LoadClients(List<CsvRow> rows)
        {
            var sourceIds = new Dictionary<int, string>();
            foreach (CsvRow row in DataRows(rows))
            {
                processed++;
                string externalId = row.Get("IdCliente");
                try
                {
                    int sourceId = ParsePositiveInt(externalId, "IdCliente");
                    string name = RequiredText(row.Get("Nombre"), "Nombre", 150);
                    string email = RequiredText(row.Get("Email"), "Email", 120).ToLowerInvariant();
                    ValidateEmail(email);
                    if (sourceIds.TryGetValue(sourceId, out string priorEmail))
                    {
                        if (String.Equals(priorEmail, email, StringComparison.OrdinalIgnoreCase)) { duplicates++; continue; }
                        throw new RowValidationException("IdCliente externo duplicado con datos distintos.");
                    }
                    sourceIds.Add(sourceId, email);

                    object existing = Scalar("SELECT TOP (1) IdCliente FROM dbo.Clientes WHERE Email=@email", P("@email", SqlDbType.VarChar, email, 120));
                    int databaseId;
                    if (existing != null && existing != DBNull.Value)
                    {
                        databaseId = Convert.ToInt32(existing, CultureInfo.InvariantCulture);
                        string existingName = Convert.ToString(Scalar("SELECT Nombre FROM dbo.Clientes WHERE IdCliente=@id", P("@id", SqlDbType.Int, databaseId)), CultureInfo.InvariantCulture);
                        if (!String.Equals(existingName, name, StringComparison.Ordinal))
                            throw new RowValidationException("El email ya existe con un nombre diferente.");
                        duplicates++;
                    }
                    else
                    {
                        databaseId = InsertIdentity("INSERT dbo.Clientes (Nombre, Email, Estado) VALUES (@name,@email,1); SELECT CONVERT(int,SCOPE_IDENTITY());",
                            P("@name", SqlDbType.VarChar, name, 150), P("@email", SqlDbType.VarChar, email, 120));
                        AddInserted("Clientes");
                    }
                    clientMap[sourceId] = databaseId;
                }
                catch (RowValidationException ex) { Reject("clients.csv", row, externalId, ex.Message); }
                catch (FormatException ex) { Reject("clients.csv", row, externalId, ex.Message); }
            }
        }

        private void LoadProducts(List<CsvRow> rows)
        {
            var sourceIds = new Dictionary<int, string>();
            foreach (CsvRow row in DataRows(rows))
            {
                processed++;
                string externalId = row.Get("IdProducto");
                try
                {
                    int sourceId = ParsePositiveInt(externalId, "IdProducto");
                    string name = RequiredText(row.Get("Nombre"), "Nombre", 200);
                    string category = RequiredText(row.Get("Categoría"), "Categoría", 100);
                    if (!categoryMap.TryGetValue(category, out int categoryId))
                        throw new RowValidationException("La categoría no existe en dbo.Categoria: " + category);
                    if (sourceIds.TryGetValue(sourceId, out string priorName))
                    {
                        if (String.Equals(priorName, name, StringComparison.Ordinal)) { duplicates++; continue; }
                        throw new RowValidationException("IdProducto externo duplicado con datos distintos.");
                    }
                    sourceIds.Add(sourceId, name);

                    object existing = Scalar("SELECT TOP (1) IdProducto FROM dbo.Productos WHERE Nombre=@name AND IdCategoria=@category ORDER BY IdProducto",
                        P("@name", SqlDbType.VarChar, name, 200), P("@category", SqlDbType.Int, categoryId));
                    int databaseId;
                    if (existing != null && existing != DBNull.Value)
                    {
                        databaseId = Convert.ToInt32(existing, CultureInfo.InvariantCulture);
                        duplicates++;
                    }
                    else
                    {
                        // products.csv no incluye Precio; la tabla exige un decimal NOT NULL.
                        databaseId = InsertIdentity("INSERT dbo.Productos (Nombre, IdCategoria, Precio, Estado) VALUES (@name,@category,0.00,1); SELECT CONVERT(int,SCOPE_IDENTITY());",
                            P("@name", SqlDbType.VarChar, name, 200), P("@category", SqlDbType.Int, categoryId));
                        AddInserted("Productos");
                    }
                    productMap[sourceId] = databaseId;
                }
                catch (RowValidationException ex) { Reject("products.csv", row, externalId, ex.Message); }
                catch (FormatException ex) { Reject("products.csv", row, externalId, ex.Message); }
            }
        }

        private void LoadDataSources(List<CsvRow> rows)
        {
            var sourceIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (CsvRow row in DataRows(rows))
            {
                processed++;
                string externalId = row.Get("IdFuente");
                try
                {
                    ValidateExternalCode(externalId, 'F', "IdFuente");
                    if (String.IsNullOrWhiteSpace(externalId) || externalId.Length > 50)
                        throw new RowValidationException("IdFuente es obligatorio y debe tener como máximo 50 caracteres.");
                    string rawType = RequiredText(row.Get("TipoFuente"), "TipoFuente", 50);
                    if (!typeMap.TryGetValue(rawType, out int typeId))
                        throw new RowValidationException("TipoFuente inválido; se esperaba Web, CSV o Red Social: " + rawType);
                    DateTime loadDate = ParseDate(row.Get("FechaCarga"), "FechaCarga");
                    string signature = rawType + "|" + loadDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    if (sourceIds.TryGetValue(externalId, out string priorSignature))
                    {
                        if (priorSignature == signature) { duplicates++; continue; }
                        throw new RowValidationException("IdFuente externo duplicado con datos distintos.");
                    }
                    sourceIds.Add(externalId, signature);

                    string description = "CSV fuente_datos.csv | IdFuente externo=" + externalId + " | Tipo original=" + rawType;
                    object existing = Scalar("SELECT TOP (1) IdFuente FROM dbo.FuenteDatos WHERE IdTipoFuente=@type AND DescripcionCarga=@description",
                        P("@type", SqlDbType.Int, typeId), P("@description", SqlDbType.VarChar, description, 500));
                    if (existing != null && existing != DBNull.Value) { duplicates++; continue; }
                    Execute("INSERT dbo.FuenteDatos (IdTipoFuente, FechaCarga, Estado, DescripcionCarga) VALUES (@type,@date,'Importado',@description)",
                        P("@type", SqlDbType.Int, typeId), P("@date", SqlDbType.DateTime, loadDate), P("@description", SqlDbType.VarChar, description, 500));
                    AddInserted("FuenteDatos (CSV)");
                }
                catch (RowValidationException ex) { Reject("fuente_datos.csv", row, externalId, ex.Message); }
                catch (FormatException ex) { Reject("fuente_datos.csv", row, externalId, ex.Message); }
            }
        }

        private void LoadSocialComments(List<CsvRow> rows)
        {
            int sourceId = sourceByFile["social_comments.csv"];
            foreach (CsvRow row in DataRows(rows))
            {
                processed++;
                string externalId = row.Get("IdComment");
                try
                {
                    ValidateExternalCode(externalId, 'T', "IdComment");
                    string customerRaw = row.Get("IdCliente");
                    int? customerId = String.IsNullOrWhiteSpace(customerRaw) ? (int?)null : ResolvePrefixedId(customerRaw, 'C', clientMap, "IdCliente");
                    int productId = ResolvePrefixedId(row.Get("IdProducto"), 'P', productMap, "IdProducto");
                    string network = RequiredText(row.Get("Fuente"), "Fuente", 50);
                    string comment = RequiredText(row.Get("Comentario"), "Comentario", 1000);
                    DateTime date = ParseDate(row.Get("Fecha"), "Fecha");
                    object existing = Scalar("SELECT TOP (1) IdComment FROM dbo.ComentariosSociales WHERE ((IdCliente=@client) OR (IdCliente IS NULL AND @client IS NULL)) AND IdProducto=@product AND RedSocial=@network AND Comentario=@comment AND FechaComentario=@date",
                        P("@client", SqlDbType.Int, customerId.HasValue ? (object)customerId.Value : DBNull.Value), P("@product", SqlDbType.Int, productId), P("@network", SqlDbType.VarChar, network, 50), P("@comment", SqlDbType.VarChar, comment, 1000), P("@date", SqlDbType.DateTime, date));
                    if (existing != null && existing != DBNull.Value) { duplicates++; continue; }
                    Execute("INSERT dbo.ComentariosSociales (IdCliente,IdProducto,IdFuente,RedSocial,Comentario,FechaComentario,Estado) VALUES (@client,@product,@source,@network,@comment,@date,1)",
                        P("@client", SqlDbType.Int, customerId.HasValue ? (object)customerId.Value : DBNull.Value), P("@product", SqlDbType.Int, productId), P("@source", SqlDbType.Int, sourceId), P("@network", SqlDbType.VarChar, network, 50), P("@comment", SqlDbType.VarChar, comment, 1000), P("@date", SqlDbType.DateTime, date));
                    AddInserted("ComentariosSociales");
                }
                catch (RowValidationException ex) { Reject("social_comments.csv", row, externalId, ex.Message); }
                catch (FormatException ex) { Reject("social_comments.csv", row, externalId, ex.Message); }
            }
        }

        private void LoadSurveys(List<CsvRow> rows)
        {
            int sourceId = sourceByFile["surveys_part1.csv"];
            foreach (CsvRow row in DataRows(rows))
            {
                processed++;
                string externalId = row.Get("IdOpinion");
                try
                {
                    ParsePositiveInt(externalId, "IdOpinion");
                    int customerId = ResolveNumericId(row.Get("IdCliente"), clientMap, "IdCliente");
                    int productId = ResolveNumericId(row.Get("IdProducto"), productMap, "IdProducto");
                    DateTime date = ParseDate(row.Get("Fecha"), "Fecha");
                    string comment = OptionalText(row.Get("Comentario"), "Comentario", 1000);
                    string rawClassification = RequiredText(row.Get("Clasificación"), "Clasificación", 50);
                    RequiredText(row.Get("Fuente"), "Fuente", 50);
                    string classification = NormalizeClassification(rawClassification);
                    if (!classificationMap.TryGetValue(classification, out int classificationId))
                        throw new RowValidationException("Clasificación no encontrada en el catálogo: " + rawClassification);
                    int score = ParseInt(row.Get("PuntajeSatisfacción"), "PuntajeSatisfacción");
                    if (score < 1 || score > 5) throw new RowValidationException("PuntajeSatisfacción debe estar entre 1 y 5.");

                    object existing = Scalar("SELECT TOP (1) IdOpinion FROM dbo.EncuestasInternas WHERE IdCliente=@client AND IdProducto=@product AND IdClasificacion=@classification AND PuntajeSatisfaccion=@score AND FechaOpinion=@date AND ISNULL(Comentario,'')=ISNULL(@comment,'')",
                        P("@client", SqlDbType.Int, customerId), P("@product", SqlDbType.Int, productId), P("@classification", SqlDbType.Int, classificationId), P("@score", SqlDbType.Int, score), P("@date", SqlDbType.DateTime, date), P("@comment", SqlDbType.VarChar, comment == null ? (object)DBNull.Value : comment, 1000));
                    if (existing != null && existing != DBNull.Value) { duplicates++; continue; }
                    Execute("INSERT dbo.EncuestasInternas (IdCliente,IdProducto,IdFuente,IdClasificacion,Comentario,PuntajeSatisfaccion,FechaOpinion,Estado) VALUES (@client,@product,@source,@classification,@comment,@score,@date,1)",
                        P("@client", SqlDbType.Int, customerId), P("@product", SqlDbType.Int, productId), P("@source", SqlDbType.Int, sourceId), P("@classification", SqlDbType.Int, classificationId), P("@comment", SqlDbType.VarChar, comment == null ? (object)DBNull.Value : comment, 1000), P("@score", SqlDbType.Int, score), P("@date", SqlDbType.DateTime, date));
                    AddInserted("EncuestasInternas");
                }
                catch (RowValidationException ex) { Reject("surveys_part1.csv", row, externalId, ex.Message); }
                catch (FormatException ex) { Reject("surveys_part1.csv", row, externalId, ex.Message); }
            }
        }

        private void LoadWebReviews(List<CsvRow> rows)
        {
            int sourceId = sourceByFile["web_reviews.csv"];
            foreach (CsvRow row in DataRows(rows))
            {
                processed++;
                string externalId = row.Get("IdReview");
                try
                {
                    ValidateExternalCode(externalId, 'W', "IdReview");
                    int customerId = ResolvePrefixedId(row.Get("IdCliente"), 'C', clientMap, "IdCliente");
                    int productId = ResolvePrefixedId(row.Get("IdProducto"), 'P', productMap, "IdProducto");
                    DateTime date = ParseDate(row.Get("Fecha"), "Fecha");
                    string comment = OptionalText(row.Get("Comentario"), "Comentario", 1000);
                    int rating = ParseInt(row.Get("Rating"), "Rating");
                    if (rating < 1 || rating > 5) throw new RowValidationException("Rating debe estar entre 1 y 5.");
                    object existing = Scalar("SELECT TOP (1) IdReview FROM dbo.ReseniasWeb WHERE IdCliente=@client AND IdProducto=@product AND Rating=@rating AND FechaResenia=@date AND ISNULL(Comentario,'')=ISNULL(@comment,'')",
                        P("@client", SqlDbType.Int, customerId), P("@product", SqlDbType.Int, productId), P("@rating", SqlDbType.Int, rating), P("@date", SqlDbType.DateTime, date), P("@comment", SqlDbType.VarChar, comment == null ? (object)DBNull.Value : comment, 1000));
                    if (existing != null && existing != DBNull.Value) { duplicates++; continue; }
                    Execute("INSERT dbo.ReseniasWeb (IdCliente,IdProducto,IdFuente,Comentario,Rating,FechaResenia,Estado) VALUES (@client,@product,@source,@comment,@rating,@date,1)",
                        P("@client", SqlDbType.Int, customerId), P("@product", SqlDbType.Int, productId), P("@source", SqlDbType.Int, sourceId), P("@comment", SqlDbType.VarChar, comment == null ? (object)DBNull.Value : comment, 1000), P("@rating", SqlDbType.Int, rating), P("@date", SqlDbType.DateTime, date));
                    AddInserted("ReseniasWeb");
                }
                catch (RowValidationException ex) { Reject("web_reviews.csv", row, externalId, ex.Message); }
                catch (FormatException ex) { Reject("web_reviews.csv", row, externalId, ex.Message); }
            }
        }

        private void AddInserted(string table)
        {
            inserted[table] = inserted.ContainsKey(table) ? inserted[table] + 1 : 1;
        }

        private void Reject(string file, CsvRow row, string key, string reason)
        {
            rejected.Add(new RejectedRow(file, row.RecordNumber, key, reason));
        }

        private static IEnumerable<CsvRow> DataRows(List<CsvRow> rows) { return rows.Skip(1); }

        private static string NormalizeClassification(string value)
        {
            string normalized = value.Trim();
            if (String.Equals(normalized, "Positiva", StringComparison.OrdinalIgnoreCase)) return "Positivo";
            if (String.Equals(normalized, "Neutra", StringComparison.OrdinalIgnoreCase)) return "Neutral";
            if (String.Equals(normalized, "Negativa", StringComparison.OrdinalIgnoreCase)) return "Negativo";
            return normalized;
        }

        private static int ResolveNumericId(string value, Dictionary<int, int> map, string field)
        {
            int externalId = ParsePositiveInt(value, field);
            if (!map.TryGetValue(externalId, out int databaseId))
                throw new RowValidationException(field + " " + externalId + " no tiene correspondencia en los CSV de clientes/productos; se rechaza por integridad referencial.");
            return databaseId;
        }

        private static int ResolvePrefixedId(string value, char prefix, Dictionary<int, int> map, string field)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Length < 2 || Char.ToUpperInvariant(value[0]) != prefix)
                throw new RowValidationException(field + " debe tener formato " + prefix + " seguido de un número.");
            return ResolveNumericId(value.Substring(1), map, field);
        }

        private static string RequiredText(string value, string field, int maxLength)
        {
            string text = (value ?? String.Empty).Trim();
            if (text.Length == 0) throw new RowValidationException(field + " es obligatorio.");
            if (text.Length > maxLength) throw new RowValidationException(field + " supera el máximo de " + maxLength + " caracteres.");
            return text;
        }

        private static string OptionalText(string value, string field, int maxLength)
        {
            string text = (value ?? String.Empty).Trim();
            if (text.Length == 0) return null;
            if (text.Length > maxLength) throw new RowValidationException(field + " supera el máximo de " + maxLength + " caracteres.");
            return text;
        }

        private static int ParsePositiveInt(string value, string field)
        {
            int result = ParseInt(value, field);
            if (result <= 0) throw new RowValidationException(field + " debe ser mayor que cero.");
            return result;
        }

        private static void ValidateExternalCode(string value, char prefix, string field)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Length < 2 || Char.ToUpperInvariant(value[0]) != prefix)
                throw new RowValidationException(field + " debe tener formato " + prefix + " seguido de un número.");
            ParsePositiveInt(value.Substring(1), field);
        }

        private static int ParseInt(string value, string field)
        {
            if (!Int32.TryParse((value ?? String.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                throw new RowValidationException(field + " no tiene un formato entero válido.");
            return result;
        }

        private static DateTime ParseDate(string value, string field)
        {
            if (!DateTime.TryParseExact((value ?? String.Empty).Trim(), new[] { "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                throw new RowValidationException(field + " debe tener formato yyyy-MM-dd.");
            return date;
        }

        private static void ValidateEmail(string email)
        {
            int at = email.IndexOf('@');
            if (at <= 0 || at != email.LastIndexOf('@') || at >= email.Length - 3 || email.IndexOf('.', at) < at + 2 || email.EndsWith(".", StringComparison.Ordinal))
                throw new RowValidationException("Email no tiene un formato válido.");
        }

        private SqlCommand Command(string sql, params SqlParameter[] parameters)
        {
            var command = new SqlCommand(sql, connection, transaction);
            if (parameters != null) command.Parameters.AddRange(parameters);
            return command;
        }

        private object Scalar(string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand command = Command(sql, parameters)) return command.ExecuteScalar();
        }

        private int InsertIdentity(string sql, params SqlParameter[] parameters)
        {
            return Convert.ToInt32(Scalar(sql, parameters), CultureInfo.InvariantCulture);
        }

        private void Execute(string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand command = Command(sql, parameters)) command.ExecuteNonQuery();
        }

        private static SqlParameter P(string name, SqlDbType type, object value, int size = 0)
        {
            var parameter = new SqlParameter(name, type);
            if (size > 0) parameter.Size = size;
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }

        public void WriteReports(string directory)
        {
            string rejectPath = Path.Combine(directory, "registros_rechazados.csv");
            using (var writer = new StreamWriter(rejectPath, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("Archivo,Fila,IdOrigen,Motivo");
                foreach (RejectedRow row in rejected)
                    writer.WriteLine(String.Join(",", new[] { CsvEscape(row.File), row.RecordNumber.ToString(CultureInfo.InvariantCulture), CsvEscape(row.SourceId), CsvEscape(row.Reason) }));
            }

            var report = new StringBuilder();
            report.AppendLine("Sistema ETL - SistemaBDTransaccional");
            report.AppendLine("Fecha: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            report.AppendLine("Procesados: " + processed.ToString(CultureInfo.InvariantCulture));
            report.AppendLine("Insertados: " + inserted.Values.Sum().ToString(CultureInfo.InvariantCulture));
            report.AppendLine("Duplicados omitidos: " + duplicates.ToString(CultureInfo.InvariantCulture));
            report.AppendLine("Rechazados: " + rejected.Count.ToString(CultureInfo.InvariantCulture));
            foreach (KeyValuePair<string, int> item in inserted.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                report.AppendLine(item.Key + ": " + item.Value.ToString(CultureInfo.InvariantCulture));
            report.AppendLine("Detalle de rechazos: registros_rechazados.csv");
            File.WriteAllText(Path.Combine(directory, "resumen_etl.txt"), report.ToString(), new UTF8Encoding(true));
        }

        public void PrintSummary(string directory)
        {
            Console.WriteLine("=== Resumen de carga ETL ===");
            Console.WriteLine("Base de datos: SistemaBDTransaccional");
            Console.WriteLine("Registros procesados: " + processed);
            Console.WriteLine("Registros insertados: " + inserted.Values.Sum());
            Console.WriteLine("Duplicados omitidos: " + duplicates);
            Console.WriteLine("Registros rechazados: " + rejected.Count);
            foreach (KeyValuePair<string, int> item in inserted.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine("  " + item.Key + ": " + item.Value);
            Console.WriteLine("Reporte: " + Path.Combine(directory, "resumen_etl.txt"));
            Console.WriteLine("Rechazos: " + Path.Combine(directory, "registros_rechazados.csv"));
        }

        private static string CsvEscape(string value)
        {
            string text = value ?? String.Empty;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }

    internal sealed class CsvRow
    {
        public int RecordNumber { get; private set; }
        public Dictionary<string, string> Values { get; private set; }
        public CsvRow(int number, Dictionary<string, string> values) { RecordNumber = number; Values = values; }
        public string Get(string header)
        {
            string value;
            return Values.TryGetValue(CsvFile.NormalizeHeader(header), out value) ? value : String.Empty;
        }
    }

    internal static class CsvFile
    {
        public static List<CsvRow> Read(string path)
        {
            var result = new List<CsvRow>();
            using (var parser = new TextFieldParser(path, Encoding.UTF8, true))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;
                parser.TrimWhiteSpace = false;
                if (parser.EndOfData) throw new InvalidDataException("CSV vacío: " + Path.GetFileName(path));
                string[] headers = parser.ReadFields();
                if (headers == null || headers.Length == 0) throw new InvalidDataException("No se pudieron leer encabezados: " + Path.GetFileName(path));
                var normalizedHeaders = headers.Select(NormalizeHeader).ToArray();
                if (normalizedHeaders.Any(String.IsNullOrWhiteSpace)) throw new InvalidDataException("Hay una columna sin nombre: " + Path.GetFileName(path));
                if (normalizedHeaders.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedHeaders.Length)
                    throw new InvalidDataException("Hay columnas repetidas en el encabezado: " + Path.GetFileName(path));

                var headerValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string header in normalizedHeaders) headerValues.Add(header, String.Empty);
                result.Add(new CsvRow(1, headerValues));
                int rowNumber = 1;
                while (!parser.EndOfData)
                {
                    string[] fields;
                    try { fields = parser.ReadFields(); }
                    catch (MalformedLineException ex) { throw new InvalidDataException("CSV mal formado cerca de la línea " + parser.ErrorLineNumber + " en " + Path.GetFileName(path), ex); }
                    rowNumber++;
                    if (fields == null) continue;
                    if (fields.Length != headers.Length)
                        throw new InvalidDataException("La fila " + rowNumber + " de " + Path.GetFileName(path) + " contiene " + fields.Length + " columnas; se esperaban " + headers.Length + ".");
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < normalizedHeaders.Length; i++) row.Add(normalizedHeaders[i], fields[i] ?? String.Empty);
                    result.Add(new CsvRow(rowNumber, row));
                }
            }
            return result;
        }

        public static string NormalizeHeader(string value)
        {
            string normalized = (value ?? String.Empty).Trim().Normalize(NormalizationForm.FormD);
            var result = new StringBuilder();
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (Char.IsLetterOrDigit(c)) result.Append(Char.ToLowerInvariant(c));
            }
            return result.ToString().Normalize(NormalizationForm.FormC);
        }
    }

    internal sealed class RejectedRow
    {
        public string File { get; private set; }
        public int RecordNumber { get; private set; }
        public string SourceId { get; private set; }
        public string Reason { get; private set; }
        public RejectedRow(string file, int recordNumber, string sourceId, string reason)
        {
            File = file; RecordNumber = recordNumber; SourceId = sourceId; Reason = reason;
        }
    }

    internal sealed class RowValidationException : Exception
    {
        public RowValidationException(string message) : base(message) { }
    }
}
