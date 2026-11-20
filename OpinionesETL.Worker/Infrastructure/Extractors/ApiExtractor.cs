using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Application;
using OpinionesETL.Worker.Configuration;
using OpinionesETL.Worker.Domain;

namespace OpinionesETL.Worker.Infrastructure.Extractors;

/// <summary>Consume los comentarios de redes sociales desde una API REST (IHttpClientFactory).</summary>
public sealed class ApiExtractor : IExtractor
{
    // Forma del JSON que devuelve la API de demostración
    private sealed record ComentarioDto(int PostId, int Id, string? Name, string? Email, string? Body);

    private readonly IHttpClientFactory _http;
    private readonly ApiOptions _opt;

    public ApiExtractor(IHttpClientFactory http, IOptions<ApiOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
    }

    public string Nombre => "API";
    public TipoFuente Fuente => TipoFuente.Api;
    public bool Habilitada => _opt.Habilitada;

    public async Task<IReadOnlyList<OpinionExtraida>> ExtraerAsync(CancellationToken ct)
    {
        // El cliente HTTP (URL base, timeout y API key) se configura una sola vez en Program.cs
        var cliente = _http.CreateClient("ApiOpiniones");
        var datos = await cliente.GetFromJsonAsync<List<ComentarioDto>>(_opt.Endpoint, ct)
                    ?? new List<ComentarioDto>();

        var origen = new Uri(cliente.BaseAddress!, _opt.Endpoint).ToString();
        var consulta = _opt.MaxRegistros > 0 ? datos.Take(_opt.MaxRegistros) : datos;

        return consulta.Select(d => new OpinionExtraida
        {
            Fuente = TipoFuente.Api,
            ClienteNombre = d.Email,   // usuario que comenta
            Producto = d.Name,         // producto o publicación mencionada
            Comentario = d.Body,
            Fecha = DateTime.Today,    // la API de demostración no trae fecha
            Origen = origen,
            RedSocial = _opt.RedSocial
        }).ToList();
    }
}
