## Epic #4939 — Player Journey: KB & Agent Creation Flow (Backend)

- [ ] #4941 feat(UserLibrary): auto-link indexed PDF documents when creating game agent
- [ ] #4942 feat(KnowledgeBase): send user notification when VectorDocument indexing completes ← **CRITICAL BLOCKER**
- [ ] #4943 feat(KnowledgeBase): add GET endpoint for PDF processing/indexing status per game
- [ ] #4944 feat(UserLibrary): enforce agent creation quota based on user tier

## Epic #4940 — Player Journey: Frontend UX & Onboarding

- [ ] #4945 feat(frontend): add empty state with onboarding CTA to private library page
- [ ] #4946 feat(frontend): show PDF indexing progress in wizard and on game card [depends: #4943]
- [ ] #4947 feat(frontend): implement notification center drawer with KB-ready CTA [depends: #4942]
- [ ] #4948 feat(frontend): redesign agent config page with KB status panel and 2-column layout [depends: #4941]
- [ ] #4949 feat(frontend): add collapsible journey progress banner for new players

## Implementation Order

Phase 1 (Backend, parallel): #4941, #4942, #4943 → then #4944
Phase 2 (Frontend critical): #4947, #4946, #4948
Phase 3 (Frontend polish): #4945, #4949

---

## Pre-existing TODOs

- il file delete da http://localhost:3000/admin/pdfs dice di fallire, ma poi sparisce dalla lista. viene veramente cancellato?
- la pagina http://localhost:3000/admin/pdfs non e' presente nell'action bar. Creiamo dentro il frame principale /admin, che ha un suo actionbar
  Dal navbar si ritorna alle pagine utente.dal profilo si ritorna a http://localhost:3000/admin/ .
- in modalita mobile, il tap non fa apparire i pulsanti della MeepleCard
- meeplecard:determiniamo in che condizioni ogni pulsante e' attivo e che succede se si
  clicka. Per esempio su una game card il pulsante kb , se il gioco non e' nella collezzione e' nascosto, se e' nella collezione porta
  alla lista dei kb ea
