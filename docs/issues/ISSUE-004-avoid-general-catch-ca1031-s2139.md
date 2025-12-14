Title: Ridurre catch generici e migliorare logging (CA1031, S2139)

Description:
Sostituire catch (Exception) generici con tipi specifici o loggare e rilanciare correttamente per preservare contesto.

Scope:
- Rimuovere suppression #pragma per CA1031/S2139 dove non giustificato
- Sostituire con catch di eccezioni specifiche o utilizzare quando appropriato pattern di fallback ben documentati

Tasks:
- [ ] Inventario delle occorrenze
- [ ] Refactor per casi comuni (IO, network, parsing)
- [ ] Aggiungere test che verifichino il comportamento di recupero

Priority: P2
Assignee: Unassigned

Created: 2025-12-14T09:29:30.963Z
