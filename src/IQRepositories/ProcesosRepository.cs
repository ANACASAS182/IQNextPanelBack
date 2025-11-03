using System.Data;
using Dapper;
using IQDTOs;
using IQData;
using IQRepositories.Interfaces;

namespace IQRepositories;

public sealed class ProcesosRepository : IProcesosRepository
{
    private readonly DbConnectionFactory _factory;
    public ProcesosRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<EmpresaDto>> GetEmpresasAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, rfc AS Rfc, nombre AS Nombre
            FROM dbo.empresa
            ORDER BY nombre
        """;
        using var conn = _factory.Create();
        return await conn.QueryAsync<EmpresaDto>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<IEnumerable<ProcesoDto>> GetProcesosByEmpresaAsync(int empresaId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, empresaId AS EmpresaId, nombre AS Nombre
            FROM dbo.proceso
            WHERE empresaId = @empresaId
            ORDER BY nombre
        """;
        using var conn = _factory.Create();
        return await conn.QueryAsync<ProcesoDto>(new CommandDefinition(sql, new { empresaId }, cancellationToken: ct));
    }

    public async Task<IEnumerable<EjecucionDto>> GetEjecucionesByProcesoAsync(int procesoId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, procesoId AS ProcesoId, fechaHora AS FechaHora, estatus AS Estatus
            FROM dbo.ejecucion
            WHERE procesoId = @procesoId
            ORDER BY fechaHora DESC
        """;
        using var conn = _factory.Create();
        return await conn.QueryAsync<EjecucionDto>(new CommandDefinition(sql, new { procesoId }, cancellationToken: ct));
    }

    public async Task<EjecucionDto> RegistrarEjecucionAsync(RegistrarEjecucionReq req, CancellationToken ct)
    {
        const string exists = "SELECT 1 FROM dbo.proceso WHERE id=@id";
        const string insert = """
            INSERT INTO dbo.ejecucion (procesoId, fechaHora, estatus)
            VALUES (@ProcesoId, COALESCE(@FechaHora, SYSUTCDATETIME()), COALESCE(@Estatus, 0));
            SELECT CAST(SCOPE_IDENTITY() AS INT);
        """;
        const string get = """
            SELECT id AS Id, procesoId AS ProcesoId, fechaHora AS FechaHora, estatus AS Estatus
            FROM dbo.ejecucion
            WHERE id = @id
        """;

        using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        var ok = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(exists, new { id = req.ProcesoId }, cancellationToken: ct));
        if (ok != 1) throw new KeyNotFoundException($"Proceso {req.ProcesoId} no existe.");

        var newId = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(insert, req, cancellationToken: ct));

        return await conn.QuerySingleAsync<EjecucionDto>(
            new CommandDefinition(get, new { id = newId }, cancellationToken: ct));
    }
}