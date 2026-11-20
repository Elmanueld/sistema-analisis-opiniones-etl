using Opiniones.ExtractionWorker;
using Microsoft.Extensions.Options;
using Opiniones.ExtractionWorker.Configuration;
using Opiniones.ExtractionWorker.Extraction;
using Opiniones.ExtractionWorker.Staging;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<ProgramMarker>(optional: true);
builder.Services.Configure<ExtractionOptions>(builder.Configuration.GetSection("Extraction"));
builder.Services.Configure<CsvSourceOptions>(builder.Configuration.GetSection("Sources:Csv"));
builder.Services.Configure<DatabaseSourceOptions>(builder.Configuration.GetSection("Sources:Database"));
builder.Services.Configure<ApiSourceOptions>(builder.Configuration.GetSection("Sources:Api"));
builder.Services.AddSingleton<IStagingWriter, JsonLinesStagingWriter>();
builder.Services.AddSingleton<IExtractor, CsvExtractor>();
builder.Services.AddSingleton<IExtractor, DatabaseExtractor>();
builder.Services.AddHttpClient<ApiExtractor>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Opiniones-ExtractionWorker/1.0");
});
builder.Services.AddSingleton<IExtractor>(sp => sp.GetRequiredService<ApiExtractor>());
builder.Services.AddHostedService<ExtractionWorker>();

await builder.Build().RunAsync();


