using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IQDTOs;

public record EmpresaDto(int Id, string Rfc, string Nombre);
public record ProcesoDto(int Id, int EmpresaId, string Nombre);
public record EjecucionDto(int Id, int ProcesoId, DateTime FechaHora, byte Estatus);
public record RegistrarEjecucionReq(int ProcesoId, DateTime? FechaHora, byte? Estatus);
public sealed record LoginReq(string Correo,string Contrasena);

public sealed record LoginResponseDto(bool Exito,string Mensaje,long? UsuarioId,int? EmpresaId,string? EmpresaNombre,string? Nombre,string? Correo);

public sealed record CrearUsuarioEmpresaReq(
      int EmpresaId,
      string Correo,
      string? Nombre,
      string Contrasena
);

public sealed record UsuarioEmpresaDto(
    long Id,
    int EmpresaId,
    string Correo,
    string? Nombre
);

public sealed class UsuarioEmpresaLoginRow
{
    public long UsuarioId { get; set; }
    public int EmpresaId { get; set; }
    public string Correo { get; set; } = default!;
    public string? Nombre { get; set; }
    public string ContrasenaHash { get; set; } = default!;
    public string EmpresaNombre { get; set; } = default!;
}
public sealed class CambiarPasswordReq
{
    public int EmpresaId { get; init; }
    public string Correo { get; init; } = default!;
    public string PasswordActual { get; init; } = default!;
    public string PasswordNueva { get; init; } = default!;
}
public sealed record CambiarPasswordResp(bool Exito, string Mensaje);

