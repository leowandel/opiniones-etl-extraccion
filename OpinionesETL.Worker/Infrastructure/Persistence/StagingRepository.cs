using System.Data;
using Microsoft.Data.SqlClient;
using OpinionesETL.Worker.Application;
using OpinionesETL.Worker.Domain;

namespace OpinionesETL.Worker.Infrastructure.Persistence;

/// <summary>
/// Inserta en las tablas staging con SqlBulkCopy (rápido para grandes volúmenes)
/// y registra cada corrida en ETL_Ejecuciones.
/// </summary>
public sealed class StagingRepository : IStagingRepository
{
    private readonly string _cadena;

    public StagingRepository(IConfiguration config)
    {
        _cadena = config.GetConnectionString("Analitica")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Analitica en la configuración.");
    }

    public async Task<int> IniciarEjecucionAsync(TipoFuente fuente, CancellationToken ct)
    {
        await using var cn = new SqlConnection(_cadena);
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "INSERT INTO dbo.ETL_Ejecuciones (FuenteProcesada) OUTPUT INSERTED.IdEjecucion VALUES (@f);", cn);
        cmd.Parameters.AddWithValue("@f", Codigo(fuente));
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task FinalizarEjecucionAsync(int idEjecucion, int extraidos, int cargados,
        int descartados, string estado, CancellationToken ct)
    {
        await using var cn = new SqlConnection(_cadena);
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(@"
            UPDATE dbo.ETL_Ejecuciones
               SET FechaFin = GETDATE(), RegistrosExtraidos = @e, RegistrosCargados = @c,
                   RegistrosDescartados = @d, Estado = @s
             WHERE IdEjecucion = @id;", cn);
        cmd.Parameters.AddWithValue("@e", extraidos);
        cmd.Parameters.AddWithValue("@c", cargados);
        cmd.Parameters.AddWithValue("@d", descartados);
        cmd.Parameters.AddWithValue("@s", estado);
        cmd.Parameters.AddWithValue("@id", idEjecucion);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> GuardarAsync(TipoFuente fuente, IReadOnlyList<OpinionExtraida> filas,
        CancellationToken ct)
    {
        var (tabla, datos) = Construir(fuente, filas);

        await using var cn = new SqlConnection(_cadena);
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);
        try
        {
            // Vuelve a ejecutar sin duplicar: limpia solo lo que aún no fue procesado
            await using (var borrar = new SqlCommand($"DELETE FROM dbo.{tabla} WHERE Procesado = 0;", cn, tx))
                await borrar.ExecuteNonQueryAsync(ct);

            if (datos.Rows.Count > 0)
            {
                using var bulk = new SqlBulkCopy(cn, SqlBulkCopyOptions.Default, tx)
                {
                    DestinationTableName = $"dbo.{tabla}",
                    BatchSize = 5000,
                    BulkCopyTimeout = 120
                };
                foreach (DataColumn c in datos.Columns)
                    bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
                await bulk.WriteToServerAsync(datos, ct);
            }

            await tx.CommitAsync(ct);
            return datos.Rows.Count;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static string Codigo(TipoFuente f) => f switch
    {
        TipoFuente.Csv => "CSV",
        TipoFuente.BaseDatos => "BD",
        _ => "API"
    };

    // Convierte las opiniones en un DataTable con las columnas de la tabla staging de cada fuente
    private static (string Tabla, DataTable Datos) Construir(TipoFuente fuente, IReadOnlyList<OpinionExtraida> filas)
    {
        var dt = new DataTable();
        string tabla;

        if (fuente == TipoFuente.Api)
        {
            tabla = "Staging_RedesSociales";
            foreach (var c in new[] { "UsuarioSocial", "RedSocial", "ProductoMencionado", "Comentario", "EndpointOrigen" })
                dt.Columns.Add(c, typeof(string));
            dt.Columns.Add("FechaOpinion", typeof(DateTime));

            foreach (var o in filas)
                dt.Rows.Add(V(Cortar(o.ClienteNombre, 150)), V(Cortar(o.RedSocial, 50)),
                    V(Cortar(o.Producto, 150)), V(o.Comentario), V(Cortar(o.Origen, 200)), V(o.Fecha));
            return (tabla, dt);
        }

        // CSV y BD comparten la misma forma; solo cambia la tabla y la columna de origen
        tabla = fuente == TipoFuente.Csv ? "Staging_EncuestasCSV" : "Staging_ResenasWeb";
        string colOrigen = fuente == TipoFuente.Csv ? "ArchivoOrigen" : "OrigenBD";
        foreach (var c in new[] { "ClienteNombre", "ClienteEmail", "ProductoNombre", "Comentario" })
            dt.Columns.Add(c, typeof(string));
        dt.Columns.Add("Calificacion", typeof(int));
        dt.Columns.Add("FechaOpinion", typeof(DateTime));
        dt.Columns.Add(colOrigen, typeof(string));

        foreach (var o in filas)
            dt.Rows.Add(V(Cortar(o.ClienteNombre, 150)), V(Cortar(o.ClienteEmail, 150)),
                V(Cortar(o.Producto, 150)), V(o.Comentario), V(o.Calificacion), V(o.Fecha),
                V(Cortar(o.Origen, 200)));
        return (tabla, dt);
    }

    private static object V(object? valor) => valor ?? DBNull.Value;

    private static string? Cortar(string? s, int max) =>
        s is { Length: > 0 } && s.Length > max ? s[..max] : s;
}
