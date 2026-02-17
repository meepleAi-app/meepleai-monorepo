# Appendix G: Sistema di Configurazione Admin RAG

**Data**: 2026-02-02
**Scopo**: Documentare il sistema di configurazione che permette agli admin di sovrascrivere valori stimati con dati misurati.

---

## 1. Panoramica

Il sistema di configurazione RAG permette di:

1. **Override valori stimati** con dati misurati dalla produzione
2. **Aggiornare prezzi modelli** LLM quando cambiano
3. **Visualizzare confronto** estimated vs measured
4. **Calcolare proiezioni costi** basate su parametri configurabili

---

## 2. Architettura

### Frontend

```
apps/web/src/components/rag-dashboard/
├── types.ts                    # Tipi originali (immutabili)
├── types-configurable.ts       # Tipi configurabili (extended)
└── RagConfigurationForm.tsx    # Form admin
```

### Backend API (Proposta)

```
apps/api/src/Api/BoundedContexts/SystemConfiguration/
├── Domain/
│   └── RagConfiguration.cs
├── Application/
│   ├── Commands/
│   │   └── UpdateRagConfigurationCommand.cs
│   └── Queries/
│       └── GetRagConfigurationQuery.cs
└── Infrastructure/
    └── RagConfigurationRepository.cs
```

---

## 3. Tipi di Dato

### ConfigurableValue<T>

Pattern centrale per valori che possono essere stimati o misurati:

```typescript
interface ConfigurableValue<T> {
  /** Valore teorico stimato da documentazione/ricerca */
  estimated: T;
  /** Valore misurato dalla produzione (sovrascrive estimated) */
  measured?: T;
  /** Data ultima misurazione */
  measuredAt?: string; // ISO date
  /** Confidenza statistica (0-1) */
  confidence?: number;
  /** Fonte/metodo di misurazione */
  source?: string;
}
```

### Logica di Risoluzione

```typescript
function getEffectiveValue<T>(config: ConfigurableValue<T>): T {
  return config.measured !== undefined ? config.measured : config.estimated;
}
```

**Regola**: Se `measured` è presente, viene usato. Altrimenti si usa `estimated`.

---

## 4. Configurazioni Supportate

### 4.1 Prezzi Modelli LLM

| Campo | Tipo | Descrizione |
|-------|------|-------------|
| `inputCost` | `ConfigurableValue<number>` | $/1M token input |
| `outputCost` | `ConfigurableValue<number>` | $/1M token output |
| `cacheCost` | `ConfigurableValue<number>` | $/1M token cache (se disponibile) |
| `lastUpdated` | `string` | Data ultimo aggiornamento |
| `pricingSource` | `string` | URL fonte ufficiale prezzi |

### 4.2 Strategie RAG

Per ogni strategia (FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM):

| Campo | Tipo | Descrizione |
|-------|------|-------------|
| `tokens` | `ConfigurableValue<number>` | Token totali per query |
| `cost` | `ConfigurableValue<number>` | Costo USD per query |
| `latency.minMs` | `ConfigurableValue<number>` | Latenza minima ms |
| `latency.maxMs` | `ConfigurableValue<number>` | Latenza massima ms |
| `latency.p50Ms` | `ConfigurableValue<number>` | Latenza P50 (opzionale) |
| `latency.p95Ms` | `ConfigurableValue<number>` | Latenza P95 (opzionale) |
| `accuracy.min` | `ConfigurableValue<number>` | Accuratezza minima (0-1) |
| `accuracy.max` | `ConfigurableValue<number>` | Accuratezza massima (0-1) |
| `accuracy.average` | `ConfigurableValue<number>` | Accuratezza media misurata |

### 4.3 Layer RAG

Per ogni layer (L1-L6):

| Campo | Tipo | Descrizione |
|-------|------|-------------|
| `tokenRange.min` | `ConfigurableValue<number>` | Token minimi |
| `tokenRange.max` | `ConfigurableValue<number>` | Token massimi |
| `latencyRange.minMs` | `ConfigurableValue<number>` | Latenza minima |
| `latencyRange.maxMs` | `ConfigurableValue<number>` | Latenza massima |

### 4.4 Configurazione Globale

| Campo | Tipo | Descrizione |
|-------|------|-------------|
| `tokenDistribution.inputRatio` | `ConfigurableValue<number>` | % token input (default 0.70) |
| `cacheConfig.hitRate` | `ConfigurableValue<number>` | Cache hit rate (default 0.80) |
| `chunkConfig.sizeAvg` | `ConfigurableValue<number>` | Dimensione media chunk |

---

## 5. API Endpoints (Proposta)

### GET /api/v1/admin/rag-configuration

Recupera configurazione corrente.

```http
GET /api/v1/admin/rag-configuration
Authorization: Bearer {admin_token}
```

**Response**:
```json
{
  "schemaVersion": "1.0.0",
  "lastUpdated": "2026-02-02T10:30:00Z",
  "updatedBy": "admin@example.com",
  "global": { ... },
  "modelPricing": [ ... ],
  "strategies": { ... },
  "layers": [ ... ]
}
```

### PUT /api/v1/admin/rag-configuration

Aggiorna configurazione.

```http
PUT /api/v1/admin/rag-configuration
Authorization: Bearer {admin_token}
Content-Type: application/json

{
  "strategies": {
    "FAST": {
      "tokens": {
        "estimated": 2060,
        "measured": 2150,
        "measuredAt": "2026-02-02T10:30:00Z",
        "confidence": 0.95,
        "source": "Production metrics Jan 2026"
      }
    }
  }
}
```

### GET /api/v1/admin/rag-configuration/metrics

Recupera metriche attuali dalla produzione per auto-update.

