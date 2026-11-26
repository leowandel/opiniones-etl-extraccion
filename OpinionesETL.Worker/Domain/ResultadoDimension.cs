namespace OpinionesETL.Worker.Domain;

/// <summary>Resultado de cargar una dimensión del DataWarehouse.</summary>
public sealed record ResultadoDimension(
    string Dimension, int Leidos, int Insertados, int Existentes, int Rechazados,
    TimeSpan Duracion, bool Exitoso = true);
