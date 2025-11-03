using IQDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IQServices.Interfaces
{
    public interface IProcesosService
    {
        Task<IEnumerable<EmpresaDto>> GetEmpresasAsync(CancellationToken ct);
        Task<IEnumerable<ProcesoDto>> GetProcesosByEmpresaAsync(int empresaId, CancellationToken ct);
        Task<IEnumerable<EjecucionDto>> GetEjecucionesByProcesoAsync(int procesoId, CancellationToken ct);
        Task<EjecucionDto> RegistrarEjecucionAsync(RegistrarEjecucionReq req, CancellationToken ct);
    }
}
