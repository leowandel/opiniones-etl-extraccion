/* =====================================================================
   Base de datos relacional de ORIGEN: reseñas publicadas en el sitio web.
   Es la fuente que consulta DatabaseExtractor.
   Es re-ejecutable: no duplica datos.
   ===================================================================== */

IF DB_ID('ResenasWebDB') IS NULL
    CREATE DATABASE ResenasWebDB;
GO

USE ResenasWebDB;
GO

IF OBJECT_ID('dbo.Resenas', 'U') IS NULL
CREATE TABLE dbo.Resenas (
    IdResena      INT IDENTITY(1,1) NOT NULL,
    NombreCliente VARCHAR(150)      NULL,
    EmailCliente  VARCHAR(150)      NULL,
    Producto      VARCHAR(150)      NOT NULL,
    Comentario    VARCHAR(MAX)      NOT NULL,
    Calificacion  INT               NOT NULL,
    Fecha         DATE              NOT NULL,
    CONSTRAINT PK_Resenas PRIMARY KEY (IdResena),
    CONSTRAINT CK_Resenas_Calificacion CHECK (Calificacion BETWEEN 1 AND 5)
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Resenas)
INSERT INTO dbo.Resenas (NombreCliente, EmailCliente, Producto, Comentario, Calificacion, Fecha)
VALUES
 (N'Maria Perez',     'maria.perez@mail.com', N'Laptop Dell XPS 13',          N'Excelente rendimiento y muy buena pantalla',        5, '2026-02-04'),
 (N'Kevin Lora',      'kevin.l@mail.com',     N'Laptop Dell XPS 13',          N'Se calienta bastante cuando trabaja mucho',         3, '2026-02-20'),
 (NULL,               NULL,                   N'Audífonos Sony WH-1000XM5',   N'La cancelación de ruido es increíble',              5, '2026-03-05'),
 (N'Daniela Ortiz',   'daniela.o@mail.com',   N'Audífonos Sony WH-1000XM5',   N'La batería dura menos de lo que dicen',             3, '2026-03-18'),
 (N'Luis Fernández',  'luis.f@mail.com',      N'Teléfono Samsung Galaxy S24', N'Muy rápido y la cámara es excelente',               5, '2026-04-02'),
 (N'Ramón Acosta',    'ramon.a@mail.com',     N'Teléfono Samsung Galaxy S24', N'Llegó con un defecto en la pantalla',               1, '2026-04-15'),
 (N'Sofía Núñez',     'sofia.n@mail.com',     N'Monitor LG 27 Pulgadas',      N'Buena imagen para el precio',                       4, '2026-05-06'),
 (N'Pedro Santana',   'pedro.s@mail.com',     N'Monitor LG 27 Pulgadas',      N'El soporte es débil y se mueve',                    2, '2026-05-22'),
 (N'Laura Peña',      'laura.p@mail.com',     N'Teclado Logitech MX Keys',    N'Muy cómodo, lo recomiendo',                         5, '2026-06-03'),
 (N'Carlos Mejía',    'carlos.m@mail.com',    N'Mouse Logitech MX Master 3',  N'Perfecto para trabajar todo el día',                5, '2026-06-19'),
 (NULL,               NULL,                   N'mouse logitech mx master 3',  N'El scroll falla después de unos meses',             2, '2026-07-07'),
 (N'Ana Martínez',    'ana.m@mail.com',       N'Teclado Logitech MX Keys',    N'Cumple su función, nada especial',                  3, '2026-07-21');
GO

SELECT COUNT(*) AS TotalResenas FROM dbo.Resenas;
GO
