using System.Diagnostics;
using OpinionesETL.Worker.Domain;

namespace OpinionesETL.Worker.Application;

/// <summary>
/// Orquesta la extracción: ejecuta todas las fuentes habilitadas en paralelo,
/// guarda cada resultado en staging y registra logs y tiempos.
/// </summary>
public sealed class ExtractionService
{
    private readonly IEnumerable<IExtractor> _extractores;
    private readonly IStagingRepository _staging;
    private readonly ILogger<ExtractionService> _log;

    public ExtractionService(IEnumerable<IExtractor> extractores,
        IStagingRepository staging, ILogger<ExtractionService> log)
    {
        _extractores = extractores;
        _staging = staging;
        _log = log;
    }

    public async Task<IReadOnlyList<ResultadoFuente>> EjecutarAsync(CancellationToken ct)
    {
        var total = Stopwatch.StartNew();
        var activos = _extractores.Where(e => e.Habilitada).ToList();
        _log.LogInformation("Iniciando extracción de {Cantidad} fuente(s) en paralelo", activos.Count);

        // Rendimiento: cada fuente corre en su propia tarea asíncrona
        var resultados = await Task.WhenAll(activos.Select(e => ProcesarAsync(e, ct)));

        foreach (var r in resultados)
            _log.LogInformation("Resumen {Fuente}: extraídos={Ext}, guardados={Gua}, estado={Estado}, {Ms} ms",
                r.Fuente, r.Extraidos, r.Guardados, r.Exitoso ? "Exitoso" : "Fallido",
                (long)r.Duracion.TotalMilliseconds);

        _log.LogInformation("Extracción terminada en {Ms} ms. Total extraído: {Total}",
            total.ElapsedMilliseconds, resultados.Sum(r => r.Extraidos));
        return resultados;
    }

    private async Task<ResultadoFuente> ProcesarAsync(IExtractor extractor, CancellationToken ct)
    {
        var reloj = Stopwatch.StartNew();
        int idEjecucion = 0, extraidos = 0;
        try
        {
            idEjecucion = await _staging.IniciarEjecucionAsync(extractor.Fuente, ct);
            _log.LogInformation("[{Fuente}] Extracción iniciada (ejecución #{Id})", extractor.Nombre, idEjecucion);

            var datos = await extractor.ExtraerAsync(ct);
            extraidos = datos.Count;
            _log.LogInformation("[{Fuente}] {Cantidad} registros extraídos", extractor.Nombre, extraidos);

            var guardados = await _staging.GuardarAsync(extractor.Fuente, datos, ct);
            await _staging.FinalizarEjecucionAsync(idEjecucion, extraidos, guardados,
                extraidos - guardados, "Exitoso", ct);

            _log.LogInformation("[{Fuente}] {Cantidad} registros guardados en staging", extractor.Nombre, guardados);
            return new ResultadoFuente(extractor.Nombre, extraidos, guardados, true, reloj.Elapsed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Una fuente que falla no detiene a las demás
            _log.LogError(ex, "[{Fuente}] Error durante la extracción", extractor.Nombre);
            await MarcarFallidaAsync(idEjecucion, extraidos);
            return new ResultadoFuente(extractor.Nombre, extraidos, 0, false, reloj.Elapsed);
        }
    }

    private async Task MarcarFallidaAsync(int idEjecucion, int extraidos)
    {
        if (idEjecucion == 0) return;
        try
        {
            await _staging.FinalizarEjecucionAsync(idEjecucion, extraidos, 0, extraidos,
                "Fallido", CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo registrar el estado Fallido de la ejecución #{Id}", idEjecucion);
        }
    }
}
