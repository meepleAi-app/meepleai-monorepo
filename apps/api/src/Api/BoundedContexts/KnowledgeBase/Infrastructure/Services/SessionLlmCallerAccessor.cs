using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// #4164: legge il chiamante dalla sessione validata della richiesta HTTP in corso.
/// </summary>
/// <remarks>
/// <para>
/// Segue il pattern del progetto, che non ha un <c>ICurrentUserService</c>: il middleware di
/// autenticazione mette <see cref="SessionStatusDto"/> in <c>HttpContext.Items</c>, come lo leggono
/// <c>AuditLoggingBehavior</c> e <c>KbQualityCurrentUser</c>.
/// </para>
/// <para>
/// Usa <see cref="Principal.Subject"/>, non <see cref="Principal.EffectiveActor"/>: il costo
/// registrato alimenta il budget dell'utente, e per le chiavi di quota il contratto di
/// <see cref="Principal"/> prescrive il soggetto. Durante un'impersonazione la spesa va quindi
/// all'utente impersonato.
/// </para>
/// <para>
/// Fuori da una richiesta (job, hosted service) <see cref="IHttpContextAccessor.HttpContext"/> è
/// <c>null</c>, e così il chiamante.
/// </para>
/// </remarks>
internal sealed class SessionLlmCallerAccessor : ILlmCallerAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SessionLlmCallerAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc/>
    public LlmUserContext? Current
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null
                || !httpContext.Items.TryGetValue(nameof(SessionStatusDto), out var value)
                || value is not SessionStatusDto { IsValid: true, Principal: { } principal })
            {
                return null;
            }

            var subject = principal.Subject;
            return new LlmUserContext(subject.Id, subject.Role, MapTier(subject.Role));
        }
    }

    /// <summary>
    /// Il tier non entra nel registro dei costi; lo si valorizza come fa
    /// <c>LlmUserContextMapper</c> per i ruoli, perché il record lo richiede.
    /// </summary>
    private static LlmUserTier MapTier(string role) => role.ToLowerInvariant() switch
    {
        "admin" or "superadmin" => LlmUserTier.Admin,
        "editor" => LlmUserTier.Editor,
        _ => LlmUserTier.User
    };
}
