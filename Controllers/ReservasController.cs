using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Salas_Elvis.Models;
using Salas_Elvis.Contracts;
using Salas_Elvis.Services;

namespace Salas_Elvis.Controllers;

[ApiController]
[Authorize]
[Route("reservas")]
public class ReservasController(ReservaService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReservaResponse>>> Listar(CancellationToken cancellationToken)
        => Ok(await service.ListarAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(ReservaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponse>> Criar(CriarReservaRequest request, CancellationToken cancellationToken)
    {
        var reserva = await service.CriarAsync(request, User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, reserva);
    }

    [HttpPost("{id}/cancelar")]
    [Authorize(Roles = Perfis.Admin)]
    public async Task<ActionResult<ReservaResponse>> Cancelar(string id, CancelarReservaRequest request, CancellationToken ct)
        => Ok(await service.CancelarAsync(id, request.Motivo, User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct));
}
