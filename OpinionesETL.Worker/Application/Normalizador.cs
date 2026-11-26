using System.Globalization;
using System.Text.RegularExpressions;

namespace OpinionesETL.Worker.Application;

/// <summary>Reglas de limpieza y normalización usadas al cargar las dimensiones.</summary>
public static class Normalizador
{
    private static readonly Regex Especiales = new(@"[^\p{L}\p{N}\s.,;:!?¡¿'""()\-%/]", RegexOptions.Compiled);
    private static readonly Regex Espacios = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Email = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly TextInfo Texto = CultureInfo.GetCultureInfo("es-DO").TextInfo;

    public static string Limpiar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        return Espacios.Replace(Especiales.Replace(s, " "), " ").Trim();
    }

    public static string NormalizarNombre(string s) =>
        s.Length == 0 ? s : Texto.ToTitleCase(s.ToLowerInvariant());

    // Solo aplica mayúsculas iniciales si viene todo en minúsculas (conserva "WH-1000XM5")
    public static string NormalizarProducto(string s) =>
        s.Length > 0 && s == s.ToLowerInvariant() ? Texto.ToTitleCase(s) : s;

    public static bool EmailValido(string s) => Email.IsMatch(s);
}
