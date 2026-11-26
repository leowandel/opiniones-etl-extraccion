using OpinionesETL.Worker.Domain;

namespace OpinionesETL.Worker.Application;

/// <summary>Registro de auditoría de la carga de dimensiones.</summary>
public interface IDimensionAudit
{
    Task AsegurarTablaAsync(CancellationToken ct);
    Task RegistrarAsync(ResultadoDimension resultado, DateTime inicio, CancellationToken ct);
}
