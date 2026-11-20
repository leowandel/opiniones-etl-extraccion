using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Application;
using OpinionesETL.Worker.Configuration;

namespace OpinionesETL.Worker;

/// <summary>
/// Servicio en segundo plano. Con IntervaloMinutos = 0 ejecuta la extracción una vez
/// y se cierra; con un valor mayor repite la extracción cada ese número de minutos.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ExtractionService _extraccion;
    private readonly EtlOptions _etl;
    private readonly IHostApplicationLifetime _vida;
    private readonly ILogger<Worker> _log;

    public Worker(ExtractionService extraccion, IOptions<EtlOptions> etl,
        IHostApplicationLifetime vida, ILogger<Worker> log)
    {
        _extraccion = extraccion;
        _etl = etl.Value;
        _vida = vida;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            if (_etl.IntervaloMinutos <= 0)
            {
                await _extraccion.EjecutarAsync(ct);
                _log.LogInformation("Ejecución única terminada. Cerrando el servicio.");
                _vida.StopApplication();
                return;
            }

            using var temporizador = new PeriodicTimer(TimeSpan.FromMinutes(_etl.IntervaloMinutos));
            do
            {
                await _extraccion.EjecutarAsync(ct);
            } while (await temporizador.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
            _log.LogInformation("Servicio detenido.");
        }
        catch (Exception ex)
        {
            _log.LogCritical(ex, "Error fatal en el Worker Service");
            _vida.StopApplication();
        }
    }
}
