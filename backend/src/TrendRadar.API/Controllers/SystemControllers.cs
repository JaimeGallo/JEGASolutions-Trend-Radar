using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrendRadar.Application.Abstractions;
using TrendRadar.Infrastructure.Persistence;

namespace TrendRadar.API.Controllers;

[ApiController]
[Route("api/health")]
[AllowAnonymous]
public sealed class HealthController(TrendRadarDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Ok(new { status = "ok" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "sin base de datos" });
}

[ApiController]
[Route("api/domains")]
[Authorize]
public sealed class DomainsController(ITaxonomyReader taxonomy) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<DomainDto>> List(CancellationToken ct) =>
        (await taxonomy.ListDomainsAsync(ct)).Select(d => new DomainDto(d.Id, d.Code, d.Name, d.ParentId));
}

[ApiController]
[Route("api/integrity")]
[Authorize(Policy = Policies.Owner)]
public sealed class IntegrityController(IIntegrityVerifier verifier) : ControllerBase
{
    [HttpGet("verify")]
    public async Task<IntegrityDto> Verify(CancellationToken ct)
    {
        var r = await verifier.VerifyAuditChainAsync(ct);
        return new IntegrityDto(r.IsValid, r.CheckedEntries, r.FirstInvalidEntryId, r.VerifiedAt);
    }
}

[ApiController]
[Route("api/audit")]
[Authorize(Policy = Policies.Owner)]
public sealed class AuditController(IAuditReader reader) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<AuditEntryDto>> List(
        [FromQuery] string? entityType, [FromQuery] string? entityId, [FromQuery] int take = 100, CancellationToken ct = default) =>
        (await reader.ListAsync(new AuditQuery(entityType, entityId, take), ct)).Select(a => new AuditEntryDto(
            a.Id, a.OccurredAt, a.UserId, a.Action, a.EntityType, a.EntityId,
            a.OldValue, a.NewValue, a.Reason, Convert.ToHexStringLower(a.ChainHash)));
}
