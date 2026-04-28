using Consolidado.Application.UseCases.ObterConsolidado;
using MediatR;

namespace Consolidado.Api.Endpoints
{
    public static class ConsolidadoEndpoints
    {
        public static void MapConsolidadoEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/consolidado")
                .RequireAuthorization()
                .WithTags("Consolidado Diário");

            // GET /api/consolidado/{data}
            group.MapGet("/{data}", async (DateOnly data, IMediator mediator, CancellationToken ct) =>
            {
                var query = new ObterConsolidadoQuery(data);
                var result = await mediator.Send(query, ct);

                return result is null
                    ? Results.NotFound(new { Message = $"Nenhum consolidado encontrado para {data:yyyy-MM-dd}." })
                    : Results.Ok(result);
            })
            .WithName("ObterConsolidado")
            .WithSummary("Retorna o saldo consolidado de um dia específico")
            .Produces<ConsolidadoDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);
        }
    }
}
