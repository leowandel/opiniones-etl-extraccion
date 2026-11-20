# Worker Service ETL – Extracción (Actividad 1)

Sistema de Análisis de Opiniones de Clientes.
Worker Service en .NET 8 que **extrae** opiniones de tres fuentes y las guarda en las
tablas **staging** de la base de datos analítica.

| Fuente | Clase | Destino (staging) |
|---|---|---|
| Archivo CSV (encuestas) | `CsvExtractor` | `Staging_EncuestasCSV` |
| BD relacional (reseñas web) | `DatabaseExtractor` | `Staging_ResenasWeb` |
| API REST (redes sociales) | `ApiExtractor` | `Staging_RedesSociales` |

## Estructura
```
OpinionesETL.Worker/
├── Domain/           Modelos (OpinionExtraida, TipoFuente)
├── Application/      Interfaces (IExtractor, IStagingRepository) y ExtractionService
├── Configuration/    Clases de opciones (appsettings.json)
├── Infrastructure/
│   ├── Extractors/   CsvExtractor, DatabaseExtractor, ApiExtractor
│   └── Persistence/  StagingRepository (SqlBulkCopy)
├── Data/             encuestas.csv de ejemplo
├── Worker.cs         BackgroundService
└── Program.cs        Inyección de dependencias
sql/
└── crear_bd_resenas_web.sql   BD de origen de las reseñas
```

## Requisitos
- .NET SDK 8 o superior
- SQL Server (local)
- Haber creado `OpinionesAnaliticaDB` con `crear_modelo_estrella_opiniones.sql`

## Cómo ejecutarlo
1. Ejecutar en SSMS `sql/crear_bd_resenas_web.sql` (crea la BD de reseñas web).
2. Revisar las cadenas de conexión en `OpinionesETL.Worker/appsettings.json`.
3. Desde la carpeta `OpinionesETL.Worker`:
   ```
   dotnet run
   ```
4. Revisar los logs en consola y las tablas `Staging_*` y `ETL_Ejecuciones`.

## Configuración
Todo se controla en `appsettings.json`: cadenas de conexión, ruta del CSV, consulta SQL,
URL de la API y qué fuentes están habilitadas (`Habilitada`).
`Etl:IntervaloMinutos` = 0 ejecuta una sola vez; un valor mayor repite la extracción.

## Seguridad
Las conexiones usan autenticación de Windows (sin contraseñas en el código).
Si se necesitan contraseñas o API keys, no se escriben en el archivo; se guardan con:
```
dotnet user-secrets set "ConnectionStrings:Analitica" "<cadena con credenciales>"
dotnet user-secrets set "Fuentes:Api:ApiKey" "<clave>"
```

## Agregar una fuente nueva
1. Crear una clase que implemente `IExtractor`.
2. Registrarla en `Program.cs` con `AddSingleton<IExtractor, MiNuevaFuente>()`.
3. Agregar su sección en `appsettings.json`.
