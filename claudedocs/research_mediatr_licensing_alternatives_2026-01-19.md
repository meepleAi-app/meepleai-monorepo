# MediatR Licensing e Alternative Open Source - Report di Ricerca

**Data**: 2026-01-19
**Query**: MediatR non è utilizzabile commercialmente gratis. Quali sono i costi e ci sono alternative open source adatte alla produzione?

---

## Executive Summary

MediatR ha adottato un modello **dual-license** (RPL-1.5 + commerciale) a partire da luglio 2025. Le versioni ≥13.0 richiedono una licenza commerciale per aziende sopra determinate soglie. Esistono **eccellenti alternative MIT-licensed** completamente gratuite per uso commerciale, con alcune che offrono prestazioni superiori grazie ai source generator.

---

## 1. MediatR - Cambiamento di Licenza

### Cronologia
- **Aprile 2025**: Annuncio del passaggio a modello commerciale
- **2 Luglio 2025**: Lancio ufficiale edizione commerciale (v13.0+)
- **Versioni precedenti**: Rimangono sotto licenza MIT (archiviate)

### Nuovo Modello di Licenza
- **RPL-1.5** (Reciprocal Public License): Licenza copyleft che richiede la condivisione del codice se si distribuisce software che usa MediatR
- **Licenza Commerciale**: Per uso proprietario/chiuso

---

## 2. Prezzi MediatR Commerciale

| Tier | Sviluppatori | Prezzo Annuale | Prezzo Mensile |
|------|--------------|----------------|----------------|
| **Standard** | 1-10 | €799/anno | €80/mese |
| **Professional** | 11-50 | €1.499/anno | €150/mese |
| **Enterprise** | Illimitati | €6.399/anno | €640/mese |

### Cosa Include (tutti i tier)
- Accesso NuGet e feed privato
- Server Discord
- Early releases
- Deploy illimitati

### Enterprise aggiunge
- Bug fix prioritari
- Supporto email

---

## 3. Community Edition (Gratuita)

### Requisiti di Eleggibilità
| Criterio | Soglia |
|----------|--------|
| Fatturato annuo lordo | < $5.000.000 USD |
| Capitale da fonti esterne (VC, PE) | < $10.000.000 USD |
| Non-profit budget annuale | < $5.000.000 USD |
| Agenzie governative | ❌ Non idonee |

⚠️ **Nota**: Serve comunque registrazione per license key (solo audit)

---

## 4. Alternative Open Source (MIT License)

### 4.1 Mediator (martinothamar) ⭐ RACCOMANDATO

