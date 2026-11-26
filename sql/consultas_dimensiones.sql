USE OpinionesAnaliticaDB;
GO

/* 1. Auditoría de la carga (una fila por dimensión y por ejecución) */
SELECT * FROM dbo.DW_CargaDimensiones ORDER BY IdCarga;

/* 2. Cantidad de registros en cada dimensión */
SELECT 'Clientes'    AS Dimension, COUNT(*) AS Registros FROM dbo.Clientes
UNION ALL SELECT 'Productos',   COUNT(*) FROM dbo.Productos
UNION ALL SELECT 'FuenteDatos', COUNT(*) FROM dbo.FuenteDatos
UNION ALL SELECT 'Fechas',      COUNT(*) FROM dbo.Fechas;

/* 3. Sin duplicados (deben devolver 0 filas) */
SELECT Email, COUNT(*) AS Veces FROM dbo.Clientes WHERE Email IS NOT NULL GROUP BY Email HAVING COUNT(*) > 1;
SELECT Nombre, COUNT(*) AS Veces FROM dbo.Productos GROUP BY Nombre HAVING COUNT(*) > 1;

/* 4. Contenido de las dimensiones cargadas */
SELECT * FROM dbo.Clientes ORDER BY IdCliente;
SELECT * FROM dbo.Productos ORDER BY IdProducto;
SELECT * FROM dbo.FuenteDatos ORDER BY IdFuente;

/* 5. Rango de la dimensión Fechas */
SELECT MIN(Fecha) AS Desde, MAX(Fecha) AS Hasta, COUNT(*) AS Dias FROM dbo.Fechas;
SELECT TOP 10 * FROM dbo.Fechas ORDER BY Fecha DESC;
