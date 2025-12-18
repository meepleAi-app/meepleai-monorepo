Title: Abilitare e risolvere i controlli di null (CA1062 e CS860x)

Description:
Rendere sicuro il codice rispetto ai NullReference aggiungendo validazioni e aggiornando API pubbliche per validare argomenti.

Scope:
- Abilitare CA1062 nelle cartelle di produzione
- Aggiungere ArgumentNullException.ThrowIfNull(...) o pattern equivalenti
- Rimuovere soppressioni per migrations/test file solo dopo approvazione

Tasks:
- [ ] Identificare tutte le istanze CA1062/CS860x
- [ ] Applicare fix automatici dove possibile
- [ ] Revisione manuale per i handler/command pubblici

Priority: P1
Assignee: Unassigned

Created: 2025-12-14T09:29:30.963Z
