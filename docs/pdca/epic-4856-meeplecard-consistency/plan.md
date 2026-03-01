# Plan: Epic #4856 - MeepleCard Design System Consistency Migration

## Hypothesis
Migrare tutti i componenti che visualizzano entita (giochi, sessioni, documenti, agenti) al design system MeepleCard/EntityTableView per ottenere consistenza visiva glassmorphica in tutta l'app.

## Scope
7 sub-issues organizzate in 3 gruppi:
- **Gruppo A** (User-facing, priorita alta): #4857, #4858, #4859, #4860
- **Gruppo B** (Admin, priorita media): #4861, #4862
- **Gruppo C** (Cross-cutting): #4863

## Execution Strategy
Ogni sub-issue viene implementata con `/implementa`:
1. Branch da `frontend-dev`
2. Implementazione codice
3. Test unitari
4. PR verso `frontend-dev`
5. Merge
6. Chiusura issue

## Sequenza
1. #4857 - PrivateGameCard -> MeepleCard
2. #4858 - Library Cards minori (RecentLibrary, SharedLibrary, GameSide)
3. #4859 - BGG Search Cards (BggGameCard, BggPreviewCard)
4. #4860 - ShareRequestCard -> MeepleCard
5. #4861 - VectorCollectionCard -> MeepleCard
6. #4862 - Admin Tables -> EntityTableView
7. #4863 - Sessions -> EntityListView

## Expected Outcomes
- Consistenza visiva glassmorphica in tutte le aree
- Entity colors corretti per tipo
- Font Quicksand/Nunito ovunque
- Dark mode funzionante
- Nessun componente legacy rimasto

## Risks
- Componenti non trovati (VectorCollectionCard, Documents table) -> verificare e creare se necessario
- Regressioni funzionali -> test accurati per ogni migrazione
- Breaking changes UX -> preservare tutte le funzionalita esistenti
