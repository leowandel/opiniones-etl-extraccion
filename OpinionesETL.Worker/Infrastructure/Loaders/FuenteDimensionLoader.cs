using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Application;
using OpinionesETL.Worker.Configuration;
using OpinionesETL.Worker.Domain;
using OpinionesETL.Worker.Infrastructure.Persistence;

namespace OpinionesETL.Worker.Infrastructure.Loaders;

/// <summary>Carga la dimensión FuenteDatos (catálogo de canales) desde la configuración.</summary>
public sealed class FuenteDimensionLoader : IDimensionLoader
{
    private readonly ConexionesDw _db;
    private readonly DimensionesOptions _opt;
    private readonly ILogger<FuenteDimensionLoader> _log;

    public FuenteDimensionLoader(ConexionesDw db, IOptions<DimensionesOptions> opt, ILogger<FuenteDimensionLoader> log)
    {
        _db = db;
        _opt = opt.Value;
        _log = log;
    }

    public string Dimension => "FuenteDatos";

    public async Task<ResultadoDimension> CargarAsync(CancellationToken ct)
    {
        var reloj = Stopwatch.StartNew();
        var fuentes = _opt.Fuentes.Select(Normalizador.Limpiar).Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        int nuevos = 0;
        await using var dw = await _db.AbrirDwAsync(ct);
        await using var tx = (SqlTransaction)await dw.BeginTransactionAsync(ct);
        try
        {
            foreach (var fuente in fuentes)
                nuevos += await ConexionesDw.EjecutarAsync(dw, tx, @"
                    INSERT INTO dbo.FuenteDatos (TipoFuente)
                    SELECT @f WHERE NOT EXISTS (SELECT 1 FROM dbo.FuenteDatos WHERE TipoFuente = @f);", ct,
                    new SqlParameter("@f", SqlDbType.VarChar, 50) { Value = fuente });
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }

        _log.LogInformation("[FuenteDatos] leídas={L}, nuevas={N}, ya existían={E}", fuentes.Count, nuevos, fuentes.Count - nuevos);
        return new ResultadoDimension(Dimension, fuentes.Count, nuevos, fuentes.Count - nuevos, 0, reloj.Elapsed);
    }
}
