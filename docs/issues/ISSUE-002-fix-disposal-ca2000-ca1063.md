Title: Risolvere i problemi di disposing e resource leaks (CA2000, CA1063, IDE0067)

Description:
Correggere tutte le segnalazioni relative a oggetti non disposati e pattern IDisposable incompleti.
Queste regole prevengono leak di risorse e sono critiche per la stabilità a lungo termine.

Scope:
- Rimuovere le soppressioni #pragma e correggere i casi reali di mancato Dispose
- Applicare using/using var e implementare correttamente IDisposable

Tasks:
- [ ] Lista delle istanze attive (file, linea, regola)
- [ ] Applicare correzioni automatiche dove sicuro (es. using var cts)
- [ ] Revisionare i casi complessi e aggiungere unit tests

Priority: P0
Assignee: Unassigned

Created: 2025-12-14T09:29:30.963Z
