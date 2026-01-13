# BoardGameGeek API Setup

## Overview

MeepleAI integra l'API di BoardGameGeek per cercare giochi e importare dettagli. A partire dal 2024, BGG richiede **registrazione dell'applicazione e token di autorizzazione** per la maggior parte degli utilizzi dell'API.

## Ottenere un Token BGG

### 1. Registra la Tua Applicazione

Vai su: https://boardgamegeek.com/applications

- Clicca "Register New Application"
- Compila il form con:
  - **Application Name**: MeepleAI (o il tuo deployment name)
  - **Application URL**: https://tuodominio.com (o http://localhost:3000 per dev)
  - **Description**: AI-powered board game rules assistant
  - **Contact Email**: tua email
  - **License Type**:
    - Non-Commercial (se no ads, no payments)
    - Commercial (se monetizzato)

### 2. Attendi l'Approvazione

- BGG team revisionerà la richiesta
- Riceverai email quando approvata
- Il token apparirà nella dashboard applicazioni

### 3. Configura il Token

#### Opzione A: Variabile d'Ambiente (Raccomandato)

```bash
# .env o .env.local
BGG_API_TOKEN=your_token_here
```

#### Opzione B: appsettings.json (Solo per Development)

```json
{
  "Bgg": {
    "ApiToken": "your_token_here"
  }
}
```

⚠️ **NON committare mai il token su Git!**

### 4. Restart dei Container

```bash
cd infra
docker compose down
docker compose --profile full up -d
```

## Verifica Configurazione

### Test Token Configurato

```bash
# Verifica che il token sia caricato
docker exec meepleai-api printenv | grep Bgg__ApiToken

# Test ricerca BGG (richiede login in app)
curl http://localhost:8080/api/v1/bgg/search?q=Catan
```

### Test Senza Token (401 Expected)

Senza token, riceverai:
```json
{
  "error": "internal_server_error",
  "message": "BoardGameGeek API is currently unavailable"
}
```

Con token valido:
```json
{
  "games": [
    {"id": 13, "name": "Catan", "yearPublished": 1995, ...}
  ]
}
```

## Rate Limiting

BGG API ha limiti di utilizzo:

- **Configurato in MeepleAI**: Max 2 req/s (configurabile in `appsettings.json`)
- **BGG Limits**: Variano per license type (non documentati pubblicamente)
- **Caching**: Risultati cachati per 7 giorni (default)

### Monitoraggio Uso API

Dashboard BGG applications mostra:
- Request count
- Rate limit status
- Token expiration

## Troubleshooting

### 401 Unauthorized

**Causa**: Token mancante, invalido o scaduto

**Soluzioni**:
1. Verifica token configurato: `printenv | grep BGG`
2. Controlla token valido nella dashboard BGG
3. Verifica formato header: deve essere `Authorization: Bearer TOKEN` (no colon dopo Bearer!)
4. Assicurati di usare HTTPS in production

### 429 Too Many Requests

**Causa**: Rate limit superato

**Soluzioni**:
1. Riduci `MaxRequestsPerSecond` in `appsettings.json`
2. Aumenta `CacheTtlDays` per ridurre richieste
3. Attendi e riprova

### Nessun Risultato

**Causa**: Query troppo specifica o gioco non su BGG

**Soluzioni**:
1. Usa `exact=false` per ricerca fuzzy (default)
2. Riduci termini di ricerca
3. Verifica spelling del nome gioco

## Sviluppo Senza Token

Per sviluppo locale senza token BGG:

1. **Usa giochi già nel database**: Endpoint `/api/v1/games` ritorna giochi già importati
2. **Mock service**: Crea mock del BggApiService per testing
3. **Richiedi token di sviluppo**: BGG può fornire token limitato per development

## BGG Files API (Rulebook Fetch)

MeepleAI utilizza anche l'API interna di BGG per scaricare PDF dei regolamenti direttamente da BoardGameGeek.

### Architettura

L'API interna BGG (`api.geekdo.com/api/files`) è un'API JSON non documentata che:
- **Non richiede autenticazione** per l'accesso pubblico ai file
- **Non ha CORS** - funziona solo da server, non da browser
- Restituisce metadati dei file uploadati dagli utenti BGG

### Endpoint API

```
POST /api/v1/bgg/games/{bggId}/rulebook
```

**Request Body**:
```json
{
  "gameId": "uuid",           // ID del gioco in MeepleAI (opzionale)
  "preferredLanguage": "en",  // Lingua preferita: en, it, de, fr, es, etc.
  "documentType": "base",     // Tipo: base, expansion, faq, reference
  "isPublic": false           // Se rendere il documento pubblico
}
```

**Response**:
```json
{
  "success": true,
  "documentId": "uuid",
  "fileName": "wingspan_rules.pdf",
  "title": "Wingspan Rules",
  "fileSizeBytes": 5242880,
  "language": "en",
  "totalRulebooksFound": 15,
  "selectedRulebookScore": 95,
  "bggFilePageUrl": "https://boardgamegeek.com/filepage/12345"
}
```

### Algoritmo di Selezione Rulebook

Il sistema utilizza un algoritmo di scoring per selezionare il miglior regolamento:

1. **Categoria** (priorità massima):
   - `Rules` → score base alto
   - `RulesSummary`, `Reference` → score medio
   - Altre categorie → escluse

2. **Lingua**:
   - Corrispondenza esatta con `preferredLanguage` → +50 punti
   - Fallback su inglese se non trovata

3. **Popolarità**:
   - ThumbsUp e commenti degli utenti BGG

4. **Recency**:
   - File più recenti preferiti

### Tool CLI (Development)

Per testare lo scraper direttamente:

```bash
cd tools/game-scraper

# Installa dipendenze
pnpm install

# Scarica tutti i file per un gioco
pnpm scraper:rulebook 266192  # Wingspan

# Output: tools/game-scraper/output/266192.json
```

### Configurazione

```json
// appsettings.json
{
  "BggFiles": {
    "TimeoutSeconds": 60,
    "MaxRetries": 3,
    "DelayBetweenRequestsMs": 500
  }
}
```

### Rate Limiting

Per rispettare i server BGG:
- Delay di 500ms tra richieste multiple
- Retry con backoff esponenziale
- Cache dei risultati per 24 ore

### Troubleshooting

#### 500 Internal Server Error (dal browser)

**Causa**: CORS bloccato - l'API `api.geekdo.com` non permette richieste cross-origin

**Soluzione**: Le richieste devono passare dal backend, non direttamente dal browser

#### Nessun Regolamento Trovato

**Causa**: Il gioco non ha PDF uploadati su BGG

**Soluzioni**:
1. Verifica manualmente su BGG: `https://boardgamegeek.com/boardgame/{bggId}/files`
2. Alcuni giochi hanno solo immagini, non PDF
3. Prova con lingua diversa (alcuni giochi hanno solo regolamenti in tedesco/francese)

#### File Scaricato Corrotto

**Causa**: Timeout durante download di file grandi

**Soluzioni**:
1. Aumenta `TimeoutSeconds` in configurazione
2. Verifica connessione internet stabile
3. Riprova - il sistema ha retry automatico

## Riferimenti

- **BGG XML API Docs**: https://boardgamegeek.com/wiki/page/BGG_XML_API2
- **Registrazione App**: https://boardgamegeek.com/applications
- **API Usage Guide**: https://boardgamegeek.com/using_the_xml_api
- **BGG Files Page**: https://boardgamegeek.com/boardgame/{bggId}/files

---

**Ultima Modifica**: 2026-01-13
**Versione**: 1.1
