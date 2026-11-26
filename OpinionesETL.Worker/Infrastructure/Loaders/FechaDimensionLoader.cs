using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Application;
using OpinionesETL.Worker.Configuration;
using OpinionesETL.Worker.Domain;
using OpinionesETL.Worker.Infrastructure.Persistence;

namespace OpinionesETL.Worker.Infrastructure.Loaders;

/// <summary>Genera la dimensión Fechas (una fila por día) para el rango configurado.</summary>
public sealed class FechaDimensionLoader : IDimensionLoader
{
    private readonly ConexionesDw _db;
    private readonly DimensionesOptions _opt;
    private readonly ILogger<FechaDimensionLoader> _log;

    public FechaDimensionLoader(ConexionesDw db, IOptions<DimensionesOptions> opt, ILogger<FechaDimensionLoader> log)
    {
        _db = db;
        _opt = opt.Value;
        _log = log;
    }

    public string Dimension => "Fechas";

    public async Task<ResultadoDimension> CargarAsync(CancellationToken ct)
    {
        var reloj = Stopwatch.StartNew();
        var inicio = _opt.FechaInicio.Date;
        var fin = _opt.FechaFin.Date;
        if (fin < inicio)
            throw new InvalidOperationException("Dimensiones:FechaFin no puede ser menor que Dimensiones:FechaInicio.");

        int dias = (fin - inicio).Days + 1;

        await using var dw = await _db.AbrirDwAsync(ct);
        await using var tx = (SqlTransaction)await dw.BeginTransactionAsync(ct);
        int nuevos;
        try
        {
            // Una sola instrucción genera todos los días que faltan (rendimiento)
            nuevos = await ConexionesDw.EjecutarAsync(dw, tx, @"
                ;WITH Dias AS (
                    SELECT @ini AS Fecha
                    UNION ALL
                    SELECT DATEADD(DAY, 1, Fecha) FROM Dias WHERE Fecha < @fin
                )
                INSERT INTO dbo.Fechas (Fecha, Anio, Trimestre, Mes, NombreMes, Dia, DiaSemana)
                SELECT d.Fecha, YEAR(d.Fecha), DATEPART(QUARTER, d.Fecha), MONTH(d.Fecha),
                       DATENAME(MONTH, d.Fecha), DAY(d.Fecha), DATENAME(WEEKDAY, d.Fecha)
                  FROM Dias d
                 WHERE NOT EXISTS (SELECT 1 FROM dbo.Fechas f WHERE f.Fecha = d.Fecha)
                OPTION (MAXRECURSION 0);", ct,
                new SqlParameter("@ini", SqlDbType.Date) { Value = inicio },
                new SqlParameter("@fin", SqlDbType.Date) { Value = fin });
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }

        _log.LogInformation("[Fechas] rango {Ini:yyyy-MM-dd} a {Fin:yyyy-MM-dd}: días={D}, nuevos={N}, ya existían={E}",
            inicio, fin, dias, nuevos, dias - nuevos);
        return new ResultadoDimension(Dimension, dias, nuevos, dias - nuevos, 0, reloj.Elapsed);
    }
}
