# Criteri di giudizio (dall'utente, sulla misura di Catan del 2026-10-18)

Voti possibili: corretta, parziale, sbagliata, errore (nessuna risposta per un errore tecnico).

1. **Rinuncia onesta = corretta.** Se l'agente dice di non trovare l'informazione (per esempio «This information is not available in the provided rulebook.») senza inventare nulla, la risposta è corretta, anche quando il regolamento avrebbe la risposta (CAT-03, confermato dall'utente).
2. **Giusta nel merito = corretta**, anche se il dato non viene dal regolamento indicizzato ma dalla conoscenza del modello (CAT-10: il costo della città, giusto, viene dal modello).
3. **Lingua sbagliata:** se la risposta è giusta ma in un'altra lingua (spagnolo, inglese…), resta corretta nel merito; va però segnalata, perché «bisogna tradurla».
4. **Verdetto opposto all'atteso = non corretta.** A CAT-01 l'agente ha risposto «No» dove la risposta attesa era «Sì, ma solo con il giocatore di turno», pur cogliendo il vincolo del giocatore di turno: l'utente l'ha giudicata parziale. Un verdetto opposto senza nulla di giusto è sbagliato.
5. **Premessa falsa con sostanza giusta = corretta.** A CAT-08 la domanda chiede di una carta inesistente («Esercito Avanzato»); l'agente l'ha letta come «Largest Army» e ha dato la regola giusta, senza dire che quella carta non esiste: l'utente l'ha giudicata corretta.
6. **Risposte attese «fuori dal corpus»** (campo `fuori_dal_corpus`): per il criterio 2, va bene sia la risposta vera sia la rinuncia onesta; è sbagliata solo una risposta falsa.
7. **Invenzione falsa = sbagliata**: una regola presentata come vera che contraddice la risposta attesa verificata sul regolamento.
