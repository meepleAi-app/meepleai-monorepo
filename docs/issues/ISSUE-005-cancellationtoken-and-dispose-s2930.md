Title: Correggere l'uso di CancellationTokenSource e dispose (S2930)

Description:
Assicurare che tutti i CancellationTokenSource siano creati e disposati correttamente (using/using var o Dispose() nel finally).

Scope:
- Convertire dichiarazioni new CancellationTokenSource() in using var o pattern CreateLinkedTokenSource
- Rimuovere suppression e aggiungere test per verificare il comportamento di cancellazione

Tasks:
- [ ] Trovare istanze (grep già eseguito)
- [ ] Applicare correzioni automatiche in test e produzione dove appropriato
- [ ] PR per ogni gruppo di file modificati

Priority: P1
Assignee: Unassigned

Created: 2025-12-14T09:29:30.963Z
