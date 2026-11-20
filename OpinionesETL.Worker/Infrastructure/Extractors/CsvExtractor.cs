using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Application;
using OpinionesETL.Worker.Configuration;
using OpinionesETL.Worker.Domain;

namespace OpinionesETL.Worker.Infrastructure.Extractors;

/// <summary>Extrae las encuestas internas desde un archivo CSV usando CsvHelper.</summary>
public sealed class CsvExtractor : IExtractor
{
    private static readonly string[] Formatos = { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy" };
    private static readonly string[] Requeridas =
        { "ClienteNombre", "ClienteEmail", "ProductoNombre", "Comentario", "Calificacion", "FechaOpinion" };

    private readonly CsvOptions _opt;
    private readonly ILogger<CsvExtractor> _log;

    public CsvExtractor(IOptions<CsvOptions> opt, ILogger<CsvExtractor> log)
    {
        _opt = opt.Value;
        _log = log;
    }

    public string Nombre => "CSV";
    public TipoFuente Fuente => TipoFuente.Csv;
    public bool Habilitada => _opt.Habilitada;

    public async Task<IReadOnlyList<OpinionExtraida>> ExtraerAsync(CancellationToken ct)
    {
        var ruta = Path.IsPathRooted(_opt.Ruta) ? _opt.Ruta : Path.Combine(AppContext.BaseDirectory, _opt.Ruta);
        if (!File.Exists(ruta))
            throw new FileNotFoundException($"No se encontró el archivo CSV: {ruta}", ruta);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = a => a.Header.Trim().ToLowerInvariant()
        };

        using var reader = new StreamReader(ruta, Encoding.UTF8, true);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();
        var faltantes = Requeridas.Where(c => csv.HeaderRecord?.Any(h =>
            h.Trim().Equals(c, StringComparison.OrdinalIgnoreCase)) != true).ToList();
        if (faltantes.Count > 0)
            throw new InvalidDataException("Faltan columnas en el CSV: " + string.Join(", ", faltantes));

        string? Campo(string nombre) => csv.TryGetField<string>(nombre, out var v) ? v : null;

        var lista = new List<OpinionExtraida>();
        int sinInterpretar = 0;
        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            int? calificacion = int.TryParse(Campo("Calificacion"), out var c) ? c : null;
            DateTime? fecha = DateTime.TryParseExact(Campo("FechaOpinion"), Formatos,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;
            if (calificacion is null || fecha is null) sinInterpretar++;

            lista.Add(new OpinionExtraida
            {
                Fuente = TipoFuente.Csv,
                ClienteNombre = Campo("ClienteNombre"),
                ClienteEmail = Campo("ClienteEmail"),
                Producto = Campo("ProductoNombre"),
                Comentario = Campo("Comentario"),
                Calificacion = calificacion,
                Fecha = fecha,
                Origen = Path.GetFileName(ruta)
            });
        }

        if (sinInterpretar > 0)
            _log.LogWarning("[CSV] {Cantidad} fila(s) con calificación o fecha no interpretable; se guardan en NULL", sinInterpretar);
        return lista;
    }
}
