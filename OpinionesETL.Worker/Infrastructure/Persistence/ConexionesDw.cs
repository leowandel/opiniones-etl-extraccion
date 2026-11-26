using Microsoft.Data.SqlClient;

namespace OpinionesETL.Worker.Infrastructure.Persistence;

/// <summary>
/// Abre las conexiones del proceso: la del staging (origen) y la del DataWarehouse (destino).
/// Si no se define "DataWarehouse", se usa la misma base que el staging.
/// </summary>
public sealed class ConexionesDw
{
    private readonly string _staging;
    private readonly string _dw;

    public ConexionesDw(IConfiguration config)
    {
        _staging = config.GetConnectionString("Analitica")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Analitica en la configuración.");
        _dw = config.GetConnectionString("DataWarehouse") ?? _staging;
    }

    public async Task<SqlConnection> AbrirStagingAsync(CancellationToken ct)
    {
        var cn = new SqlConnection(_staging);
        await cn.OpenAsync(ct);
        return cn;
    }

    public async Task<SqlConnection> AbrirDwAsync(CancellationToken ct)
    {
        var cn = new SqlConnection(_dw);
        await cn.OpenAsync(ct);
        return cn;
    }

    public static async Task<int> EjecutarAsync(SqlConnection cn, SqlTransaction? tx, string sql,
        CancellationToken ct, params SqlParameter[] parametros)
    {
        await using var cmd = new SqlCommand(sql, cn, tx) { CommandTimeout = 120 };
        cmd.Parameters.AddRange(parametros);
        return await cmd.ExecuteNonQueryAsync(ct);
    }
}
