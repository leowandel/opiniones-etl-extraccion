using Microsoft.Extensions.Options;
using OpinionesETL.Worker;
using OpinionesETL.Worker.Application;
using OpinionesETL.Worker.Configuration;
using OpinionesETL.Worker.Infrastructure.Extractors;
using OpinionesETL.Worker.Infrastructure.Loaders;
using OpinionesETL.Worker.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

// Configuración centralizada en appsettings.json (y user-secrets para datos sensibles)
builder.Services.Configure<EtlOptions>(builder.Configuration.GetSection("Etl"));
builder.Services.Configure<CsvOptions>(builder.Configuration.GetSection("Fuentes:Csv"));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Fuentes:BaseDatos"));
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Fuentes:Api"));
builder.Services.Configure<DimensionesOptions>(builder.Configuration.GetSection("Dimensiones"));

// Cliente HTTP configurado una sola vez (URL base, timeout y API key opcional)
builder.Services.AddHttpClient("ApiOpiniones", (sp, cliente) =>
{
    var api = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
    cliente.BaseAddress = new Uri(api.BaseUrl);
    cliente.Timeout = TimeSpan.FromSeconds(api.TimeoutSegundos);
    if (!string.IsNullOrWhiteSpace(api.ApiKey))
        cliente.DefaultRequestHeaders.Add("X-Api-Key", api.ApiKey);
});

// Cada fuente se registra contra la misma interfaz: agregar una fuente = una línea más
builder.Services.AddSingleton<IExtractor, CsvExtractor>();
builder.Services.AddSingleton<IExtractor, DatabaseExtractor>();
builder.Services.AddSingleton<IExtractor, ApiExtractor>();

builder.Services.AddSingleton<IStagingRepository, StagingRepository>();
builder.Services.AddSingleton<ExtractionService>();
// Carga de dimensiones del DataWarehouse: una dimensión nueva = una línea más
builder.Services.AddSingleton<ConexionesDw>();
builder.Services.AddSingleton<IDimensionAudit, DimensionAuditRepository>();
builder.Services.AddSingleton<IDimensionLoader, FuenteDimensionLoader>();
builder.Services.AddSingleton<IDimensionLoader, FechaDimensionLoader>();
builder.Services.AddSingleton<IDimensionLoader, ProductoDimensionLoader>();
builder.Services.AddSingleton<IDimensionLoader, ClienteDimensionLoader>();
builder.Services.AddSingleton<DimensionService>();

builder.Services.AddHostedService<Worker>();

builder.Build().Run();
