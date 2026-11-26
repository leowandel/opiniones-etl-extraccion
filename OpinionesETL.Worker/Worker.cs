using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Application;
using OpinionesETL.Worker.Configuration;

namespace OpinionesETL.Worker;

/// <summary>
/// Servicio en segundo plano. Según Etl:Modo ejecuta la extracción, la carga de
/// dimensiones o ambas. Con IntervaloMinutos = 0 corre una vez y se cierra.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ExtractionService _extraccion;
    private readonly DimensionService _dimensiones;
    private readonly EtlOptions _etl;
    private readonly IHostApplicationLifetime _vida;
    private readonly ILogger<Worker> _log;

    public Worker(ExtractionService extraccion, DimensionService dimensiones, IOptions<EtlOptions> etl,
        IHostApplicationLifetime vida, ILogger<Worker> log)
    {
        _extraccion = extraccion;
        _dimensiones = dimensiones;
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
                await EjecutarCicloAsync(ct);
                _log.LogInformation("Ejecución única terminada. Cerrando el servicio.");
                _vida.StopApplication();
                return;
            }

            using var temporizador = new PeriodicTimer(TimeSpan.FromMinutes(_etl.IntervaloMinutos));
            do
            {
                await EjecutarCicloAsync(ct);
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

    private async Task EjecutarCicloAsync(CancellationToken ct)
    {
        bool Es(string modo) => string.Equals(_etl.Modo, modo, StringComparison.OrdinalIgnoreCase);

        _log.LogInformation("Modo de ejecución: {Modo}", _etl.Modo);
        if (Es("Extraccion") || Es("Todo")) await _extraccion.EjecutarAsync(ct);
        if (Es("Dimensiones") || Es("Todo")) await _dimensiones.EjecutarAsync(ct);
    }
}
