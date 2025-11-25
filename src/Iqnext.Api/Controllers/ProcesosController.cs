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

    [HttpGet("GetRegistrarCurl/{procesoId:int}")]
    public async Task<ActionResult<RegistrarCurlDto>> GetRegistrarCurl(
    int procesoId,
    CancellationToken ct)
    {
        await using var con = new SqlConnection(_connString);
        await con.OpenAsync(ct);

        const string sql = "SELECT uuid FROM dbo.proceso WHERE id = @id;";
        var uuid = await con.ExecuteScalarAsync<string>(
            new CommandDefinition(sql, new { id = procesoId }, cancellationToken: ct));

        if (string.IsNullOrWhiteSpace(uuid))
            return NotFound($"Proceso {procesoId} no existe o no tiene UUID.");

        // Construye la URL real según donde esté corriendo (local, server, detrás de Nginx, etc.)
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var endpoint = $"{baseUrl}/RegistrarEjecucion";

        // Solo estatus + uuid, tal como lo usas en n8n
        var payload1 = $"{{ \"estatus\": 1, \"uuid\": \"{uuid}\" }}";
        var payload0 = $"{{ \"estatus\": 0, \"uuid\": \"{uuid}\" }}";

        var curl1 = $"curl -X POST '{endpoint}' " +
                    "-H 'accept: application/json' " +
                    "-H 'Content-Type: application/json' " +
                    $"-d '{payload1}'";

        var curl0 = $"curl -X POST '{endpoint}' " +
                    "-H 'accept: application/json' " +
                    "-H 'Content-Type: application/json' " +
                    $"-d '{payload0}'";

        var resp = new RegistrarCurlDto(
            ProcesoId: procesoId,
            Uuid: uuid,
            CurlEstatus1: curl1,
            CurlEstatus0: curl0
        );

        return Ok(resp);
    }

    [HttpPost("CrearProceso")]
    public async Task<ActionResult<ProcesoCreadoDto>> CrearProceso(
    [FromBody] CrearProcesoReq body,
    CancellationToken ct)
    {
        if (body.EmpresaId <= 0 || string.IsNullOrWhiteSpace(body.Nombre))
            return BadRequest("EmpresaId y nombre son obligatorios.");

        await using var con = new SqlConnection(_connString);
        await con.OpenAsync(ct);

        // 1) Verifica que exista la empresa
        const string sqlEmpresa = "SELECT COUNT(1) FROM dbo.empresa WHERE id = @EmpresaId;";
        var existsEmpresa = await con.ExecuteScalarAsync<int>(
            new CommandDefinition(sqlEmpresa, new { body.EmpresaId }, cancellationToken: ct));
        if (existsEmpresa == 0)
            return BadRequest("La empresa no existe.");

        // 2) (Opcional) Evita duplicar nombre dentro de la misma empresa
        const string sqlDupNombre = @"
        SELECT TOP 1 1
        FROM dbo.proceso
        WHERE empresaId = @EmpresaId AND nombre = @Nombre;
    ";
        var dup = await con.ExecuteScalarAsync<int?>(
            new CommandDefinition(sqlDupNombre, new { body.EmpresaId, body.Nombre }, cancellationToken: ct));
        if (dup.HasValue)
            return BadRequest("Ya existe un proceso con ese nombre en la empresa.");

        // 3) Genera UUID y crea
        var uuid = Guid.NewGuid().ToString();

        const string sqlInsert = @"
        INSERT INTO dbo.proceso (empresaId, nombre, uuid)
        OUTPUT inserted.id    AS Id,
               inserted.empresaId AS EmpresaId,
               inserted.nombre AS Nombre,
               inserted.uuid   AS Uuid
        VALUES (@EmpresaId, @Nombre, @Uuid);
    ";

        try
        {
            var created = await con.QuerySingleAsync<ProcesoCreadoDto>(
                new CommandDefinition(sqlInsert,
                    new { body.EmpresaId, body.Nombre, Uuid = uuid },
                    cancellationToken: ct));

            // 201 Created + Location con el id del proceso
            return Created($"/GetEjecucionesProcesos/{created.Id}", created);
        }
        catch (SqlException ex)
        {
            return StatusCode(500, $"Error al crear el proceso: {ex.Message}");
        }
    }

    [HttpPost("Login")]
    public async Task<ActionResult<LoginResponseDto>> Login(
        [FromBody] LoginReq body,
        CancellationToken ct)
    {
        // Validación básica
        if (string.IsNullOrWhiteSpace(body.Correo) || string.IsNullOrWhiteSpace(body.Contrasena))
        {
            return BadRequest(new LoginResponseDto(
                Exito: false,
                Mensaje: "Correo y contraseña son obligatorios.",
                UsuarioId: null,
                EmpresaId: null,
                EmpresaNombre: null,
                Nombre: null,
                Correo: null,
                EsAdmin: false   // <- añadir siempre el campo
            ));
        }

        const string sql = @"
SELECT TOP 1
    ue.Id            AS UsuarioId,
    ue.EmpresaId     AS EmpresaId,
    ue.Correo        AS Correo,
    ue.Nombre        AS Nombre,
    ue.Contrasena    AS ContrasenaHash,
    ue.esAdmin       AS EsAdmin,      -- <- viene como TINYINT/bit
    e.Nombre         AS EmpresaNombre
FROM dbo.UsuarioEmpresa ue
JOIN dbo.empresa e ON e.id = ue.EmpresaId
WHERE ue.Correo = @Correo;";

        await using var con = new SqlConnection(_connString);
        await con.OpenAsync(ct);

        var user = await con.QuerySingleOrDefaultAsync<UsuarioEmpresaLoginRow>(
            new CommandDefinition(sql, new { body.Correo }, cancellationToken: ct));

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
                Correo: null,
                EsAdmin: false
            ));
        }

        // Verificar contraseña
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
                Correo: null,
                EsAdmin: false
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
            Correo: user.Correo,
            EsAdmin: (user.EsAdmin == 1) // si EsAdmin es tinyint; si es bit: (user.EsAdmin)
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
