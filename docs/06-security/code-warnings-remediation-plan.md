# Code Warnings Remediation Plan

**Created**: 2025-12-14
**Status**: In Progress
**Priority**: P0/P1/P2

---

## Overview

Questo documento consolida tutte le issue relative a warning di sicurezza e code quality che devono essere risolte prima del rilascio in produzione.

---

## Issue 1: Security Analyzers (P0)

**Regole**: CA3xxx, CA53xx, S2xxx

### Descrizione
Abilitare e correggere tutti i warning di sicurezza critici che sono attualmente soppressi o ignorati. Queste regole sono bloccanti per la produzione.

### Scope
- Rimuovere le soppressioni globali per le regole di sicurezza
- Correggere le violazioni o aggiungere giustificazioni e test di mitigazione
- Assicurare che build in CI falliscano su violazioni critiche

### Tasks
- [ ] Inventario completo delle istanze (file, regola)
- [ ] Creare PR per gruppi di regole (es. CA21xx, CA53xx, S2068, S3649)
- [ ] Applicare fix o mitigazioni e aggiungere test
- [ ] Aggiornare documentazione

---

## Issue 2: Disposing e Resource Leaks (P0)

**Regole**: CA2000, CA1063, IDE0067

### Descrizione
Correggere tutte le segnalazioni relative a oggetti non disposati e pattern IDisposable incompleti. Queste regole prevengono leak di risorse e sono critiche per la stabilità.

### Scope
- Rimuovere le soppressioni #pragma e correggere i casi reali di mancato Dispose
- Applicare using/using var e implementare correttamente IDisposable

### Tasks
- [ ] Lista delle istanze attive (file, linea, regola)
- [ ] Applicare correzioni automatiche dove sicuro (es. `using var cts`)
- [ ] Revisionare i casi complessi e aggiungere unit tests

---

## Issue 3: Null Checks (P1)

**Regole**: CA1062, CS860x

### Descrizione
Rendere sicuro il codice rispetto ai NullReference aggiungendo validazioni e aggiornando API pubbliche.

### Scope
- Abilitare CA1062 nelle cartelle di produzione
- Aggiungere `ArgumentNullException.ThrowIfNull(...)` o pattern equivalenti
- Rimuovere soppressioni per migrations/test file solo dopo approvazione

### Tasks
- [ ] Identificare tutte le istanze CA1062/CS860x
- [ ] Applicare fix automatici dove possibile
- [ ] Revisione manuale per i handler/command pubblici

---

## Issue 4: Generic Catch (P2)

**Regole**: CA1031, S2139

### Descrizione
Sostituire `catch (Exception)` generici con tipi specifici o loggare e rilanciare correttamente per preservare contesto.

### Scope
- Rimuovere suppression #pragma per CA1031/S2139 dove non giustificato
- Sostituire con catch di eccezioni specifiche

### Tasks
- [ ] Inventario delle occorrenze
- [ ] Refactor per casi comuni (IO, network, parsing)
- [ ] Aggiungere test che verifichino il comportamento di recupero

---

## Issue 5: CancellationTokenSource Dispose (P1)

**Regole**: S2930

### Descrizione
Assicurare che tutti i CancellationTokenSource siano creati e disposati correttamente.

### Scope
- Convertire dichiarazioni `new CancellationTokenSource()` in `using var` o pattern CreateLinkedTokenSource
- Rimuovere suppression e aggiungere test per verificare il comportamento di cancellazione

### Tasks
- [ ] Trovare istanze
- [ ] Applicare correzioni automatiche in test e produzione dove appropriato
- [ ] PR per ogni gruppo di file modificati

---

## Related Documentation

- [Security Patterns](./security-patterns.md)
- [Security Analyzers Configuration](./security-analyzers-configuration.md)
- [Generic Catch Analysis](./generic-catch-analysis.md)

---

**Last Updated**: 2025-12-18
