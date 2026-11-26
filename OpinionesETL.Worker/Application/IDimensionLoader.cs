using OpinionesETL.Worker.Domain;

namespace OpinionesETL.Worker.Application;

/// <summary>Contrato de carga de una dimensión. Una dimensión nueva = una clase nueva.</summary>
public interface IDimensionLoader
{
    string Dimension { get; }
    Task<ResultadoDimension> CargarAsync(CancellationToken ct);
}
