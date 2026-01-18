# WorkflowIntegration Bounded Context

**n8n workflows, webhooks, error logging**

---

## 📋 Responsabilità

- n8n workflow orchestration
- Webhook management (inbound/outbound)
- Integration error logging
- External service connectors

---

## 🏗️ Domain Model

**Webhook**:
```csharp
public class Webhook
{
    public Guid Id { get; private set; }
    public string Url { get; private set; }
    public List<string> Events { get; private set; }
    public string Secret { get; private set; }       // HMAC signing
    public bool IsActive { get; private set; }

    public void Activate() { }
    public void Deactivate() { }
}
```

**WorkflowError**:
```csharp
public class WorkflowError
{
    public Guid Id { get; private set; }
    public string WorkflowId { get; private set; }
    public string ErrorMessage { get; private set; }
    public DateTime OccurredAt { get; private set; }
}
```

---

## 📡 Application Layer

### Commands
- `CreateWebhookCommand`
- `DeleteWebhookCommand`
- `TriggerWorkflowCommand`

### Queries
- `GetWebhooksQuery`
- `GetWorkflowErrorsQuery`

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/WorkflowIntegration/`

---

**Status**: 🚧 Beta
**Last Updated**: 2026-01-18
