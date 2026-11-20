namespace OpinionesETL.Worker.Configuration;

public sealed class EtlOptions
{
    /// <summary>0 = ejecutar una sola vez y cerrar. Mayor que 0 = repetir cada N minutos.</summary>
    public int IntervaloMinutos { get; set; }
}

public sealed class CsvOptions
{
    public bool Habilitada { get; set; } = true;
    public string Ruta { get; set; } = "Data/encuestas.csv";
}

public sealed class DatabaseOptions
{
    public bool Habilitada { get; set; } = true;
    public string Origen { get; set; } = "ResenasWebDB.dbo.Resenas";
    public string Consulta { get; set; } = "";
}

public sealed class ApiOptions
{
    public bool Habilitada { get; set; } = true;
    public string BaseUrl { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string RedSocial { get; set; } = "API";
    public string ApiKey { get; set; } = "";
    public int MaxRegistros { get; set; }          // 0 = sin límite
    public int TimeoutSegundos { get; set; } = 30;
}
