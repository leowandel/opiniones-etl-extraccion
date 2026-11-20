using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Application;
using OpinionesETL.Worker.Configuration;
using OpinionesETL.Worker.Domain;

namespace OpinionesETL.Worker.Infrastructure.Extractors;

/// <summary>Extrae las reseñas del sitio web desde una base de datos relacional (ADO.NET).</summary>
public sealed class DatabaseExtractor : IExtractor
{
    private readonly DatabaseOptions _opt;
    private readonly IConfiguration _config;

    public DatabaseExtractor(IOptions<DatabaseOptions> opt, IConfiguration config)
    {
        _opt = opt.Value;
        _config = config;
    }

    public string Nombre => "BD";
    public TipoFuente Fuente => TipoFuente.BaseDatos;
    public bool Habilitada => _opt.Habilitada;

    public async Task<IReadOnlyList<OpinionExtraida>> ExtraerAsync(CancellationToken ct)
    {
        // La cadena de conexión viene de appsettings.json / user-secrets, nunca del código
        var cadena = _config.GetConnectionString("ResenasWeb")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:ResenasWeb en la configuración.");

        var lista = new List<OpinionExtraida>();
        await using var cn = new SqlConnection(cadena);
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(_opt.Consulta, cn) { CommandTimeout = 60 };
        await using var rd = await cmd.ExecuteReaderAsync(ct);

        int iCliente = rd.GetOrdinal("Cliente"), iEmail = rd.GetOrdinal("Email"),
            iProducto = rd.GetOrdinal("Producto"), iComentario = rd.GetOrdinal("Comentario"),
            iCalificacion = rd.GetOrdinal("Calificacion"), iFecha = rd.GetOrdinal("Fecha");

        while (await rd.ReadAsync(ct))
        {
            lista.Add(new OpinionExtraida
            {
                Fuente = TipoFuente.BaseDatos,
                ClienteNombre = Texto(rd, iCliente),
                ClienteEmail = Texto(rd, iEmail),
                Producto = Texto(rd, iProducto),
                Comentario = Texto(rd, iComentario),
                Calificacion = rd.IsDBNull(iCalificacion) ? null : Convert.ToInt32(rd.GetValue(iCalificacion)),
                Fecha = rd.IsDBNull(iFecha) ? null : Convert.ToDateTime(rd.GetValue(iFecha)),
                Origen = _opt.Origen
            });
        }
        return lista;
    }

    private static string? Texto(SqlDataReader rd, int i) => rd.IsDBNull(i) ? null : rd.GetString(i);
}
