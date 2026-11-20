using OpinionesETL.Worker.Domain;

namespace OpinionesETL.Worker.Application;

/// <summary>Guarda lo extraído en las tablas staging y registra cada corrida.</summary>
public interface IStagingRepository
{
    Task<int> IniciarEjecucionAsync(TipoFuente fuente, CancellationToken ct);

    Task FinalizarEjecucionAsync(int idEjecucion, int extraidos, int cargados,
        int descartados, string estado, CancellationToken ct);

    Task<int> GuardarAsync(TipoFuente fuente, IReadOnlyList<OpinionExtraida> filas,
        CancellationToken ct);
}
