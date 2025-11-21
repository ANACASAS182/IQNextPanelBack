using Dapper;
using IQDTOs;
using IQServices.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using BCrypt.Net;

namespace Iqnext.Api.Controllers;

[ApiController]
[Route("/")]
public sealed class ProcesosController : ControllerBase
{
    private readonly IProcesosService _svc;
    private readonly string _connString;

    public ProcesosController(IProcesosService svc, IConfiguration config)
    {
        _svc = svc;
        _connString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'Default' en appsettings.");
    }

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

    [HttpPost("CrearUsuarioEmpresa")]
    public async Task<ActionResult<UsuarioEmpresaDto>> CrearUsuarioEmpresa(
   [FromBody] CrearUsuarioEmpresaReq body,
   CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Correo) || string.IsNullOrWhiteSpace(body.Contrasena))
            return BadRequest("Correo y contraseña son obligatorios.");

        await using var con = new SqlConnection(_connString);
        await con.OpenAsync(ct);
        const string sqlEmpresa = @"SELECT COUNT(1) FROM dbo.empresa WHERE id = @EmpresaId;";
        var existeEmpresa = await con.ExecuteScalarAsync<int>(
            new CommandDefinition(sqlEmpresa, new { body.EmpresaId }, cancellationToken: ct));

        if (existeEmpresa == 0)
            return BadRequest("La empresa no existe.");
        const string sqlExisteCorreo = @"
SELECT TOP 1 1
FROM dbo.UsuarioEmpresa
WHERE EmpresaId = @EmpresaId
  AND Correo = @Correo;";

        var existeCorreo = await con.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                sqlExisteCorreo,
                new { body.EmpresaId, body.Correo },
                cancellationToken: ct));

        if (existeCorreo.HasValue)
            return BadRequest("El correo ya está registrado para esta empresa.");
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(body.Contrasena);
        const string sqlInsert = @"
INSERT INTO dbo.UsuarioEmpresa (EmpresaId, Correo, Nombre, Contrasena)
OUTPUT inserted.Id, inserted.EmpresaId, inserted.Correo, inserted.Nombre
VALUES (@EmpresaId, @Correo, @Nombre, @ContrasenaHash);";

        try
        {
            var created = await con.QuerySingleAsync<UsuarioEmpresaDto>(
                new CommandDefinition(
                    sqlInsert,
                    new
                    {
                        body.EmpresaId,
                        body.Correo,
                        body.Nombre,
                        ContrasenaHash = passwordHash
                    },
                    cancellationToken: ct));

            return Ok(created);
        }
        catch (SqlException ex)
        {
            return StatusCode(500, $"Error al crear el usuario: {ex.Message}");
        }
    }

    [HttpPost("Login")]
    public async Task<ActionResult<LoginResponseDto>> Login(
    [FromBody] LoginReq body,
    CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Correo) || string.IsNullOrWhiteSpace(body.Contrasena))
        {
            return BadRequest(new LoginResponseDto(
                Exito: false,
                Mensaje: "Correo y contraseña son obligatorios.",
                UsuarioId: null,
                EmpresaId: null,
                EmpresaNombre: null,
                Nombre: null,
                Correo: null
            ));
        }

        const string sql = @"
SELECT TOP 1
    ue.Id            AS UsuarioId,
    ue.EmpresaId     AS EmpresaId,
    ue.Correo        AS Correo,
    ue.Nombre        AS Nombre,
    ue.Contrasena    AS ContrasenaHash,
    e.Nombre         AS EmpresaNombre
FROM dbo.UsuarioEmpresa ue
JOIN dbo.empresa e ON e.id = ue.EmpresaId
WHERE ue.Correo = @Correo;";

        await using var con = new SqlConnection(_connString);
        await con.OpenAsync(ct);

        var user = await con.QuerySingleOrDefaultAsync<UsuarioEmpresaLoginRow>(
            new CommandDefinition(
                sql,
                new { body.Correo },
                cancellationToken: ct));

        // Usuario no existe
        if (user is null)
        {
            return Unauthorized(new LoginResponseDto(
                Exito: false,
                Mensaje: "Correo o contraseña incorrectos.",
                UsuarioId: null,
                EmpresaId: null,
                EmpresaNombre: null,
                Nombre: null,
                Correo: null
            ));
        }

        // Verificar hash de contraseña
        var ok = BCrypt.Net.BCrypt.Verify(body.Contrasena, user.ContrasenaHash);

        if (!ok)
        {
            return Unauthorized(new LoginResponseDto(
                Exito: false,
                Mensaje: "Correo o contraseña incorrectos.",
                UsuarioId: null,
                EmpresaId: null,
                EmpresaNombre: null,
                Nombre: null,
                Correo: null
            ));
        }

        // Login correcto
        var resp = new LoginResponseDto(
            Exito: true,
            Mensaje: "Inicio de sesión correcto.",
            UsuarioId: user.UsuarioId,
            EmpresaId: user.EmpresaId,
            EmpresaNombre: user.EmpresaNombre,
            Nombre: user.Nombre,
            Correo: user.Correo
        );

        return Ok(resp);
    }

    [HttpPost("CambiarPassword")]
    public async Task<ActionResult<CambiarPasswordResp>> CambiarPassword(
    [FromBody] CambiarPasswordReq body,
    CancellationToken ct)
    {
        // Validaciones rápidas
        if (string.IsNullOrWhiteSpace(body.Correo) ||
            string.IsNullOrWhiteSpace(body.PasswordActual) ||
            string.IsNullOrWhiteSpace(body.PasswordNueva))
        {
            return BadRequest(new CambiarPasswordResp(false, "Correo, contraseña actual y nueva son obligatorios."));
        }

        if (body.PasswordNueva.Length < 6)
            return BadRequest(new CambiarPasswordResp(false, "La nueva contraseña debe tener al menos 6 caracteres."));

        await using var con = new SqlConnection(_connString);
        await con.OpenAsync(ct);

        // 1) Traer hash actual del usuario (scoped por empresa + correo)
        const string sqlGet = @"
SELECT TOP 1 Id, EmpresaId, Correo, Contrasena
FROM dbo.UsuarioEmpresa
WHERE EmpresaId = @EmpresaId AND Correo = @Correo;";

        var user = await con.QuerySingleOrDefaultAsync(new CommandDefinition(
            sqlGet,
            new { body.EmpresaId, Correo = body.Correo.Trim() },
            cancellationToken: ct));

        if (user is null)
            return NotFound(new CambiarPasswordResp(false, "Usuario no encontrado para la empresa indicada."));

        string hashActual = (string)user.Contrasena;

        // 2) Verificar contraseña actual
        bool ok = BCrypt.Net.BCrypt.Verify(body.PasswordActual, hashActual);
        if (!ok)
            return Unauthorized(new CambiarPasswordResp(false, "La contraseña actual es incorrecta."));

        // 3) Generar hash de la nueva
        string nuevoHash = BCrypt.Net.BCrypt.HashPassword(body.PasswordNueva);

        // 4) Actualizar
        const string sqlUpdate = @"
UPDATE dbo.UsuarioEmpresa
SET Contrasena = @NuevoHash
WHERE Id = @Id;";

        int rows = await con.ExecuteAsync(new CommandDefinition(
            sqlUpdate,
            new { Id = (long)user.Id, NuevoHash = nuevoHash },
            cancellationToken: ct));

        if (rows == 0)
            return StatusCode(500, new CambiarPasswordResp(false, "No se pudo actualizar la contraseña."));

        return Ok(new CambiarPasswordResp(true, "Contraseña actualizada correctamente."));
    }


}
