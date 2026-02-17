# Workflow Integration - Flussi API

## Panoramica

Il bounded context Workflow Integration gestisce l'integrazione con n8n per automazione workflow, template e logging errori.

---

## 1. n8n Configuration

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/n8n` | `GetAllN8NConfigsQuery` | — | `[A]` |
| GET | `/admin/n8n/{configId}` | `GetN8NConfigByIdQuery` | — | `[A]` |
| POST | `/admin/n8n` | `CreateN8NConfigCommand` | CreateN8NConfigRequest | `[A]` |
| PUT | `/admin/n8n/{configId}` | `UpdateN8NConfigCommand` | UpdateN8NConfigRequest | `[A]` |
| DELETE | `/admin/n8n/{configId}` | `DeleteN8NConfigCommand` | — | `[A]` |
| POST | `/admin/n8n/{configId}/test` | `TestN8NConnectionCommand` | — | `[A]` |

---

## 2. n8n Templates

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| GET | `/n8n/templates` | `GetN8NTemplatesQuery` | `category?` | `[S]` |
| GET | `/n8n/templates/{id}` | `GetN8NTemplateByIdQuery` | — | `[S]` |
| POST | `/n8n/templates/{id}/import` | `ImportN8NTemplateCommand` | ImportTemplateRequest | `[S]` |
| POST | `/n8n/templates/validate` | `ValidateN8NTemplateQuery` | ValidateTemplateRequest | `[S]` |

---

## 3. Workflow Error Logging

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| POST | `/logs/workflow-error` | `LogWorkflowErrorCommand` | LogWorkflowErrorRequest | `[P]` (webhook) |
| GET | `/admin/workflows/errors` | `GetWorkflowErrorsQuery` | `workflowId?, fromDate?, toDate?, page?, limit?` | `[A]` |
| GET | `/admin/workflows/errors/{id}` | `GetWorkflowErrorByIdQuery` | — | `[A]` |

---

## Flusso Setup n8n Integration

```
1. Config:    POST /admin/n8n { url, apiKey, name }
                    │
                    ▼ { configId }
                    │
2. Test:      POST /admin/n8n/{configId}/test
                    │
                    ▼ { success: true, latency: "45ms" }
                    │
3. Template:  GET /n8n/templates?category=notifications
                    │
                    ▼ [template1, template2, ...]
                    │
4. Import:    POST /n8n/templates/{id}/import
                    │
                    ▼ Workflow attivato in n8n
```

---

## Flusso Error Monitoring

```
n8n Workflow Error → POST /logs/workflow-error (webhook, no auth)
                          │
                          ▼ Error logged
                          │
Admin:              GET /admin/workflows/errors?fromDate=2026-02-01
                          │
                          ▼ Lista errori con dettagli
                          │
                    GET /admin/workflows/errors/{id}
                          │
                          ▼ Dettaglio singolo errore
```

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 134 |
| **Passati** | 134 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | <1s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| n8n Configuration | `CreateN8NConfigTests.cs`, `UpdateConfigTests.cs`, `DeleteConfigTests.cs` | Passato |
| n8n Connection Test | `TestN8NConnectionTests.cs` | Passato |
| Templates | `GetTemplatesTests.cs`, `ImportTemplateTests.cs`, `ValidateTemplateTests.cs` | Passato |
| Error Logging | `LogWorkflowErrorTests.cs`, `GetWorkflowErrorsTests.cs` | Passato |
| Domain Entities | Workflow, WorkflowExecution (5 file) | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*
