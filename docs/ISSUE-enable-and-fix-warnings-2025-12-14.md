Title: Abilitare e risolvere i warning di analisi del codice (CA*, S*, MA*, IDE*, CS*)

Description:
Questo task serve ad abilitare progressivamente gli analyzer e correggere i warning soppressi/NoWarn nel repository.
Molti avvisi importanti sono attualmente soppressi via NoWarn in apps/api/tests/Api.Tests/Api.Tests.csproj, tramite .editorconfig e #pragma warning disable in vari file.

Motivazione:
- Migliorare la sicurezza del codice (CA2xxx/CA3xxx, S2xxx) e prevenire regressioni.
- Ridurre i leak di risorse e problemi di disposing (CA2000, IDE0067, CA1063).
- Garantire la robustezza rispetto ai null (CA1062, CS860x).

Current timestamp: 2025-12-14T09:03:05.604Z

Scope / Acceptance Criteria:
- Rimuovere/sostituire le soppressioni globali più critiche (NoWarn in Api.Tests.csproj) e ripristinare le regole in .editorconfig per produzione.
- Correggere o giustificare ogni istanza con commento // Justification: ... per le eccezioni deliberate.
- Risolvere i primi gruppi prioritari: sicurezza, disposal/IDisposable, null-checks.
- Documentare le soppressioni residue con motivo, ticket di follow-up e owner.

Tasks (proposal):
1) Inventario: avere la lista completa delle soppressioni (file, regola, motivo) — DONE (file di tracking generato dalla build).
2) Abilitare in modalità warning prima, poi errore per CA2000/CA1063/CS8602 in src/ produzione.
3) Applicare correzioni automatiche sicure (es. using var per CancellationTokenSource, ArgumentNullException checks) e review manuale per i casi complessi.
4) Aggiornare .editorconfig e rimuovere NoWarn da Api.Tests.csproj gradualmente, con PR per ogni gruppo di regole.
5) Verificare build CI verde e nessun warning critico open.

Priority (proposal):
- P0: Regole di sicurezza (CA3xxx/S2xxx), CA2000
- P1: CA1062 (null checks), IDE0067/CA1063 (IDisposable)
- P2: CA1031 / S2139 (general catch patterns)
- P3: Style e MA/renaming warnings

Labels: security, analyzer, maintenance, tech-debt
Assignee: Unassigned

Checklist:
- [ ] Create separate branches/PRs per rule group
- [ ] Run full build + analyzers on PRs
- [ ] Add unit/integration fixes where needed
- [ ] Update docs on rationale for kept suppressions

Notes:
Alcune soppressioni sono intenzionali (CQRS multi-type files, test conventions). Correggere prima le soppressioni che compromettono sicurezza o portano a leak di risorse.

References:
- .editorconfig (root & apps/api)
- apps/api/tests/Api.Tests/Api.Tests.csproj (NoWarn list)
- build outputs and warnings_tracking.csv

Created by automation at 2025-12-14T09:03:05.604Z
