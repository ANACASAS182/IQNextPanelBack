using IQDTOs;
using IQServices.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Iqnext.Api.Controllers;

[ApiController]
[Route("/")]
public sealed class ProcesosController : ControllerBase
{
    private readonly IProcesosService _svc;
    public ProcesosController(IProcesosService svc) => _svc = svc;

    [HttpGet("GetEmpresas")]
    public async Task<ActionResult<IEnumerable<EmpresaDto>>> GetEmpresas(CancellationToken ct)
        => Ok(await _svc.GetEmpresasAsync(ct));

    [HttpGet("GetProcesoEmpresas/{empresaId:int}")]
    public async Task<ActionResult<IEnumerable<ProcesoDto>>> GetProcesos(int empresaId, CancellationToken ct)
        => Ok(await _svc.GetProcesosByEmpresaAsync(empresaId, ct));

    [HttpGet("GetEjecucionesProcesos/{procesoId:int}")]
    public async Task<ActionResult<IEnumerable<EjecucionDto>>> GetEjecuciones(int procesoId, CancellationToken ct)
        => Ok(await _svc.GetEjecucionesByProcesoAsync(procesoId, ct));

    [HttpPost("RegistrarEjecucion")]
    public async Task<ActionResult<EjecucionDto>> Registrar([FromBody] RegistrarEjecucionReq body, CancellationToken ct)
    {
        try
        {
            var created = await _svc.RegistrarEjecucionAsync(body, ct);
            return Created($"/GetEjecucionesProcesos/{created.ProcesoId}", created);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}