/**
 * Unit test per l'estrattore di endpoint API — ondata 0 dell'audit esaustivo.
 *
 * I casi coprono le tre insidie misurate sul codice reale il 2026-08-26:
 *   - il prefisso di gruppo va risolto per variabile ricevente (95 file dichiarano
 *     un MapGroup, alcuni due: applicare il primo a tutti dà path sbagliati);
 *   - l'autorizzazione è spesso dichiarata sul gruppo e va ereditata;
 *   - un endpoint senza alcun modificatore resta 'unknown' e va letto a mano.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { describe, expect, it } from 'vitest';

import {
  parseGroupPrefixes,
  parseProgramPrefixes,
  parseRoutingFile,
  registrarName,
} from '../extract-api-endpoints';

describe('parseProgramPrefixes', () => {
  it('associa il metodo di registrazione al prefisso dichiarato', () => {
    const source = `
      var v1Api = app.MapGroup("/api/v1");
      v1Api.MapGroup("/admin/catalog/seeds").MapAdminCatalogSeedEndpoints();
      v1Api.MapGameEndpoints();
    `;
    const prefixes = parseProgramPrefixes(source);
    expect(prefixes.get('MapAdminCatalogSeedEndpoints')).toBe('/api/v1/admin/catalog/seeds');
    expect(prefixes.get('MapGameEndpoints')).toBe('/api/v1');
  });
});

describe('registrarName', () => {
  it('riconosce il metodo di registrazione dichiarato public', () => {
    const source = 'public static RouteGroupBuilder MapGameEndpoints(this RouteGroupBuilder g) {}';
    expect(registrarName(source)).toBe('MapGameEndpoints');
  });

  it('riconosce il metodo di registrazione dichiarato internal', () => {
    // AdminBulkImportEndpoints e affini sono `internal static`: non riconoscerli
    // significa applicare il prefisso di default a endpoint che lo dichiarano
    // già per intero, producendo path come /api/v1/api/v1/admin/...
    const source =
      'internal static void MapAdminBulkImportEndpoints(this IEndpointRouteBuilder app) {}';
    expect(registrarName(source)).toBe('MapAdminBulkImportEndpoints');
  });

  it('ignora i metodi privati di supporto, che non sono registrar', () => {
    const source = [
      'private static void MapGameRetrievalEndpoints(RouteGroupBuilder group) {}',
      'public static RouteGroupBuilder MapGameEndpoints(this RouteGroupBuilder g) {}',
    ].join('\n');
    expect(registrarName(source)).toBe('MapGameEndpoints');
  });
});

describe('parseGroupPrefixes', () => {
  it('risolve i prefissi annidati per variabile', () => {
    const source = `
      var group = app.MapGroup("/admin");
      var agentsGroup = group.MapGroup("/agents");
    `;
    const groups = parseGroupPrefixes(source);
    expect(groups.get('group')?.prefix).toBe('/admin');
    expect(groups.get('agentsGroup')?.prefix).toBe('/admin/agents');
  });

  it("propaga l'autorizzazione del gruppo padre al figlio", () => {
    const source = `
      var group = app.MapGroup("/admin").RequireAuthorization("AdminOnlyPolicy");
      var sub = group.MapGroup("/agents");
    `;
    expect(parseGroupPrefixes(source).get('sub')?.auth).toBe('admin');
  });
});

describe('parseRoutingFile', () => {
  it('estrae metodo, path completo e stato di autorizzazione', () => {
    const source = `
        group.MapGet("/games", HandleGetAllGames)
        .AllowAnonymous()
        .WithTags("Games");

        group.MapPost("/games", HandleCreateGame)
        .RequireAuthorization("AdminOnlyPolicy")
        .WithTags("Games");
    `;
    const found = parseRoutingFile(source, 'Routing/GameEndpoints.cs', '/api/v1');

    expect(found).toHaveLength(2);
    expect(found[0]).toMatchObject({
      method: 'GET',
      path: '/api/v1/games',
      auth: 'anonymous',
      tags: ['Games'],
    });
    expect(found[1]).toMatchObject({ method: 'POST', path: '/api/v1/games', auth: 'admin' });
  });

  it('marca authenticated quando RequireAuthorization non nomina una policy admin', () => {
    const source = `group.MapDelete("/games/{id}", H).RequireAuthorization();`;
    expect(parseRoutingFile(source, 'f.cs', '/api/v1')[0]).toMatchObject({
      method: 'DELETE',
      path: '/api/v1/games/{id}',
      auth: 'authenticated',
    });
  });

  it('applica il prefisso dichiarato dentro il file', () => {
    const source = `
      var group = app.MapGroup("/admin/agent-definitions");
      group.MapGet("/", H).RequireAuthorization("AdminOnlyPolicy");
    `;
    expect(parseRoutingFile(source, 'f.cs', '/api/v1')[0].path).toBe(
      '/api/v1/admin/agent-definitions/'
    );
  });

  it('usa il prefisso del gruppo ricevente, non del primo dichiarato nel file', () => {
    const source = `
      var group = app.MapGroup("/admin");
      var agentsGroup = group.MapGroup("/agents");
      group.MapGet("/health", H);
      agentsGroup.MapGet("/metrics", H);
    `;
    const paths = parseRoutingFile(source, 'f.cs', '/api/v1').map(e => e.path);
    expect(paths).toEqual(['/api/v1/admin/health', '/api/v1/admin/agents/metrics']);
  });

  it("eredita l'autorizzazione dal gruppo quando l'endpoint non la dichiara", () => {
    const source = `
      var group = app.MapGroup("/admin").RequireAuthorization("AdminOnlyPolicy");
      group.MapGet("/users", H);
    `;
    expect(parseRoutingFile(source, 'f.cs', '/api/v1')[0].auth).toBe('admin');
  });

  it("lascia prevalere AllowAnonymous sull'autorizzazione del gruppo", () => {
    const source = `
      var group = app.MapGroup("/x").RequireAuthorization("AdminOnlyPolicy");
      group.MapGet("/public", H).AllowAnonymous();
    `;
    expect(parseRoutingFile(source, 'f.cs', '')[0].auth).toBe('anonymous');
  });

  it("marca unknown quando non c'è alcun modificatore, né sull'endpoint né sul gruppo", () => {
    expect(parseRoutingFile(`group.MapGet("/ping", H);`, 'f.cs', '')[0].auth).toBe('unknown');
  });

  // Il progetto protegge gli endpoint con filtri custom (Extensions/EndpointFilterExtensions.cs),
  // non con le policy ASP.NET: 890 usi contro 264. Ignorarli lascerebbe l'81% degli
  // endpoint classificato 'unknown', cioè un audit senza mappa dei permessi.
  it.each([
    ['.RequireAdminSession()', 'admin'],
    ['.RequireAuthenticatedUser()', 'authenticated'],
    ['.RequireSession()', 'authenticated'],
    ['.RequireLiveSessionParticipant()', 'authenticated'],
  ])('riconosce il filtro custom %s come %s', (filter, expected) => {
    const source = `group.MapGet("/x", H)${filter};`;
    expect(parseRoutingFile(source, 'f.cs', '')[0].auth).toBe(expected);
  });

  // Un endpoint con lambda inline non nomina alcun handler. Interrogare comunque
  // il corpo dei metodi con un nome vuoto farebbe combaciare il PRIMO metodo
  // statico del file, e l'endpoint erediterebbe l'autorizzazione di codice che
  // non lo riguarda: qui il vicino e' protetto e l'endpoint non lo e'.
  it("non eredita l'autorizzazione di un metodo statico che l'endpoint non riferisce", () => {
    const source = `
      group.MapGet("/aperto", async (HttpContext c) => Results.Ok());

      private static IResult AltroHandler(HttpContext context)
      {
          var (ok, s, err) = context.RequireAdminSession();
          return ok ? Results.Ok() : err!;
      }
    `;
    expect(parseRoutingFile(source, 'f.cs', '')[0].auth).toBe('unknown');
  });

  it('preferisce admin quando un endpoint combina più filtri', () => {
    const source = `group.MapPost("/x", H).RequireAuthenticatedUser().RequireAdminSession();`;
    expect(parseRoutingFile(source, 'f.cs', '')[0].auth).toBe('admin');
  });

  it('non tronca la catena sui punto e virgola dentro un handler inline', () => {
    // La forma dominante nel repo: lambda inline il cui corpo contiene ';'.
    // Fermarsi al primo ';' significa non vedere mai i modificatori che seguono.
    const source = `
      group.MapPost("/game-sessions/{sessionId:guid}/actions/score", async (
          Guid sessionId,
          HttpContext httpContext,
          CancellationToken ct) =>
      {
          var userId = httpContext.User.GetUserId();
          if (userId == Guid.Empty)
          {
              return Results.Unauthorized();
          }
          return Results.Ok();
      })
      .RequireAuthenticatedUser()
      .WithTags("Sessions");
    `;
    const found = parseRoutingFile(source, 'f.cs', '');
    expect(found[0].auth).toBe('authenticated');
    expect(found[0].tags).toEqual(['Sessions']);
  });

  it('non si fa confondere da parentesi sbilanciate dentro le stringhe', () => {
    const source = `group.MapGet("/x", H).WithSummary("Public endpoint :-)").RequireAdminSession();`;
    expect(parseRoutingFile(source, 'f.cs', '')[0].auth).toBe('admin');
  });

  it('riconosce il filtro applicato in forma grezza al gruppo', () => {
    const source = `
      var group = app.MapGroup("/admin/queue")
          .AddEndpointFilter<RequireAdminSessionFilter>();
      group.MapGet("/jobs", H);
    `;
    expect(parseRoutingFile(source, 'f.cs', '')[0].auth).toBe('admin');
  });

  it("segue l'autorizzazione imperativa dentro un handler separato", () => {
    // Diversi file non dichiarano l'auth nella catena: la controllano nel corpo
    // dell'handler. Ignorarlo significa marcare 'unknown' endpoint protetti.
    const source = `
      group.MapGet("/config", HandleGetConfig);

      private static async Task<IResult> HandleGetConfig(HttpContext context)
      {
          var (authorized, _, error) = context.RequireAdminSession();
          if (!authorized) return error;
          return Results.Ok();
      }
    `;
    expect(parseRoutingFile(source, 'f.cs', '')[0].auth).toBe('admin');
  });

  it('non attribuisce a un handler il controllo che sta nel metodo successivo', () => {
    const source = `
      group.MapGet("/aperto", HandlePublic);

      private static IResult HandlePublic(HttpContext context)
      {
          return Results.Ok();
      }

      private static IResult HandleProtected(HttpContext context)
      {
          var (authorized, _, error) = context.RequireAdminSession();
          return Results.Ok();
      }
    `;
    expect(parseRoutingFile(source, 'f.cs', '')[0].auth).toBe('unknown');
  });

  it('non si fa confondere da un ; dentro un commento di linea', () => {
    const source = [
      'group.MapGet("/x", H)',
      '    // storico: prima era .AllowAnonymous();',
      '    .RequireAdminSession();',
    ].join('\n');
    expect(parseRoutingFile(source, 'f.cs', '')[0].auth).toBe('admin');
  });
});
