using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using OpinionesETL.Worker.Application;
using OpinionesETL.Worker.Domain;
using OpinionesETL.Worker.Infrastructure.Persistence;

namespace OpinionesETL.Worker.Infrastructure.Loaders;

/// <summary>
/// Carga la dimensión Clientes desde el staging de encuestas CSV y reseñas web:
/// valida nombre y email, normaliza, elimina repetidos y agrega solo los clientes nuevos.
/// </summary>
public sealed class ClienteDimensionLoader : IDimensionLoader
{
    private readonly ConexionesDw _db;
    private readonly ILogger<ClienteDimensionLoader> _log;

    public ClienteDimensionLoader(ConexionesDw db, ILogger<ClienteDimensionLoader> log)
    {
        _db = db;
        _log = log;
    }

    public string Dimension => "Clientes";

    public async Task<ResultadoDimension> CargarAsync(CancellationToken ct)
    {
        var reloj = Stopwatch.StartNew();
        // Identidad del cliente: el email si existe; si no, el nombre
        var unicos = new Dictionary<string, (string Nombre, string? Email)>();
        int leidos = 0, rechazados = 0;

        // 1) EXTRAER del staging
        await using (var origen = await _db.AbrirStagingAsync(ct))
        await using (var cmd = new SqlCommand(@"
            SELECT ClienteNombre, ClienteEmail FROM dbo.Staging_EncuestasCSV
            UNION ALL
            SELECT ClienteNombre, ClienteEmail FROM dbo.Staging_ResenasWeb;", origen))
        await using (var rd = await cmd.ExecuteReaderAsync(ct))
        {
            while (await rd.ReadAsync(ct))
            {
                leidos++;
                // 2) TRANSFORMAR y VALIDAR
                var nombre = Normalizador.NormalizarNombre(Normalizador.Limpiar(rd.IsDBNull(0) ? null : rd.GetString(0)));
                var email = rd.IsDBNull(1) ? null : rd.GetString(1).Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(email)) email = null;

                bool valido = nombre.Length > 0 && nombre.Length <= 150
                    && (email is null || (email.Length <= 150 && Normalizador.EmailValido(email)));
                if (!valido) { rechazados++; continue; }

                unicos.TryAdd(email ?? "n:" + nombre.ToLowerInvariant(), (nombre, email));
            }
        }

        // 3) CARGAR al DataWarehouse (solo los clientes que no existen)
        int nuevos;
        await using var dw = await _db.AbrirDwAsync(ct);
        await using var tx = (SqlTransaction)await dw.BeginTransactionAsync(ct);
        try
        {
            await ConexionesDw.EjecutarAsync(dw, tx, @"
                CREATE TABLE #Clientes (
                    Nombre VARCHAR(150) COLLATE DATABASE_DEFAULT NOT NULL,
                    Email  VARCHAR(150) COLLATE DATABASE_DEFAULT NULL);", ct);

            var tabla = new DataTable();
            tabla.Columns.Add("Nombre", typeof(string));
            tabla.Columns.Add("Email", typeof(string));
            foreach (var c in unicos.Values)
                tabla.Rows.Add(c.Nombre, (object?)c.Email ?? DBNull.Value);

            using (var bulk = new SqlBulkCopy(dw, SqlBulkCopyOptions.Default, tx) { DestinationTableName = "#Clientes" })
            {
                bulk.ColumnMappings.Add("Nombre", "Nombre");
                bulk.ColumnMappings.Add("Email", "Email");
                await bulk.WriteToServerAsync(tabla, ct);
            }

            nuevos = await ConexionesDw.EjecutarAsync(dw, tx, @"
                INSERT INTO dbo.Clientes (Nombre, Email)
                SELECT t.Nombre, t.Email FROM #Clientes t
                 WHERE NOT EXISTS (
                        SELECT 1 FROM dbo.Clientes c
                         WHERE (t.Email IS NOT NULL AND c.Email = t.Email)
                            OR (t.Email IS NULL AND c.Email IS NULL AND c.Nombre = t.Nombre));", ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }

        int repetidos = leidos - rechazados - unicos.Count;
        _log.LogInformation("[Clientes] leídos={L}, rechazados={R}, repetidos en origen={Rep}, nuevos={N}, ya existían={E}",
            leidos, rechazados, repetidos, nuevos, unicos.Count - nuevos);
        return new ResultadoDimension(Dimension, leidos, nuevos, unicos.Count - nuevos, rechazados, reloj.Elapsed);
    }
}
