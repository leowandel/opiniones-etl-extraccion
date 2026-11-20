namespace OpinionesETL.Worker.Domain;

/// <summary>Tipos de fuente que soporta el proceso de extracción.</summary>
public enum TipoFuente
{
    Csv,
    BaseDatos,
    Api
}

/// <summary>
/// Opinión tal como se extrae de cualquier fuente (sin limpiar ni clasificar).
/// La limpieza y validación se hacen después, en la etapa de transformación.
/// </summary>
public sealed class OpinionExtraida
{
    public TipoFuente Fuente { get; init; }
    public string? ClienteNombre { get; init; }
    public string? ClienteEmail { get; init; }
    public string? Producto { get; init; }
    public string? Comentario { get; init; }
    public int? Calificacion { get; init; }
    public DateTime? Fecha { get; init; }
    public string? Origen { get; init; }     // archivo, base de datos o endpoint
    public string? RedSocial { get; init; }  // solo para la API
}

/// <summary>Resultado de extraer y guardar una fuente.</summary>
public sealed record ResultadoFuente(
    string Fuente, int Extraidos, int Guardados, bool Exitoso, TimeSpan Duracion);
