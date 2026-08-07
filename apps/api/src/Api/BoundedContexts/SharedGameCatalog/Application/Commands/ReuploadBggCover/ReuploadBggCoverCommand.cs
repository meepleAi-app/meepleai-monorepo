using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.ReuploadBggCover;

/// <summary>
/// #3590 Slice B — ri-ospita su R2 la cover che BoardGameGeek espone per il <c>BggId</c> del gioco.
/// <para>
/// Percorso <b>server-to-server admin</b>, legittimo per ADR-059 §2 e già in uso alla creazione di
/// uno SharedGame da PDF (<c>CreateSharedGameFromPdfCommandHandler</c>); qui diventa un trigger per
/// un gioco <b>già a catalogo</b>, che prima non esisteva.
/// </para>
/// <para>
/// NON accetta un URL: la sorgente è l'immagine che BGG dichiara per quel <c>BggId</c>. Il campo
/// cover manuale a URL libero resta sbarrato verso gli host geekdo da
/// <see cref="Api.SharedKernel.Infrastructure.Http.BggHostDenyList"/> (ban #2123) — questo comando
/// non la allenta e non la aggira: usa un canale diverso, già sanzionato.
/// </para>
/// </summary>
/// <param name="GameId">Gioco a catalogo di cui ri-ospitare la cover.</param>
/// <param name="AdminId">Admin che ha richiesto l'operazione (audit).</param>
internal record ReuploadBggCoverCommand(Guid GameId, Guid AdminId) : ICommand<BggCoverResult>;

/// <summary>Esito del re-upload: la chiave R2 persistita sull'aggregato.</summary>
/// <param name="R2Key">Chiave R2 della cover ri-ospitata.</param>
internal record BggCoverResult(string R2Key);
