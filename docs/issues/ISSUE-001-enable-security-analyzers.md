Title: Abilitare e risolvere i warning di sicurezza e le regole CA3xxx/Sxxxx

Description:
Abilitare e correggere tutti i warning di sicurezza critici (CA3xxx, CA53xx, S2xxx) che sono attualmente soppressi o ignorati.
Queste regole sono bloccanti per la produzione e devono essere affrontate prima di rilasciare codice.

Why:
- Correggere vulnerabilità come SQL injection, deserialization, credential leakage, e uso di crittografia debole.

Scope:
- Rimuovere le soppressioni globali per le regole di sicurezza.
- Correggere le violazioni o aggiungere giustificazioni e test di mitigazione.
- Assicurare che build in CI falliscano su violazioni critiche.

Tasks:
- [ ] Inventario completo delle istanze (file, regola) — output: warnings_tracking.csv
- [ ] Creare PR per gruppi di regole (es. CA21xx, CA53xx, S2068, S3649)
- [ ] Applicare fix o mitigazioni e aggiungere test
- [ ] Aggiornare documentazione e mark as resolved

Priority: P0
Assignee: Unassigned

Created: 2025-12-14T09:29:30.963Z
