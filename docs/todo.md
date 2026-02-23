- Registrarsi con email
- Registrarsi con oauth
- Login e logut con email/oauth
- Aggiungi un gioco da shared game
- Aggiungi un gioco custom (solo utenti Normal o piu')
- Aggiungi PDF a collection privata
- Rimuovi PDF da collection privata
- Visualizza GAME CARD e collegamento verso KNOWLEDGE CARD

Verifica End-to-End

1.  Carica PDF → verifica EntityLink creato in DB con SourceEntityType='KbCard'
2.  GET /api/v1/library/entity-links?entityType=game&entityId=X&targetEntityType=kb_card → ritorna link KB
3.  MeepleCard del gioco mostra badge con stato worst-case
4.  GameDetailDrawer → sezione "Documenti KB" lista i PDF con stato
5.  Retry PDF fallito → admin e owner possono farlo (user altri no → 403)
6.  dotnet test --filter "Category=Unit|Category=Integration" → tutti green
7.  pnpm test → tutti green
