# Data Breach Notification Process

**GDPR Articles 33 and 34 -- Breach Notification Runbook**

| Field              | Value                                                        |
|--------------------|--------------------------------------------------------------|
| Document owner     | Aaron Degrassi, Data Controller                              |
| Effective date     | 2026-03-10                                                   |
| Next review        | 2027-03-10 (annual) or after any breach incident             |
| Classification     | Internal -- Restricted                                       |

---

## 1. Scope

This document applies to all personal data breaches as defined by GDPR Art. 4(12) that affect MeepleAI users. A personal data breach is any breach of security leading to the accidental or unlawful destruction, loss, alteration, unauthorized disclosure of, or access to, personal data transmitted, stored, or otherwise processed.

This process covers breaches originating from:

- MeepleAI application code and infrastructure
- Sub-processor environments (Hetzner Online GmbH)
- Third-party integrations (OpenRouter, BoardGameGeek)

All team members with access to production systems must be familiar with this document.

---

## 2. Breach Classification

| Severity     | Criteria                                                                                     | Example                                                   |
|--------------|----------------------------------------------------------------------------------------------|-----------------------------------------------------------|
| **Low**      | No personal data exposed; technical incident only                                            | Failed brute-force attempt blocked by rate limiting        |
| **Medium**   | Limited PII exposure (e.g., email addresses); low number of data subjects (fewer than 100)   | Database query log exposing email addresses                |
| **High**     | Sensitive data exposed (auth tokens, password hashes) or large-scale PII exposure (100+)     | JWT signing key compromise; bulk user record exfiltration  |
| **Critical** | Special category data exposed, or data enabling identity theft or financial fraud             | Payment credentials leaked; health-related data disclosed  |

Classification determines the urgency of the response and whether supervisory authority and data subject notification is required.

- **Low**: Record internally. No notification required unless reassessed.
- **Medium**: Notify supervisory authority unless risk to rights and freedoms is unlikely.
- **High**: Notify supervisory authority. Notify data subjects.
- **Critical**: Notify supervisory authority. Notify data subjects immediately. Engage legal counsel.

---

## 3. Detection Sources

Breaches may be detected through the following channels:

1. **Hetzner notification** -- Per DPA Section 5.8, Hetzner Online GmbH is contractually required to notify MeepleAI immediately upon becoming aware of a breach affecting our data.
2. **Application monitoring and alerting** -- Background services such as `OpenRouterRpmAlertBackgroundService`, `BudgetAlertBackgroundService`, and `CircuitBreakerStateChangedEvent` handlers may surface anomalous activity.
3. **Security audit findings** -- Periodic reviews of access logs, infrastructure configuration, and code audits.
4. **User reports** -- Data subjects reporting suspicious account activity or unauthorized access.
5. **Third-party disclosure** -- Vulnerability researchers, partner notifications, or public disclosure of a vulnerability affecting MeepleAI dependencies.

Any team member who suspects a breach must immediately notify the Data Controller (Aaron Degrassi) and must not attempt remediation independently unless the breach is actively in progress and containment is time-critical.

---

## 4. Response Timeline (72-Hour Protocol)

GDPR Art. 33(1) requires notification to the supervisory authority without undue delay and, where feasible, not later than 72 hours after becoming aware of a breach. The following protocol breaks this window into four phases.

### Phase 1: Containment and Assessment (Hour 0 to Hour 4)

**Objective**: Stop the breach and establish its boundaries.

Actions:

1. **Contain the breach immediately.**
   - Revoke all active sessions for affected users (`/api/v1/auth/revoke-all-sessions`).
   - Rotate compromised credentials (JWT signing keys, database passwords, API keys).
   - Isolate affected systems. If the breach involves infrastructure, contact Hetzner support at +49 9831 505-0.
   - If the breach involves OpenRouter API keys, disable the key in the OpenRouter dashboard and update `infra/secrets/openrouter.secret`.

2. **Assess scope.**
   - What categories of personal data are affected?
   - How many data subjects are potentially affected?
   - Is the breach ongoing or has it been contained?
   - What is the geographic scope (EU/EEA data subjects)?

3. **Create the initial breach record.**
   - Log in the application AuditLog with action `GdprBreachDetected`.
   - Record: timestamp of detection, detection source, initial classification, and responder identity.

4. **Notify the internal team.**
   - Contact the Data Controller directly.
   - Establish a dedicated communication channel for the incident response.

