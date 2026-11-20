using OpinionesETL.Worker.Domain;

namespace OpinionesETL.Worker.Application;

/// <summary>
/// Contrato común de todas las fuentes. Para agregar una fuente nueva
/// basta con crear otra clase que implemente esta interfaz.
/// </summary>
public interface IExtractor
{
    string Nombre { get; }
    TipoFuente Fuente { get; }
    bool Habilitada { get; }
    Task<IReadOnlyList<OpinionExtraida>> ExtraerAsync(CancellationToken ct);
}