**GitHub**: [martinothamar/Mediator](https://github.com/martinothamar/Mediator)
**Licenza**: MIT
**Stars**: 3.300+
**Versione**: 3.0.x

#### Caratteristiche Chiave
- ✅ **Source Generator**: Nessuna reflection runtime
- ✅ **Native AOT Support**: Compatibile con AOT completo
- ✅ **API simile a MediatR**: Curva di apprendimento minima
- ✅ **Build-time error detection**: Errori catturati in compilazione
- ✅ **Prestazioni superiori**: Benchmark dimostrano miglioramenti significativi
- ✅ **ValueTask<T>**: Meno allocazioni

#### Benchmark vs MediatR
- Cold-start significativamente più veloce
- Request handling più performante
- Notifiche più veloci
- Streaming ottimizzato

#### Limitazioni
- Progetto più giovane di MediatR
- Community più piccola

---

### 4.2 Wolverine ⭐ ECCELLENTE PER PROGETTI COMPLESSI

**GitHub**: [JasperFx/wolverine](https://github.com/JasperFx/wolverine)
**Licenza**: MIT
**Stars**: 1.900+
**Versione**: 5.10.0 (Gennaio 2026)

#### Caratteristiche Chiave
- ✅ **Molto più di un mediator**: Message bus completo
- ✅ **Source generators**: Alte prestazioni
- ✅ **Durable inbox/outbox**: Messaggi garantiti
- ✅ **Retry, scheduling, sagas**: Built-in
- ✅ **Integrazione Marten**: "Critter Stack" per event sourcing
- ✅ **Supporto professionale disponibile**: JasperFx Software

#### Casi d'Uso Ideali
- Microservizi con messaging
- Event sourcing
- Sistemi con requisiti di durabilità
- Workflow/Saga orchestration

#### Limitazioni
- Più complesso se serve solo mediator semplice
- Curva di apprendimento più ripida

---

### 4.3 Brighter

**GitHub**: [BrighterCommand/Brighter](https://github.com/BrighterCommand/Brighter)
**Licenza**: MIT
**Stars**: 2.000+
**Versione**: 10.0.5

#### Caratteristiche Chiave
- ✅ **Command Processor pattern**: Pipeline "Russian doll"
- ✅ **Task Queues**: RabbitMQ, SNS+SQS
- ✅ **Middleware built-in**: Logging, Polly (retry, circuit breaker)
- ✅ **Progetto maturo**: Esiste da prima di MediatR
- ✅ **CLA che garantisce FOSS**: Committed a rimanere open source

#### Casi d'Uso Ideali
- Messaging tra microservizi
- Sistemi distribuiti
- Ports & Adapters architecture

---

### 4.4 Altre Alternative

| Libreria | Licenza | Focus | Note |
|----------|---------|-------|------|
| **LiteBus** | MIT | CQS lightweight | Streaming support, interfacce esplicite |
| **Cortex.Mediator** | MIT | CQRS + UnitOfWork | IUnitOfWork built-in |
| **DispatchR** | MIT | Zero-allocation | Performance estrema |
| **SwitchMediator** | MIT | AOT-friendly | Zero reflection, FrozenDictionary |
| **Concordia** | MIT | Source generators | Handler registration compile-time |

---

## 5. Comparativa Finale

| Criterio | MediatR Comm. | Mediator | Wolverine | Brighter |
|----------|---------------|----------|-----------|----------|
| **Licenza** | Commerciale | MIT | MIT | MIT |
| **Costo** | €799-6.399/anno | Gratis | Gratis | Gratis |
| **Performance** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ |
| **API MediatR-like** | ✅ | ✅ | Parziale | ❌ |
| **Source Generator** | ❌ | ✅ | ✅ | ❌ |
| **AOT Support** | ❌ | ✅ | ✅ | ❌ |
| **Message Bus** | ❌ | ❌ | ✅ | ✅ |
| **Maturità** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Community** | Grande | Media | Media | Media |

---

## 6. Raccomandazioni per MeepleAI

### Scenario: Progetto esistente con MediatR

**Opzioni**:

1. **Mantenere MediatR (v12.x MIT)**:
   - Le versioni precedenti restano MIT
   - Nessuna nuova feature/fix
   - Rischio tecnico a lungo termine

2. **Community License** (se eleggibile):
   - Fatturato < $5M e capitale esterno < $10M
   - Gratuito con registrazione
   - Upgrade automatico se si supera soglia

3. **Migrare a Mediator (martinothamar)** ⭐ CONSIGLIATO:
   - API molto simile → migrazione facile
   - Prestazioni migliori
   - Native AOT ready
   - MIT perpetua

4. **Migrare a Wolverine** (se serve più di mediator):
   - Per evolvere verso message bus
   - Se si prevede event sourcing con Marten

### Costo di Migrazione Stimato

| Da | A | Effort |
|----|---|--------|
| MediatR | Mediator | Basso (API simile) |
| MediatR | Wolverine | Medio (API diversa) |
| MediatR | Brighter | Alto (paradigma diverso) |

---

## 7. Fonti

### Licenze e Prezzi
- [MediatR Official](https://mediatr.io/)
- [Jimmy Bogard Announcement](https://www.jimmybogard.com/automapper-and-mediatr-licensing-update/)
- [Commercial Launch](https://www.jimmybogard.com/automapper-and-mediatr-commercial-editions-launch-today/)

### Alternative
- [Mediator GitHub](https://github.com/martinothamar/Mediator)
- [Wolverine GitHub](https://github.com/JasperFx/wolverine)
- [Brighter GitHub](https://github.com/BrighterCommand/Brighter)
- [Milan Jovanovic Analysis](https://www.milanjovanovic.tech/blog/mediatr-and-masstransit-going-commercial-what-this-means-for-you)

### Tutorial Migrazione
- [TheCodeMan Wolverine Alternative](https://thecodeman.net/posts/mediatr-alternative-wolverine)
- [Cortex.Mediator](https://medium.com/@eneshoxha_65350/cortex-mediator-a-free-open-source-alternative-to-mediatr-for-cqrs-in-net-59534e1305c7)

---

## Confidence Score

| Aspetto | Confidenza |
|---------|------------|
| Prezzi MediatR | 95% (fonte ufficiale) |
| Licenze alternative | 95% (GitHub verificato) |
| Comparativa prestazioni | 80% (benchmark disponibili) |
| Raccomandazioni migrazione | 85% (basato su documentazione) |

---

*Report generato con SuperClaude Deep Research Mode*