**Deliverable**: Initial Breach Assessment form completed (see Section 7 for required fields).

### Phase 2: Investigation (Hour 4 to Hour 24)

**Objective**: Determine root cause and assess risk to data subjects.

Actions:

1. **Root cause analysis.**
   - Review application logs, database audit trails, and infrastructure access logs.
   - Identify the attack vector or failure mode.
   - Determine whether the vulnerability has been fully remediated or requires further action.

2. **Risk assessment.**
   - Evaluate the likelihood that the exposed data can be used to harm data subjects.
   - Consider: data sensitivity, volume, whether data was encrypted, whether the breach was exploited by a malicious actor.
   - Apply the ENISA risk assessment methodology or equivalent structured approach.

3. **Prepare breach documentation per Art. 33(3).**

   The following information must be compiled:

   | Art. 33(3) Requirement | Description                                                         |
   |------------------------|---------------------------------------------------------------------|
   | (a) Nature of breach   | Description of the breach including categories and approximate number of data subjects and personal data records concerned |
   | (b) DPO contact        | Name and contact details of the data protection officer or other contact point |
   | (c) Consequences       | Description of the likely consequences of the breach                |
   | (d) Measures           | Description of the measures taken or proposed to address the breach, including measures to mitigate possible adverse effects |

**Deliverable**: Completed breach documentation package.

### Phase 3: Decision and Notification Preparation (Hour 24 to Hour 48)

**Objective**: Determine notification obligations and prepare submissions.

Actions:

1. **Supervisory authority notification decision.**
   - Notification to the Garante per la protezione dei dati personali is **required** unless the breach is unlikely to result in a risk to the rights and freedoms of natural persons.
   - When in doubt, notify. Document the rationale for any decision not to notify.

2. **Data subject notification decision.**
   - Notification to data subjects is **required** under Art. 34 if the breach is likely to result in a **high risk** to their rights and freedoms.
   - Exceptions apply if: appropriate technical measures rendered the data unintelligible (e.g., encryption), subsequent measures ensure the high risk is no longer likely to materialize, or notification would involve disproportionate effort (in which case, a public communication is required).

3. **Prepare notification documents.**
   - Draft the supervisory authority notification form.
   - Draft the data subject notification (email template and in-app notification content).
   - Have the Controller review and approve all notification text.

**Deliverable**: Approved notification documents ready for submission.

### Phase 4: Notification (Hour 48 to Hour 72)

**Objective**: Submit notifications within the 72-hour deadline.

Actions:

1. **Notify the Garante per la protezione dei dati personali** (if required).
   - Submit online via https://www.garanteprivacy.it/
   - Include all Art. 33(3) information compiled in Phase 2.
   - If full information is not yet available, provide what is available and supply the remainder in phases per Art. 33(4).

2. **Notify affected data subjects** (if required per Art. 34).
   - Send email notifications to all affected users using their registered email addresses.
   - Send in-app notifications using the `GdprBreachAlert` notification type through the UserNotifications bounded context.
   - Notification must describe in clear and plain language: the nature of the breach, the DPO or contact point, the likely consequences, and the measures taken.

3. **Record the notification.**
   - Log in the application AuditLog with action `GdprBreachNotified`.
   - Record: notification recipients, method of notification, timestamp, and content summary.

**Deliverable**: Confirmation of all notifications sent and logged.

---

## 5. Hetzner-Specific Procedures

MeepleAI infrastructure is hosted on Hetzner Cloud (data center locations: Germany and Finland). Hetzner Online GmbH acts as data processor under a Data Processing Agreement.

### Breach originating at Hetzner infrastructure level

Per DPA Section 5.8, Hetzner is contractually required to notify MeepleAI immediately upon becoming aware of a personal data breach. Hetzner holds ISO 27001 and BSI C5 Type 2 certifications.

When Hetzner notifies us of a breach:

1. **Acknowledge receipt** of the notification to Hetzner DPO.
2. **Request the following information** from Hetzner (if not already provided):
   - Description of the breach
   - Categories and approximate numbers of data subjects and records affected
   - Likely consequences of the breach
   - Remediation measures taken or proposed by Hetzner
3. **Assess the impact** on MeepleAI users using the information provided.
4. **Proceed with the 72-hour protocol** described in Section 4. The 72-hour clock starts when MeepleAI becomes aware of the breach, not when Hetzner detected it.

