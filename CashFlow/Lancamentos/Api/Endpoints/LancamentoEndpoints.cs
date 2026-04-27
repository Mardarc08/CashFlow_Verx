using FluentValidation;
using Lancamentos.Application.Lancamentos.ListarLancamentosPorData;
using Lancamentos.Application.Lancamentos.RegistrarLancamento;
using MediatR;

namespace Lancamentos.Api.Endpoints
{
    public static class LancamentoEndpoints
    {
        public static void MapLancamentoEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/lancamentos")
                .WithTags("Lançamentos");

            // POST /api/lancamentos
            group.MapPost("/", async (RegistrarLancamentoCommand command, IMediator mediator, CancellationToken ct) =>
            {
                try
                {
                    var response = await mediator.Send(command, ct);
                    return Results.Created($"/api/lancamentos/{response.Id}", response);
                }
                catch (ValidationException ex)
                {
                    var errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage });
                    return Results.BadRequest(new { Message = "Dados inválidos.", Errors = errors });
                }
            })
            .WithName("RegistrarLancamento")
            .WithSummary("Registra um novo lançamento (débito ou crédito)")
            .Produces<RegistrarLancamentoResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

            // GET /api/lancamentos?data=2026-04-20
            group.MapGet("/", async (DateOnly data, IMediator mediator, CancellationToken ct) =>
            {
                var query = new ListarLancamentosPorDataQuery(data);
                var result = await mediator.Send(query, ct);
                return Results.Ok(result);
            })
            .WithName("ListarLancamentos")
            .WithSummary("Lista lançamentos por data")
            .Produces<IEnumerable<LancamentoDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
        }
    }
}
