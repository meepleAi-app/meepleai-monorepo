# Security Documentation

**Quick Navigation** - Trova rapidamente le risorse di sicurezza

---

## 📂 File in questa Sezione

| File | Descrizione | Pubblico | Ultimo Aggiornamento |
|------|-------------|----------|----------------------|
| `owasp-top-10-compliance.md` | OWASP Top 10 compliance matrix e mitigations | Developer / Security | 2026-01-18 |
| _More security guides coming_ | Penetration testing, incident response, etc. | Security | Future |

---

## 🔍 Trova per Scenario

**Se vuoi...** | **Leggi questo file**
---|---
Verificare OWASP compliance | `owasp-top-10-compliance.md`
Gestire i secret | `../deployment/secrets-management.md`
Configurare autenticazione | `../bounded-contexts/authentication.md`
Implementare OAuth/2FA | `../testing/backend/oauth-testing.md`
Security headers middleware | `../architecture/adr/adr-010-security-headers-middleware.md`
CORS configuration | `../architecture/adr/adr-011-cors-whitelist-headers.md`

---

## 🛡️ Security Checklist

**Backend**:
- [ ] Secret management configurato (`infra/secrets/*.secret`)
- [ ] FluentValidation su tutti i Command/Query
- [ ] Input sanitization (XSS, SQL injection)
- [ ] Rate limiting configurato (Redis)
- [ ] CORS policy whitelist corretta
- [ ] Security headers middleware attivo (CSP, HSTS, etc.)

**Frontend**:
- [ ] Nessun secret committato nel codice
- [ ] API calls con credentials: "include"
- [ ] XSS protection via sanitization
- [ ] HTTPS obbligatorio in produzione

**Infrastructure**:
- [ ] Firewall configurato (UFW/iptables)
- [ ] Fail2Ban attivo per SSH
- [ ] SSL/TLS certificati validi
- [ ] Database password rotate ogni 90 giorni

---

## 📖 Guide Correlate

- [Secrets Management](../deployment/secrets-management.md)
- [OAuth Testing Guide](../testing/backend/oauth-testing.md)
- [Deployment Security](../deployment/infrastructure-deployment-checklist.md)
- [ADR-010: Security Headers Middleware](../architecture/adr/adr-010-security-headers-middleware.md)
- [ADR-011: CORS Whitelist](../architecture/adr/adr-011-cors-whitelist-headers.md)

---

## 🚨 Security Incident Response

**Se rilevi una vulnerabilità**:
1. **NON committare fix immediatamente** (evita disclosure pubblica)
2. Documentare in privato (file locale, non committato)
3. Notificare team security
4. Preparare patch
5. Applicare patch + disclosure coordinata

**Risorse**:
- Security Team: [contact info]
- Vulnerability Report Template: _To be created_

---

**Last Updated**: 2026-01-18
**Maintainers**: Security Team
**Status**: 🚧 In Development