### Contacting Hetzner

| Purpose                | Contact                                        |
|------------------------|------------------------------------------------|
| Data protection issues | data-protection@hetzner.com                    |
| DPO phone              | +49 9831 505-216                               |
| General support        | +49 9831 505-0                                 |

### Responsibility boundaries

- **Hetzner** is responsible for: physical infrastructure security, network-level protection, hypervisor isolation, and notifying MeepleAI of breaches within their scope.
- **MeepleAI** is responsible for: application-level security, data subject notification, supervisory authority notification, and all GDPR controller obligations.

---

## 6. Post-Incident Review (Within 7 Days)

A post-incident review must be completed within 7 calendar days of breach containment.

### Required outputs

1. **Root cause analysis document.**
   - Technical description of the vulnerability or failure.
   - Timeline of events from initial compromise to detection to containment.
   - Assessment of detection delay (if any) and contributing factors.

2. **Remediation actions with deadlines.**
   - Each action must have an assigned owner and a completion deadline.
   - Actions must address both the immediate vulnerability and any systemic weaknesses.

3. **Technical and organizational measures (TOMs) update.**
   - Review whether existing TOMs were adequate.
   - Update TOMs where gaps are identified.

4. **Data Protection Impact Assessment (DPIA) update.**
   - If the breach reveals risks not covered by the existing DPIA, update the risk assessment.

5. **Team debrief.**
   - Conduct a retrospective with all involved personnel.
   - Document lessons learned and process improvements.
   - Update this document if procedural gaps were identified.

---

## 7. Record-Keeping (Art. 33(5))

All breaches must be recorded regardless of whether they require notification to the supervisory authority or data subjects. This obligation is absolute under Art. 33(5).

### Breach register fields

Each breach record must contain the following information:

| Field                           | Description                                                     |
|---------------------------------|-----------------------------------------------------------------|
| Breach ID                       | Unique identifier (format: `BREACH-YYYY-NNN`)                  |
| Date and time of detection      | When MeepleAI became aware of the breach                        |
| Date and time of containment    | When the breach was fully contained                             |
| Detection source                | How the breach was detected (see Section 3)                     |
| Description of breach           | Nature, scope, and method of the breach                         |
| Data categories affected        | Types of personal data involved                                 |
| Number of data subjects         | Approximate count of affected individuals                       |
| Classification                  | Low, Medium, High, or Critical (see Section 2)                  |
| Risk assessment                 | Likelihood and severity of harm to data subjects                |
| Supervisory authority notified  | Yes/No, with date and rationale                                 |
| Data subjects notified          | Yes/No, with date, method, and rationale                        |
| Remediation measures            | Actions taken to address the breach and prevent recurrence      |
| Audit log entries               | References to `GdprBreachDetected` and `GdprBreachNotified` log entries |
| Post-incident review completed  | Date of completion and document reference                       |

### Storage

Breach records must be retained for a minimum of 5 years and stored securely with access limited to the Data Controller and authorized personnel.

---

## 8. Contact Information

| Role                          | Name / Organization                              | Email                          | Phone               |
|-------------------------------|--------------------------------------------------|--------------------------------|----------------------|
| Data Controller               | Aaron Degrassi                                   | aaron@meepleai.com             | --                   |
| Processor DPO (Hetzner)       | Hetzner Online GmbH                              | data-protection@hetzner.com    | +49 9831 505-216     |
| Hetzner General Support       | Hetzner Online GmbH                              | --                             | +49 9831 505-0       |
| Supervisory Authority         | Garante per la protezione dei dati personali     | protocollo@gpdp.it             | +39 06 696771        |
| Garante Online Portal         | --                                               | https://www.garanteprivacy.it/ | --                   |

### Controller address

Aaron Degrassi
Via Giuseppe Verdi 78
33050 Perteole (UD)
Italy

---

## 9. Review Schedule

This document must be reviewed:

- **Annually**, with the next scheduled review on **2027-03-10**.
- **After any breach incident**, as part of the post-incident review (Section 6).
- **When significant changes occur** to MeepleAI infrastructure, processing activities, or applicable regulations.

Changes to this document must be approved by the Data Controller and recorded with a version history entry.

| Version | Date       | Author          | Description            |
|---------|------------|-----------------|------------------------|
| 1.0     | 2026-03-10 | Aaron Degrassi  | Initial version        |
