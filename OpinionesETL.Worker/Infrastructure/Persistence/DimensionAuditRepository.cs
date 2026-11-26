using Microsoft.Data.SqlClient;
using OpinionesETL.Worker.Application;
using OpinionesETL.Worker.Domain;

namespace OpinionesETL.Worker.Infrastructure.Persistence;

/// <summary>Registra en DW_CargaDimensiones cada carga de dimensión (trazabilidad).</summary>
public sealed class DimensionAuditRepository : IDimensionAudit
{
    private readonly ConexionesDw _db;

    public DimensionAuditRepository(ConexionesDw db) => _db = db;

    public async Task AsegurarTablaAsync(CancellationToken ct)
    {
        await using var cn = await _db.AbrirDwAsync(ct);
        await ConexionesDw.EjecutarAsync(cn, null, @"
            IF OBJECT_ID('dbo.DW_CargaDimensiones', 'U') IS NULL
            CREATE TABLE dbo.DW_CargaDimensiones (
                IdCarga             INT IDENTITY(1,1) NOT NULL,
                Dimension           VARCHAR(50)       NOT NULL,
                FechaInicio         DATETIME          NOT NULL,
                FechaFin            DATETIME          NOT NULL,
                RegistrosLeidos     INT               NOT NULL,
                RegistrosInsertados INT               NOT NULL,
                RegistrosExistentes INT               NOT NULL,
                RegistrosRechazados INT               NOT NULL,
                Estado              VARCHAR(20)       NOT NULL,
                CONSTRAINT PK_DW_CargaDimensiones PRIMARY KEY (IdCarga),
                CONSTRAINT CK_DW_Carga_Estado CHECK (Estado IN ('Exitoso','Fallido'))
            );", ct);
    }

    public async Task RegistrarAsync(ResultadoDimension r, DateTime inicio, CancellationToken ct)
    {
        await using var cn = await _db.AbrirDwAsync(ct);
        await ConexionesDw.EjecutarAsync(cn, null, @"
            INSERT INTO dbo.DW_CargaDimensiones
                (Dimension, FechaInicio, FechaFin, RegistrosLeidos, RegistrosInsertados,
                 RegistrosExistentes, RegistrosRechazados, Estado)
            VALUES (@d, @i, GETDATE(), @l, @n, @e, @r, @s);", ct,
            new SqlParameter("@d", r.Dimension),
            new SqlParameter("@i", inicio),
            new SqlParameter("@l", r.Leidos),
            new SqlParameter("@n", r.Insertados),
            new SqlParameter("@e", r.Existentes),
            new SqlParameter("@r", r.Rechazados),
            new SqlParameter("@s", r.Exitoso ? "Exitoso" : "Fallido"));
    }
}