```http
GET /api/v1/admin/rag-configuration/metrics?period=30d
Authorization: Bearer {admin_token}
```

**Response**:
```json
{
  "period": "30d",
  "strategies": {
    "FAST": {
      "queryCount": 45230,
      "avgTokens": 2145,
      "avgCost": 0.00012,
      "avgLatencyMs": 180,
      "p50LatencyMs": 150,
      "p95LatencyMs": 320,
      "accuracyScore": 0.82
    }
  }
}
```

### POST /api/v1/admin/rag-configuration/apply-metrics

Applica automaticamente metriche misurate alla configurazione.

```http
POST /api/v1/admin/rag-configuration/apply-metrics
Authorization: Bearer {admin_token}
Content-Type: application/json

{
  "period": "30d",
  "strategies": ["FAST", "BALANCED"],
  "fields": ["tokens", "latency", "accuracy"]
}
```

---

## 6. Workflow Admin

### 6.1 Aggiornamento Manuale

```
1. Admin apre /admin/rag-configuration
2. Espande sezione strategia (es. FAST)
3. Inserisce valore "misurato" nel campo desiderato
4. Il campo diventa verde (indica dato misurato attivo)
5. L'anteprima costi si aggiorna automaticamente
6. Admin clicca "Salva"
7. Configurazione persistita nel database
```

### 6.2 Aggiornamento da Metriche

```
1. Admin clicca "Importa Metriche Produzione"
2. Sistema recupera metriche ultimi 30 giorni
3. Admin vede confronto estimated vs measured
4. Admin seleziona quali metriche applicare
5. Sistema aggiorna campi `measured` automaticamente
6. Admin conferma e salva
```

### 6.3 Aggiornamento Prezzi

```
1. Admin riceve notifica cambio prezzi (o verifica manualmente)
2. Apre sezione "Prezzi Modelli LLM"
3. Aggiorna prezzi input/output per modello
4. Inserisce URL fonte ufficiale
5. Salva configurazione
6. Sistema ricalcola tutti i costi
```

---

## 7. Validazione

### Regole di Validazione

```typescript
const validationRules = {
  tokens: { min: 50, max: 100000 },
  cost: { min: 0, max: 10 },
  latency: { min: 0, max: 300000 }, // 5 minuti max
  accuracy: { min: 0, max: 1 },
  inputRatio: { min: 0.1, max: 0.99 },
  cacheHitRate: { min: 0, max: 1 },
};
```

### Coerenza Dati

- `latency.minMs` <= `latency.maxMs`
- `accuracy.min` <= `accuracy.max`
- `tokenDistribution.inputRatio + outputRatio = 1`
- Somma `usagePercent` di tutte le strategie <= 100%

---

## 8. Persistenza

### Opzione A: Database PostgreSQL

```sql
CREATE TABLE rag_configuration (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    schema_version VARCHAR(20) NOT NULL,
    configuration JSONB NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by VARCHAR(255),
    CONSTRAINT valid_json CHECK (jsonb_typeof(configuration) = 'object')
);

CREATE INDEX idx_rag_config_updated ON rag_configuration(updated_at DESC);
```

### Opzione B: File YAML/JSON

```yaml
# config/rag-configuration.yaml
schemaVersion: "1.0.0"
lastUpdated: "2026-02-02T10:30:00Z"

strategies:
  FAST:
    tokens:
      estimated: 2060
      measured: 2150
      measuredAt: "2026-02-02"
```

**Raccomandazione**: Usare database PostgreSQL per audit trail e consistenza.

---

## 9. Audit Trail

Ogni modifica viene loggata:

```typescript
interface ConfigurationAuditEntry {
  id: string;
  timestamp: string;
  userId: string;
  action: 'update' | 'reset' | 'apply_metrics';
  changes: {
    path: string; // e.g., "strategies.FAST.tokens.measured"
    oldValue: unknown;
    newValue: unknown;
  }[];
}
```

---

## 10. Sicurezza

### Permessi Richiesti

| Azione | Ruolo Minimo |
|--------|--------------|
| Visualizzare configurazione | Editor |
| Modificare stime | Admin |
| Inserire valori misurati | Admin |
| Applicare metriche automatiche | Admin |
| Reset configurazione | Admin |

### Rate Limiting

- `GET /rag-configuration`: 100 req/min
- `PUT /rag-configuration`: 10 req/min
- `POST /apply-metrics`: 5 req/min

---

## 11. Integrazione Dashboard

Il form si integra con il RAG Dashboard esistente:

```typescript
// In RagDashboard.tsx
import { RagConfigurationForm } from './RagConfigurationForm';
import { useRagConfiguration } from '@/hooks/useRagConfiguration';

function RagDashboard() {
  const { config, updateConfig, isAdmin } = useRagConfiguration();

  return (
    <>
      {/* Dashboard esistente usa config */}
      <TokenFlowVisualization config={config} />

      {/* Form admin (solo per admin) */}
      {isAdmin && (
        <RagConfigurationForm
          initialConfig={config}
          onSave={updateConfig}
        />
      )}
    </>
  );
}
```

---

## 12. Prossimi Passi

1. **Backend**: Implementare endpoints API in `SystemConfiguration` bounded context
2. **Database**: Creare migration per tabella `rag_configuration`
3. **Frontend**: Integrare form nel dashboard esistente
4. **Metriche**: Collegare Prometheus/Grafana per auto-import metriche
5. **Test**: Creare test per validazione configurazione

---

**Riferimenti**:
- `apps/web/src/components/rag-dashboard/types-configurable.ts`
- `apps/web/src/components/rag-dashboard/RagConfigurationForm.tsx`
- `docs/03-api/rag/appendix/F-calculation-formulas.md`
