using IQDTOs;
using IQServices.Interfaces;
using IQRepositories.Interfaces;

namespace IQServices;

public sealed class ProcesosService : IProcesosService
{
    private readonly IProcesosRepository _repo;
    public ProcesosService(IProcesosRepository repo) => _repo = repo;

    public Task<IEnumerable<EmpresaDto>> GetEmpresasAsync(CancellationToken ct)
        => _repo.GetEmpresasAsync(ct);

    public Task<IEnumerable<ProcesoDto>> GetProcesosByEmpresaAsync(int empresaId, CancellationToken ct)
        => _repo.GetProcesosByEmpresaAsync(empresaId, ct);

    public Task<IEnumerable<EjecucionDto>> GetEjecucionesByProcesoAsync(int procesoId, CancellationToken ct)
        => _repo.GetEjecucionesByProcesoAsync(procesoId, ct);

    public Task<EjecucionDto> RegistrarEjecucionAsync(RegistrarEjecucionReq req, CancellationToken ct)
    {
        if (req.ProcesoId <= 0) throw new ArgumentException("ProcesoId es obligatorio y > 0");
        if (req.Estatus is not null && req.Estatus is not 0 and not 1)
            throw new ArgumentException("Estatus debe ser 0 o 1");
        return _repo.RegistrarEjecucionAsync(req, ct);
    }
}
