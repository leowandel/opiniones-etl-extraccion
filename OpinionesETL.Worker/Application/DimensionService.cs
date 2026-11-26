using System.Diagnostics;
using OpinionesETL.Worker.Domain;

namespace OpinionesETL.Worker.Application;

/// <summary>
/// Ejecuta la carga de todas las dimensiones en paralelo, registra cada una en la
/// tabla de auditoría y escribe logs con cantidades y tiempos.
/// </summary>
public sealed class DimensionService
{
    private readonly IEnumerable<IDimensionLoader> _cargadores;
    private readonly IDimensionAudit _auditoria;
    private readonly ILogger<DimensionService> _log;

    public DimensionService(IEnumerable<IDimensionLoader> cargadores,
        IDimensionAudit auditoria, ILogger<DimensionService> log)
    {
        _cargadores = cargadores;
        _auditoria = auditoria;
        _log = log;
    }

    public async Task<IReadOnlyList<ResultadoDimension>> EjecutarAsync(CancellationToken ct)
    {
        var total = Stopwatch.StartNew();
        await _auditoria.AsegurarTablaAsync(ct);

        var lista = _cargadores.ToList();
        _log.LogInformation("Iniciando carga de {Cantidad} dimensión(es) del DataWarehouse", lista.Count);

        var resultados = await Task.WhenAll(lista.Select(c => CargarAsync(c, ct)));

        foreach (var r in resultados)
            _log.LogInformation("Resumen {Dim}: leídos={L}, nuevos={N}, ya existían={E}, rechazados={R}, estado={S}, {Ms} ms",
                r.Dimension, r.Leidos, r.Insertados, r.Existentes, r.Rechazados,
                r.Exitoso ? "Exitoso" : "Fallido", (long)r.Duracion.TotalMilliseconds);

        _log.LogInformation("Carga de dimensiones terminada en {Ms} ms. Registros nuevos: {Total}",
            total.ElapsedMilliseconds, resultados.Sum(r => r.Insertados));
        return resultados;
    }

    private async Task<ResultadoDimension> CargarAsync(IDimensionLoader cargador, CancellationToken ct)
    {
        var inicio = DateTime.Now;
        var reloj = Stopwatch.StartNew();
        try
        {
            var r = await cargador.CargarAsync(ct);
            await _auditoria.RegistrarAsync(r, inicio, ct);
            return r;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Una dimensión que falla no detiene a las demás
            _log.LogError(ex, "[{Dim}] Error durante la carga", cargador.Dimension);
            var fallo = new ResultadoDimension(cargador.Dimension, 0, 0, 0, 0, reloj.Elapsed, false);
            try { await _auditoria.RegistrarAsync(fallo, inicio, CancellationToken.None); }
            catch (Exception ex2) { _log.LogError(ex2, "No se pudo registrar el fallo de {Dim}", cargador.Dimension); }
            return fallo;
        }
    }
}
