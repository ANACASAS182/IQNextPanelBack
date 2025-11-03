using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IQDTOs;

public record EmpresaDto(int Id, string Rfc, string Nombre);
public record ProcesoDto(int Id, int EmpresaId, string Nombre);
public record EjecucionDto(int Id, int ProcesoId, DateTime FechaHora, byte Estatus);

// Body para POST /RegistrarEjecucion
public record RegistrarEjecucionReq(int ProcesoId, DateTime? FechaHora, byte? Estatus);
