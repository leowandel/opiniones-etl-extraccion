using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using OpinionesETL.Worker.Application;
using OpinionesETL.Worker.Domain;
using OpinionesETL.Worker.Infrastructure.Persistence;

namespace OpinionesETL.Worker.Infrastructure.Loaders;

/// <summary>
/// Carga la dimensión Productos desde el staging de encuestas CSV y reseñas web:
/// limpia, normaliza, elimina repetidos y agrega solo los que no existen.
/// </summary>
public sealed class ProductoDimensionLoader : IDimensionLoader
{
    private readonly ConexionesDw _db;
    private readonly ILogger<ProductoDimensionLoader> _log;

    public ProductoDimensionLoader(ConexionesDw db, ILogger<ProductoDimensionLoader> log)
    {
        _db = db;
        _log = log;
    }

    public string Dimension => "Productos";

    public async Task<ResultadoDimension> CargarAsync(CancellationToken ct)
    {
        var reloj = Stopwatch.StartNew();
        var unicos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int leidos = 0, rechazados = 0;

        // 1) EXTRAER del staging
        await using (var origen = await _db.AbrirStagingAsync(ct))
        await using (var cmd = new SqlCommand(@"
            SELECT ProductoNombre FROM dbo.Staging_EncuestasCSV
            UNION ALL
            SELECT ProductoNombre FROM dbo.Staging_ResenasWeb;", origen))
        await using (var rd = await cmd.ExecuteReaderAsync(ct))
        {
            while (await rd.ReadAsync(ct))
            {
                leidos++;
                // 2) TRANSFORMAR y VALIDAR
                var nombre = Normalizador.NormalizarProducto(Normalizador.Limpiar(rd.IsDBNull(0) ? null : rd.GetString(0)));
                if (nombre.Length == 0 || nombre.Length > 150) { rechazados++; continue; }
                unicos.Add(nombre);
            }
        }

        // 3) CARGAR al DataWarehouse (solo los que no existen)
        int nuevos;
        await using var dw = await _db.AbrirDwAsync(ct);
        await using var tx = (SqlTransaction)await dw.BeginTransactionAsync(ct);
        try
        {
            await ConexionesDw.EjecutarAsync(dw, tx,
                "CREATE TABLE #Productos (Nombre VARCHAR(150) COLLATE DATABASE_DEFAULT NOT NULL);", ct);

            var tabla = new DataTable();
            tabla.Columns.Add("Nombre", typeof(string));
            foreach (var n in unicos) tabla.Rows.Add(n);

            using (var bulk = new SqlBulkCopy(dw, SqlBulkCopyOptions.Default, tx) { DestinationTableName = "#Productos" })
            {
                bulk.ColumnMappings.Add("Nombre", "Nombre");
                await bulk.WriteToServerAsync(tabla, ct);
            }

            nuevos = await ConexionesDw.EjecutarAsync(dw, tx, @"
                INSERT INTO dbo.Productos (Nombre)
                SELECT t.Nombre FROM #Productos t
                 WHERE NOT EXISTS (SELECT 1 FROM dbo.Productos p WHERE p.Nombre = t.Nombre);", ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }

        int repetidos = leidos - rechazados - unicos.Count;
        _log.LogInformation("[Productos] leídos={L}, rechazados={R}, repetidos en origen={Rep}, nuevos={N}, ya existían={E}",
            leidos, rechazados, repetidos, nuevos, unicos.Count - nuevos);
        return new ResultadoDimension(Dimension, leidos, nuevos, unicos.Count - nuevos, rechazados, reloj.Elapsed);
    }
}
