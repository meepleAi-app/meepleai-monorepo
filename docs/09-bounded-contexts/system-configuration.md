# SystemConfiguration Bounded Context

**Runtime configuration, feature flags, environment settings**

---

## 📋 Responsabilità

- Auto-configuration system (ADR-021)
- Feature flags (enable/disable features at runtime)
- Environment-specific settings (dev, staging, prod)
- Configuration validation e health checks

---

## 🏗️ Domain Model

**ConfigurationEntry**:
```csharp
public class ConfigurationEntry
{
    public string Key { get; private set; }
    public string Value { get; private set; }
    public ConfigScope Scope { get; private set; }  // Global | User | Game
    public bool IsSecret { get; private set; }

    public void Update(string newValue) { }
}
```

---

## 📡 Application Layer

### Commands
- `UpdateConfigCommand`
- `ResetConfigCommand`

### Queries
- `GetAllConfigQuery`
- `GetConfigByKeyQuery`

---

## ⚙️ Auto-Configuration (ADR-021)

**Strategy**: Automatic environment detection and configuration

```csharp
var config = AutoConfigurationBuilder
    .Detect(environment)
    .ConfigureServices()
    .ConfigureDatabase()
    .ConfigureCache()
    .Build();
```

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/SystemConfiguration/`

---

## 📖 Related Documentation

- [ADR-021: Auto Configuration System](../01-architecture/adr/adr-021-auto-configuration-system.md)
- [Deployment Auto-Config Guide](../04-deployment/auto-configuration-guide.md)

---

**Status**: ✅ Production
**Last Updated**: 2026-01-18
