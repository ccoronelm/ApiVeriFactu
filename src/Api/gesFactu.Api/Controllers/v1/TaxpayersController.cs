using gesFactu.Application.Configuracion.Queries.ObtenerObligados;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace gesFactu.Api.Controllers.v1;

[ApiController]
[Route("api/v1/taxpayers")]
[Produces("application/json")]
public sealed class TaxpayersController : ControllerBase
{
    private readonly IMediator _mediator;

    public TaxpayersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista identidades públicas de los obligados habilitados en la instalación.
    /// Nunca expone thumbprints, rutas, contraseñas ni certificados.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new GetConfiguredTaxpayersQuery(),
            cancellationToken));
}
