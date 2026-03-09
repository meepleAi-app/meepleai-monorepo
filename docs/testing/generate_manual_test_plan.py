"""
MeepleAI Pre-Launch Manual Test Plan Generator
Generates a comprehensive PDF checklist for manual testing before domain purchase and hosting.
"""

from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import mm, cm
from reportlab.lib.colors import HexColor, black, white
from reportlab.lib.enums import TA_LEFT, TA_CENTER, TA_RIGHT
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    PageBreak, KeepTogether, HRFlowable, ListFlowable, ListItem
)
from reportlab.pdfgen import canvas
from datetime import datetime
import os

# ── Colors ──────────────────────────────────────────────────────────────
AMBER_PRIMARY = HexColor("#F59E0B")
AMBER_DARK = HexColor("#B45309")
AMBER_LIGHT = HexColor("#FEF3C7")
AMBER_BG = HexColor("#FFFBEB")
SLATE_DARK = HexColor("#1E293B")
SLATE_MED = HexColor("#475569")
SLATE_LIGHT = HexColor("#94A3B8")
GREEN = HexColor("#10B981")
GREEN_BG = HexColor("#D1FAE5")
RED = HexColor("#EF4444")
RED_BG = HexColor("#FEE2E2")
BLUE = HexColor("#3B82F6")
BLUE_BG = HexColor("#DBEAFE")
PURPLE = HexColor("#8B5CF6")
PURPLE_BG = HexColor("#EDE9FE")
WHITE = HexColor("#FFFFFF")
GRAY_BG = HexColor("#F8FAFC")
BORDER_GRAY = HexColor("#E2E8F0")

# ── Custom Styles ───────────────────────────────────────────────────────
def get_styles():
    styles = getSampleStyleSheet()

    styles.add(ParagraphStyle(
        'DocTitle', parent=styles['Title'],
        fontSize=28, leading=34, textColor=SLATE_DARK,
        fontName='Helvetica-Bold', spaceAfter=6
    ))
    styles.add(ParagraphStyle(
        'DocSubtitle', parent=styles['Normal'],
        fontSize=14, leading=18, textColor=SLATE_MED,
        fontName='Helvetica', spaceAfter=20
    ))
    styles.add(ParagraphStyle(
        'SectionTitle', parent=styles['Heading1'],
        fontSize=18, leading=22, textColor=AMBER_DARK,
        fontName='Helvetica-Bold', spaceBefore=24, spaceAfter=12,
        borderWidth=0, borderPadding=0
    ))
    styles.add(ParagraphStyle(
        'SubSection', parent=styles['Heading2'],
        fontSize=14, leading=18, textColor=SLATE_DARK,
        fontName='Helvetica-Bold', spaceBefore=16, spaceAfter=8
    ))
    styles.add(ParagraphStyle(
        'SubSubSection', parent=styles['Heading3'],
        fontSize=12, leading=15, textColor=SLATE_MED,
        fontName='Helvetica-Bold', spaceBefore=10, spaceAfter=6
    ))
    styles.add(ParagraphStyle(
        'TestItem', parent=styles['Normal'],
        fontSize=10, leading=14, textColor=SLATE_DARK,
        fontName='Helvetica', leftIndent=6, spaceAfter=2
    ))
    styles.add(ParagraphStyle(
        'TestNote', parent=styles['Normal'],
        fontSize=9, leading=12, textColor=SLATE_MED,
        fontName='Helvetica-Oblique', leftIndent=18, spaceAfter=4
    ))
    styles.add(ParagraphStyle(
        'Priority', parent=styles['Normal'],
        fontSize=9, leading=12, fontName='Helvetica-Bold'
    ))
    styles.add(ParagraphStyle(
        'Footer', parent=styles['Normal'],
        fontSize=8, leading=10, textColor=SLATE_LIGHT,
        fontName='Helvetica', alignment=TA_CENTER
    ))
    styles.add(ParagraphStyle(
        'CellNormal', parent=styles['Normal'],
        fontSize=9, leading=12, textColor=SLATE_DARK, fontName='Helvetica'
    ))
    styles.add(ParagraphStyle(
        'CellBold', parent=styles['Normal'],
        fontSize=9, leading=12, textColor=SLATE_DARK, fontName='Helvetica-Bold'
    ))
    styles.add(ParagraphStyle(
        'SmallNote', parent=styles['Normal'],
        fontSize=8, leading=10, textColor=SLATE_LIGHT, fontName='Helvetica'
    ))
    return styles

# ── Page Template ───────────────────────────────────────────────────────
class NumberedCanvas(canvas.Canvas):
    def __init__(self, *args, **kwargs):
        canvas.Canvas.__init__(self, *args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self._saved_page_states)
        for state in self._saved_page_states:
            self.__dict__.update(state)
            self.draw_page_number(num_pages)
            canvas.Canvas.showPage(self)
        canvas.Canvas.save(self)

    def draw_page_number(self, page_count):
        self.setFont("Helvetica", 8)
        self.setFillColor(SLATE_LIGHT)
        self.drawRightString(
            A4[0] - 20*mm, 12*mm,
            f"Pagina {self._pageNumber} di {page_count}"
        )
        self.drawString(20*mm, 12*mm, "MeepleAI - Manual Test Plan")
        # Header line
        self.setStrokeColor(AMBER_PRIMARY)
        self.setLineWidth(1.5)
        self.line(20*mm, A4[1] - 18*mm, A4[0] - 20*mm, A4[1] - 18*mm)


# ── Helper Functions ────────────────────────────────────────────────────
def make_checkbox_table(items, styles, priority="MEDIUM", category=""):
    """Create a checklist table with checkboxes."""
    data = []
    for item in items:
        if isinstance(item, tuple):
            test_desc, note = item
        else:
            test_desc, note = item, ""

        # Escape XML special chars
        def esc(s):
            return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

        checkbox = Paragraph("[ ]", styles['CellBold'])
        desc = Paragraph(esc(test_desc), styles['CellNormal'])

        row = [checkbox, desc]
        if note:
            row.append(Paragraph(esc(note), ParagraphStyle(
                'NoteCell', parent=styles['CellNormal'],
                fontSize=8, textColor=SLATE_MED, fontName='Helvetica-Oblique'
            )))
        else:
            row.append(Paragraph("", styles['CellNormal']))
        data.append(row)

    if not data:
        return Spacer(1, 0)

    col_widths = [8*mm, 110*mm, 52*mm]
    t = Table(data, colWidths=col_widths, repeatRows=0)

    # Priority colors
    if priority == "CRITICAL":
        row_bg = RED_BG
    elif priority == "HIGH":
        row_bg = AMBER_LIGHT
    elif priority == "LOW":
        row_bg = BLUE_BG
    else:
        row_bg = GRAY_BG

    style_cmds = [
        ('VALIGN', (0, 0), (-1, -1), 'TOP'),
        ('TOPPADDING', (0, 0), (-1, -1), 3),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 3),
        ('LEFTPADDING', (0, 0), (0, -1), 2),
        ('RIGHTPADDING', (-1, 0), (-1, -1), 2),
        ('LINEBELOW', (0, 0), (-1, -2), 0.3, BORDER_GRAY),
        ('LINEBELOW', (0, -1), (-1, -1), 0.5, BORDER_GRAY),
    ]

    for i in range(len(data)):
        if i % 2 == 0:
            style_cmds.append(('BACKGROUND', (0, i), (-1, i), row_bg))

    t.setStyle(TableStyle(style_cmds))
    return t

def priority_badge(text, styles, color):
    """Create a colored priority label."""
    return Paragraph(
        f'<font color="{color.hexval()}">[{text}]</font>',
        styles['Priority']
    )

def section_divider():
    return HRFlowable(width="100%", thickness=1, color=AMBER_PRIMARY, spaceAfter=8, spaceBefore=4)

def thin_divider():
    return HRFlowable(width="100%", thickness=0.5, color=BORDER_GRAY, spaceAfter=6, spaceBefore=6)

# ── Main Document Builder ──────────────────────────────────────────────
def build_test_plan():
    output_path = os.path.join(os.path.dirname(__file__), "..", "meepleai-manual-test-plan.pdf")
    output_path = os.path.normpath(output_path)

    doc = SimpleDocTemplate(
        output_path,
        pagesize=A4,
        topMargin=22*mm,
        bottomMargin=20*mm,
        leftMargin=20*mm,
        rightMargin=20*mm,
        title="MeepleAI - Pre-Launch Manual Test Plan",
        author="MeepleAI Team",
        subject="Comprehensive manual test checklist for pre-launch verification"
    )

    styles = get_styles()
    story = []

    # ════════════════════════════════════════════════════════════════════
    # COVER PAGE
    # ════════════════════════════════════════════════════════════════════
    story.append(Spacer(1, 40*mm))
    story.append(Paragraph("MeepleAI", styles['DocTitle']))
    story.append(Paragraph("Pre-Launch Manual Test Plan", ParagraphStyle(
        'CoverSub', parent=styles['DocSubtitle'],
        fontSize=20, leading=26, textColor=AMBER_DARK
    )))
    story.append(Spacer(1, 10*mm))
    story.append(HRFlowable(width="60%", thickness=2, color=AMBER_PRIMARY, spaceAfter=10))
    story.append(Spacer(1, 8*mm))

    cover_info = [
        f"Data: {datetime.now().strftime('%d %B %Y')}",
        "Versione: 1.0",
        "Stato: Pre-acquisto dominio e hosting",
        "",
        "Stack: .NET 9 + Next.js 16 + PostgreSQL + Qdrant + Redis",
        "Bounded Contexts: 15 | Endpoints: 803 | Routes Frontend: 180+",
        "Issues aperte: 80 (nessuna bloccante per launch)",
        "",
        "Priorita test:",
        "  CRITICO = bloccante per il go-live",
        "  ALTO = importante, degrado funzionale se fallisce",
        "  MEDIO = funzionalita secondarie",
        "  BASSO = nice-to-have, cosmetico"
    ]
    for line in cover_info:
        story.append(Paragraph(line, styles['TestItem']))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # TABLE OF CONTENTS
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("Indice", styles['SectionTitle']))
    story.append(section_divider())

    toc_items = [
        ("1", "Infrastruttura e Ambiente", "CRITICO"),
        ("2", "Autenticazione e Sicurezza", "CRITICO"),
        ("3", "Pagine Pubbliche (senza auth)", "ALTO"),
        ("4", "Registrazione e Onboarding", "CRITICO"),
        ("5", "Dashboard e Navigazione", "ALTO"),
        ("6", "Libreria e Collezioni", "ALTO"),
        ("7", "Catalogo Giochi e Discovery", "ALTO"),
        ("8", "Sessioni di Gioco", "MEDIO"),
        ("9", "Chat AI e RAG", "CRITICO"),
        ("10", "Knowledge Base e Documenti", "MEDIO"),
        ("11", "Profilo Utente e Impostazioni", "MEDIO"),
        ("12", "Gamification (Badge e Achievement)", "BASSO"),
        ("13", "Admin Panel - Overview e Sistema", "CRITICO"),
        ("14", "Admin Panel - Gestione Contenuti", "ALTO"),
        ("15", "Admin Panel - AI e Agenti", "ALTO"),
        ("16", "Admin Panel - Utenti e Analytics", "MEDIO"),
        ("17", "Admin Panel - Monitoring e Config", "MEDIO"),
        ("18", "API Health e Endpoints", "CRITICO"),
        ("19", "Performance e Carico", "ALTO"),
        ("20", "Responsive e Cross-Browser", "ALTO"),
        ("21", "Sicurezza Applicativa (OWASP)", "CRITICO"),
        ("22", "Deployment Checklist", "CRITICO"),
        ("23", "Issues Aperte - Valutazione Rischio", "INFO"),
    ]

    toc_data = []
    for num, title, prio in toc_items:
        prio_colors = {
            "CRITICO": RED, "ALTO": AMBER_DARK, "MEDIO": BLUE,
            "BASSO": SLATE_LIGHT, "INFO": PURPLE
        }
        color = prio_colors.get(prio, SLATE_MED)
        toc_data.append([
            Paragraph(f"<b>{num}.</b>", styles['CellBold']),
            Paragraph(title, styles['CellNormal']),
            Paragraph(f'<font color="{color.hexval()}">{prio}</font>', styles['CellBold']),
        ])

    toc_table = Table(toc_data, colWidths=[12*mm, 120*mm, 30*mm])
    toc_table.setStyle(TableStyle([
        ('VALIGN', (0, 0), (-1, -1), 'TOP'),
        ('TOPPADDING', (0, 0), (-1, -1), 3),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 3),
        ('LINEBELOW', (0, 0), (-1, -2), 0.3, BORDER_GRAY),
    ]))
    story.append(toc_table)
    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 1: INFRASTRUTTURA E AMBIENTE
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("1. Infrastruttura e Ambiente", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("1.1 Docker e Servizi Core", styles['SubSection']))
    story.append(make_checkbox_table([
        ("docker compose --profile minimal up -d avvia senza errori", "postgres, qdrant, redis"),
        ("docker compose --profile dev up -d avvia tutti i servizi dev", "inclusi grafana, mailpit, minio"),
        ("docker compose --profile full up -d avvia lo stack completo", "tutti i 15+ container"),
        ("docker compose ps mostra tutti i container 'healthy'", "nessun container in restart loop"),
        ("PostgreSQL accetta connessioni sulla porta 5432", "psql o pg_isready"),
        ("Redis accetta connessioni sulla porta 6379", "redis-cli ping = PONG"),
        ("Qdrant API risponde su porta 6333", "curl http://localhost:6333"),
        ("pgvector extension e' installata in PostgreSQL", "SELECT * FROM pg_extension"),
    ], styles, "CRITICAL"))

    story.append(Paragraph("1.2 Secrets e Configurazione", styles['SubSection']))
    story.append(make_checkbox_table([
        ("pwsh setup-secrets.ps1 -SaveGenerated genera tutti i 18 file secret", "cd infra/secrets/"),
        ("database.secret contiene credenziali valide", "CRITICO - blocca startup"),
        ("redis.secret contiene password valida", "CRITICO - blocca startup"),
        ("jwt.secret contiene chiave JWT (min 256 bit)", "CRITICO - blocca startup"),
        ("admin.secret contiene credenziali admin iniziale", "CRITICO - blocca startup"),
        ("embedding-service.secret configurato", "CRITICO - blocca startup"),
        ("openrouter.secret contiene API key valida", "IMPORTANTE - warn se mancante"),
        (".secret files sono nel .gitignore", "git status non li mostra"),
        ("Nessun secret committato nel repository", "grep -r 'password' --include='*.json'"),
    ], styles, "CRITICAL"))

    story.append(Paragraph("1.3 Database e Migrazioni", styles['SubSection']))
    story.append(make_checkbox_table([
        ("dotnet ef database update applica tutte le migrazioni", "cd apps/api/src/Api"),
        ("Schema DB creato correttamente (15+ bounded context)", "tabelle per ogni context"),
        ("pgvector indici creati per embedding", "vector search funzionante"),
        ("Soft-delete query filters attivi (IsDeleted)", "HasQueryFilter verificato"),
        ("Seed data admin utente creato", "login admin funzionante"),
    ], styles, "CRITICAL"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 2: AUTENTICAZIONE E SICUREZZA
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("2. Autenticazione e Sicurezza", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("2.1 Registrazione e Login", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Registrazione con email/password crea utente", "POST /api/v1/auth/register"),
        ("Validazione email formato corretto", "email invalida = errore 400"),
        ("Validazione password (min length, complessita)", "password debole rifiutata"),
        ("Login con credenziali valide ritorna session cookie", "POST /api/v1/auth/login"),
        ("Login con credenziali errate ritorna 401", "messaggio generico, no info leak"),
        ("Logout invalida la sessione", "POST /api/v1/auth/logout"),
        ("Session cookie ha flag HttpOnly, Secure, SameSite", "ispeziona Set-Cookie header"),
        ("Rate limiting su login (max tentativi)", "brute force protetto"),
    ], styles, "CRITICAL"))

    story.append(Paragraph("2.2 Email Verification", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Dopo registrazione, email di verifica inviata", "controlla mailpit o SMTP"),
        ("Link di verifica nell'email funziona", "GET /verify-email?token=xxx"),
        ("Token di verifica scade dopo tempo configurato", "token expired = errore"),
        ("Pagina /verification-pending mostra stato corretto", "con pulsante re-invio"),
        ("Re-invio email verifica funziona", "rate limited"),
        ("Pagina /verification-success dopo verifica OK", "redirect a login/dashboard"),
    ], styles, "HIGH"))

    story.append(Paragraph("2.3 Password Reset", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Richiesta reset password invia email", "POST /api/v1/auth/reset-password"),
        ("Link reset nell'email funziona", "pagina /reset-password con token"),
        ("Nuova password accettata e funzionante", "login con nuova password OK"),
        ("Token reset single-use (non riutilizzabile)", "secondo uso = errore"),
        ("Email non esistente non rivela se utente esiste", "risposta generica 200"),
    ], styles, "HIGH"))

    story.append(Paragraph("2.4 OAuth (Google, GitHub, Discord, Microsoft)", styles['SubSection']))
    story.append(make_checkbox_table([
        ("OAuth Google: redirect e callback funzionanti", "se oauth.secret configurato"),
        ("OAuth GitHub: redirect e callback funzionanti", "se oauth.secret configurato"),
        ("OAuth Discord: redirect e callback funzionanti", "se oauth.secret configurato"),
        ("OAuth Microsoft: redirect e callback funzionanti", "se oauth.secret configurato"),
        ("Account OAuth collegato a utente esistente", "stesso email = merge"),
        ("Nuovo utente OAuth creato se email non esiste", "registrazione automatica"),
    ], styles, "MEDIUM"))

    story.append(Paragraph("2.5 Two-Factor Authentication (2FA)", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Attivazione 2FA genera QR code TOTP", "scansione con app authenticator"),
        ("Codice TOTP valido completa login", "6 cifre, 30s window"),
        ("Codice TOTP invalido rifiutato", "errore specifico"),
        ("Codice TOTP gia usato rifiutato (replay attack)", "UsedTotpCodeEntity"),
        ("Disattivazione 2FA richiede codice corrente", "conferma sicura"),
    ], styles, "MEDIUM"))

    story.append(Paragraph("2.6 API Key Authentication", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Creazione API key ritorna chiave (mostrata una volta)", "POST /api/v1/auth/api-key"),
        ("Login con API key funziona (Bearer token)", "POST /api/v1/auth/api-key/login"),
        ("API key invalida ritorna 401", "chiave errata"),
        ("Revoca API key impedisce accesso futuro", "DELETE /api/v1/auth/api-key/{id}"),
    ], styles, "MEDIUM"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 3: PAGINE PUBBLICHE
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("3. Pagine Pubbliche (senza auth)", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("3.1 Landing e Marketing", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Homepage / carica correttamente", "hero, features, CTA visibili"),
        ("/how-it-works mostra spiegazione piattaforma", "contenuto completo"),
        ("/about mostra informazioni", "testo e immagini"),
        ("/contact mostra form di contatto", "form funzionante"),
        ("/faq mostra domande frequenti", "accordion o lista"),
        ("/blog mostra articoli", "lista post"),
        ("/gallery mostra showcase componenti", "immagini/componenti"),
        ("/contributions mostra contributi community", "lista contributi"),
    ], styles, "HIGH"))

    story.append(Paragraph("3.2 Pagine Legali", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/terms mostra termini di servizio", "testo completo e aggiornato"),
        ("/privacy mostra privacy policy", "GDPR compliant"),
        ("/cookies mostra cookie policy", "banner cookie presente"),
    ], styles, "HIGH"))

    story.append(Paragraph("3.3 Catalogo Pubblico Giochi", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/games mostra lista giochi pubblica", "ricerca e filtri funzionanti"),
        ("/games/[id] mostra dettaglio gioco", "info complete, regole, FAQ"),
        ("/games/catalog catalogo navigabile", "paginazione funzionante"),
        ("/games/add form proposta gioco accessibile", "form validazione"),
        ("/shared-games/[id] scheda gioco community", "dati corretti"),
        ("/board-game-ai landing AI section", "contenuto visibile"),
    ], styles, "HIGH"))

    story.append(Paragraph("3.4 SEO e Meta Tags", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Ogni pagina pubblica ha <title> corretto", "tab browser mostra titolo"),
        ("Meta description presente su ogni pagina", "per Google snippet"),
        ("Open Graph tags per condivisione social", "og:title, og:image, og:description"),
        ("robots.txt accessibile e corretto", "/robots.txt"),
        ("sitemap.xml generata e corretta", "/sitemap.xml"),
    ], styles, "MEDIUM"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 4: REGISTRAZIONE E ONBOARDING
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("4. Registrazione e Onboarding", styles['SectionTitle']))
    story.append(section_divider())

    story.append(make_checkbox_table([
        ("/register mostra form registrazione", "email, password, nome"),
        ("Registrazione con dati validi crea account", "redirect a verifica email"),
        ("Campi obbligatori evidenziati se mancanti", "validazione client-side"),
        ("Password strength indicator funzionante", "feedback visivo"),
        ("/welcome mostra wizard onboarding post-auth", "setup profilo"),
        ("Wizard onboarding completabile fino alla fine", "tutti gli step"),
        ("Skip onboarding possibile", "accesso diretto a dashboard"),
        ("Primo login dopo verifica email funziona", "redirect a dashboard"),
        ("Utente appena registrato ha ruolo 'User'", "non admin/editor"),
    ], styles, "CRITICAL"))

    # ════════════════════════════════════════════════════════════════════
    # SECTION 5: DASHBOARD E NAVIGAZIONE
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("5. Dashboard e Navigazione", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("5.1 Navigazione Globale", styles['SubSection']))
    story.append(make_checkbox_table([
        ("TopNav visibile su tutte le pagine autenticate", "logo, menu, user avatar"),
        ("MiniNav mostra tab contestuali per sezione", "tabs cambiano per sezione"),
        ("NavActionBar mostra azioni contestuali", "bottoni azione"),
        ("Mobile: hamburger menu funzionante", "Sheet drawer su mobile"),
        ("Mobile: NavActionBar fixed bottom funzionante", "azioni accessibili"),
        ("Navigazione tra sezioni fluida (no flash)", "client-side navigation"),
        ("Breadcrumb o back navigation presente", "orientamento utente"),
    ], styles, "HIGH"))

    story.append(Paragraph("5.2 Dashboard Principale", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/dashboard carica e mostra dati utente", "statistiche, sessioni recenti"),
        ("Quick actions funzionanti (nuova sessione, cerca gioco)", "bottoni cliccabili"),
        ("Statistiche aggiornate (giochi, sessioni, ecc)", "dati corretti"),
        ("/dashboard/budget mostra tier e utilizzo", "meter usage, upgrade"),
    ], styles, "HIGH"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 6: LIBRERIA E COLLEZIONI
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("6. Libreria e Collezioni", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("6.1 Collezione Personale", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/library mostra collezione utente con tabs", "Collection, Games, Wishlist, Proposals"),
        ("Aggiungere gioco alla collezione funziona", "bottone Add + conferma"),
        ("Rimuovere gioco dalla collezione funziona", "soft delete"),
        ("Ricerca nella collezione funzionante", "filtro per nome"),
        ("MeepleCard mostra info corrette per ogni gioco", "titolo, immagine, rating"),
        ("Ordinamento giochi (nome, rating, data aggiunta)", "sort funzionante"),
    ], styles, "HIGH"))

    story.append(Paragraph("6.2 Giochi Privati", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/library/private mostra giochi privati utente", "lista giochi custom"),
        ("/library/private/add permette creare gioco custom", "form completo"),
        ("Gioco privato creato appare nella lista", "aggiornamento immediato"),
        ("Toolkit configurabile per gioco privato", "/library/private/[id]/toolkit/configure"),
    ], styles, "MEDIUM"))

    story.append(Paragraph("6.3 Wishlist", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Aggiungere gioco alla wishlist funziona", "cuore/stella toggle"),
        ("Rimuovere dalla wishlist funziona", "toggle off"),
        ("/library?tab=wishlist mostra lista corretta", "giochi salvati"),
    ], styles, "MEDIUM"))

    story.append(Paragraph("6.4 Proposte Giochi", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/library/propose mostra form proposta", "integrazione BGG"),
        ("Proposta inviata correttamente", "stato 'pending'"),
        ("/library/proposals mostra stato proposte utente", "tracking"),
        ("Condivisione link libreria (/library/shared/[token])", "link pubblico funzionante"),
    ], styles, "MEDIUM"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 7: CATALOGO GIOCHI E DISCOVERY
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("7. Catalogo Giochi e Discovery", styles['SectionTitle']))
    story.append(section_divider())

    story.append(make_checkbox_table([
        ("/discover mostra giochi con ricerca e filtri", "meccaniche, temi, trending"),
        ("Ricerca per nome funzionante", "risultati pertinenti"),
        ("Filtro per categoria/meccanica funzionante", "combo filtri"),
        ("Paginazione risultati funzionante", "load more o pagine"),
        ("/discover/[id] mostra dettaglio gioco completo", "info, regole, FAQ, sessioni"),
        ("/games/[id]/faqs mostra FAQ (community + AI)", "domande e risposte"),
        ("/games/[id]/rules mostra versioni regole", "espandibili"),
        ("/games/[id]/reviews mostra recensioni", "user reviews"),
        ("/games/[id]/sessions mostra sessioni per gioco", "statistiche"),
        ("/games/[id]/strategies mostra strategie", "tips avanzati"),
        ("Entity links tra giochi visibili (espansioni, sequel)", "badge e link"),
    ], styles, "HIGH"))

    # ════════════════════════════════════════════════════════════════════
    # SECTION 8: SESSIONI DI GIOCO
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("8. Sessioni di Gioco", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("8.1 Gestione Sessioni", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/sessions mostra sessioni attive e cronologia", "tabs Active/History"),
        ("/sessions/new permette creare nuova sessione", "selezione gioco + giocatori"),
        ("Creazione sessione con gioco e giocatori funziona", "redirect a sessione"),
        ("/sessions/join permette unirsi tramite codice", "codice invito"),
        ("/sessions/[id] mostra sessione attiva", "scoreboard, note, aggiornamenti"),
    ], styles, "MEDIUM"))

    story.append(Paragraph("8.2 Funzionalita In-Session", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Aggiornamento punteggio in tempo reale", "SSE /stream"),
        ("Note sessione salvate correttamente", "/sessions/[id]/notes"),
        ("Gestione giocatori nella sessione", "/sessions/[id]/players"),
        ("SignalR hub connesso per aggiornamenti live", "/hubs/gamestate"),
        ("Snapshot sessione funzionante", "salvataggio stato"),
    ], styles, "MEDIUM"))

    story.append(Paragraph("8.3 Play Records e Statistiche", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/play-records mostra storico partite", "filtro per gioco/data"),
        ("/play-records/new permette registrare partita", "form completo"),
        ("/play-records/stats mostra statistiche", "win rate, frequenza, ranking"),
        ("Modifica play record esistente funziona", "/play-records/[id]/edit"),
    ], styles, "MEDIUM"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 9: CHAT AI E RAG
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("9. Chat AI e RAG", styles['SectionTitle']))
    story.append(section_divider())
    story.append(Paragraph(
        "Funzionalita core della piattaforma. Richiede OpenRouter API key o Ollama locale.",
        styles['TestNote']
    ))

    story.append(Paragraph("9.1 Chat Thread", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/chat mostra lista sessioni chat", "raggruppate per agente"),
        ("/chat/new permette creare nuovo thread", "selezione agente/gioco"),
        ("Creazione thread con agente specifico funziona", "redirect a chat"),
        ("/chat/[threadId] mostra conversazione", "messaggi precedenti"),
        ("Invio messaggio e risposta AI funzionante", "SSE streaming"),
        ("Streaming risposta AI visibile in tempo reale", "testo appare progressivamente"),
        ("Risposta AI contiene citazioni regolamento (se disponibili)", "fonti RAG"),
        ("Export chat funzionante (PDF, JSON, CSV)", "download file"),
        ("Condivisione link chat funzionante", "link pubblico"),
        ("Tier usage banner mostra limite residuo", "chat limits"),
    ], styles, "CRITICAL"))

    story.append(Paragraph("9.2 RAG Pipeline", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Domanda su gioco con KB restituisce risposta contestuale", "citazioni da regolamento"),
        ("Risposta AI pertinente e accurata", "non hallucination"),
        ("Fallback a risposta generica se KB vuota", "graceful degradation"),
        ("Confidence score nella risposta (se visibile)", "badge confidence"),
        ("Hybrid search (vector + keyword) funzionante", "risultati misti"),
    ], styles, "CRITICAL"))

    story.append(Paragraph("9.3 Ask (Domanda Diretta)", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/ask mostra interfaccia domanda diretta", "selezione gioco, input"),
        ("Domanda inviata e risposta ricevuta", "risposta AI"),
        ("Selezione gioco cambia contesto della risposta", "KB specifica"),
    ], styles, "HIGH"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 10: KNOWLEDGE BASE E DOCUMENTI
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("10. Knowledge Base e Documenti", styles['SectionTitle']))
    story.append(section_divider())

    story.append(make_checkbox_table([
        ("/knowledge-base mostra overview KB", "ricerca documenti"),
        ("/knowledge-base/[id] mostra dettaglio documento", "contenuto, riferimenti"),
        ("Upload PDF funzionante (admin)", "file fino a 500MB"),
        ("Processing PDF (estrazione testo, chunking)", "pipeline completa"),
        ("Documenti processati appaiono in KB", "searchable"),
        ("Reindex documento funzionante", "ri-elaborazione"),
        ("Delete documento funzionante", "soft delete"),
        ("Vector search su contenuto documenti", "risultati pertinenti"),
    ], styles, "MEDIUM"))

    # ════════════════════════════════════════════════════════════════════
    # SECTION 11: PROFILO E IMPOSTAZIONI
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("11. Profilo Utente e Impostazioni", styles['SectionTitle']))
    story.append(section_divider())

    story.append(make_checkbox_table([
        ("/profile mostra profilo utente", "nome, email, avatar"),
        ("Modifica profilo salva correttamente", "update display name"),
        ("/profile/achievements mostra badge guadagnati", "progresso"),
        ("/settings/notifications mostra preferenze notifiche", "toggle email/push"),
        ("Salvataggio preferenze notifiche funziona", "persistenza"),
        ("/settings/security mostra opzioni sicurezza", "password, 2FA, sessioni, API keys"),
        ("Cambio password funzionante", "vecchia + nuova password"),
        ("/notifications mostra centro notifiche", "lista, mark as read"),
        ("/badges mostra tutti i badge disponibili", "criteri e progresso"),
    ], styles, "MEDIUM"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 12: GAMIFICATION
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("12. Gamification (Badge e Achievement)", styles['SectionTitle']))
    story.append(section_divider())

    story.append(make_checkbox_table([
        ("Sistema achievement tracka azioni utente", "contatori corretti"),
        ("Badge assegnato quando criterio raggiunto", "notifica"),
        ("Badge display nel profilo funzionante", "griglia badge"),
        ("Leaderboard /players mostra classifica", "ordinamento corretto"),
        ("/players/[id] mostra profilo pubblico giocatore", "stats, giochi, sessioni"),
    ], styles, "LOW"))

    # ════════════════════════════════════════════════════════════════════
    # SECTION 13: ADMIN - OVERVIEW E SISTEMA
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("13. Admin Panel - Overview e Sistema", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("13.1 Accesso Admin", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Login admin con credenziali da admin.secret", "accesso dashboard"),
        ("Utente non-admin riceve 403 su /admin/*", "protezione ruolo"),
        ("Redirect a login se sessione scaduta", "session expiry"),
    ], styles, "CRITICAL"))

    story.append(Paragraph("13.2 Dashboard Overview", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/admin/overview mostra statistiche piattaforma", "KPI, grafici"),
        ("/admin/overview/activity mostra activity feed", "azioni recenti"),
        ("/admin/overview/system mostra health sistema", "DB, servizi, queue"),
        ("TopNav admin con 6 sezioni navigabili", "Overview, Content, AI, Users, System, Analytics"),
        ("Sidebar contestuale mostra sottomenu", "per sezione corrente"),
    ], styles, "CRITICAL"))

    story.append(Paragraph("13.3 System Health", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Health check mostra stato tutti i servizi", "verde/giallo/rosso"),
        ("PostgreSQL status corretto", "connessioni, query time"),
        ("Redis status corretto", "hit rate, memoria"),
        ("Qdrant status corretto", "collections, vectors"),
        ("Embedding service status corretto", "disponibilita"),
        ("OpenRouter status (se configurato)", "disponibilita, rate limits"),
    ], styles, "CRITICAL"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 14: ADMIN - GESTIONE CONTENUTI
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("14. Admin Panel - Gestione Contenuti", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("14.1 Shared Games", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/admin/shared-games/all mostra tutti i giochi", "ricerca, filtri, paginazione"),
        ("/admin/shared-games/new permette creare gioco", "form completo + BGG"),
        ("/admin/shared-games/[id] mostra dettaglio editabile", "modifica metadata"),
        ("Approvazione/rifiuto proposte giochi funzionante", "workflow completo"),
        ("Import bulk da BGG funzionante", "/admin/shared-games/import"),
        ("Gestione categorie funzionante", "/admin/shared-games/categories"),
    ], styles, "HIGH"))

    story.append(Paragraph("14.2 Knowledge Base Admin", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/admin/knowledge-base mostra overview KB", "stats, queue, recenti"),
        ("/admin/knowledge-base/documents mostra libreria PDF", "lista, search, filter"),
        ("Upload PDF associato a SharedGame funziona", "500MB max, progress bar"),
        ("/admin/knowledge-base/vectors mostra collezioni Qdrant", "stats vettori"),
        ("/admin/knowledge-base/queue mostra coda processing", "pending, failed, retry"),
        ("Reindex documento singolo funziona", "ri-elaborazione"),
        ("Delete documento e vettori funziona", "cleanup completo"),
        ("/admin/knowledge-base/embedding mostra stato servizio", "modello, metriche"),
    ], styles, "HIGH"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 15: ADMIN - AI E AGENTI
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("15. Admin Panel - AI e Agenti", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("15.1 Gestione Agenti", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/admin/agents mostra lista agenti", "attivi, conteggio, modello"),
        ("/admin/agents/definitions mostra definizioni agenti", "prompt, parametri"),
        ("Creazione nuovo agente funzionante", "/admin/agents/definitions/create"),
        ("Modifica agente esistente funzionante", "edit prompt, modello"),
        ("/admin/agents/builder visual builder funzionante", "drag-drop config"),
        ("/admin/agents/models mostra config modelli", "modelli + prompt"),
        ("AgentBuilderModal con KB card checklist", "selezione card"),
    ], styles, "HIGH"))

    story.append(Paragraph("15.2 Debug e Monitoring AI", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/admin/agents/pipeline mostra pipeline RAG explorer", "6-node flow diagram"),
        ("/admin/agents/debug console debug RAG funzionante", "test query, view chain"),
        ("/admin/agents/strategy mostra tier-strategy matrix", "routing config"),
        ("/admin/agents/usage mostra costi e utilizzo LLM", "KPI cards, grafici"),
        ("/admin/agents/chat-history mostra storico chat utenti", "ricerca"),
        ("/admin/agents/chat-limits mostra limiti chat per tier", "configurabile"),
    ], styles, "HIGH"))

    # ════════════════════════════════════════════════════════════════════
    # SECTION 16: ADMIN - UTENTI E ANALYTICS
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("16. Admin Panel - Utenti e Analytics", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("16.1 Gestione Utenti", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/admin/users mostra lista utenti", "tabella, ricerca, filtri ruolo/stato"),
        ("Sospensione utente funzionante", "suspend/unsuspend"),
        ("Cambio ruolo utente funzionante", "User/Editor/Admin"),
        ("Impersonificazione utente funzionante (SuperAdmin)", "login as user"),
        ("/admin/users/roles mostra gestione ruoli", "permessi"),
        ("/admin/users/activity mostra log attivita", "audit trail"),
    ], styles, "MEDIUM"))

    story.append(Paragraph("16.2 Analytics", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/admin/analytics?tab=overview mostra KPI piattaforma", "metriche chiave"),
        ("/admin/analytics?tab=ai-usage mostra utilizzo AI", "token, costi, per tier"),
        ("/admin/analytics?tab=audit mostra audit log completo", "tutte le azioni"),
        ("/admin/analytics?tab=reports genera report", "download"),
        ("/admin/analytics?tab=api-keys gestione API key utenti", "revoca, tracking"),
    ], styles, "MEDIUM"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 17: ADMIN - MONITORING E CONFIG
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("17. Admin Panel - Monitoring e Config", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("17.1 Monitoring", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/admin/monitor?tab=alerts mostra alert attivi", "gestione alert"),
        ("/admin/monitor?tab=cache mostra stato cache Redis", "hit rate, memoria, clear"),
        ("/admin/monitor?tab=infra mostra infrastruttura", "servizi, uptime"),
        ("/admin/monitor?tab=command centro comandi", "esecuzione comandi admin"),
        ("/admin/monitor?tab=email mostra log email", "invii, errori, SMTP config"),
        ("/admin/monitor?tab=export esportazione bulk dati", "games, users, sessions"),
    ], styles, "MEDIUM"))

    story.append(Paragraph("17.2 Configurazione", styles['SubSection']))
    story.append(make_checkbox_table([
        ("/admin/config?tab=general impostazioni generali", "branding, features"),
        ("/admin/config?tab=limits limiti risorse", "file size, request limits"),
        ("/admin/config?tab=flags feature flags", "toggle on/off"),
        ("/admin/config?tab=rate-limits rate limiting", "per user/endpoint"),
        ("Salvataggio configurazione persistente", "DB, non file"),
    ], styles, "MEDIUM"))

    # ════════════════════════════════════════════════════════════════════
    # SECTION 18: API HEALTH E ENDPOINTS
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("18. API Health e Endpoints", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("18.1 Health Check Endpoints", styles['SubSection']))
    story.append(make_checkbox_table([
        ("GET /health ritorna 200 con status complessivo", "Healthy/Degraded/Unhealthy"),
        ("GET /health/ready ritorna 200 (readiness probe)", "DB + cache + vector"),
        ("GET /health/live ritorna 200 (liveness probe)", "processo attivo"),
        ("GET /health/config ritorna validazione config", "configurazione OK"),
        ("Health check distingue servizi critici vs non-critici", "degraded != unhealthy"),
    ], styles, "CRITICAL"))

    story.append(Paragraph("18.2 API Documentation", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Scalar API docs accessibili su /scalar/v1", "http://localhost:8080/scalar/v1"),
        ("Tutti gli endpoint documentati con schema", "request/response schema"),
        ("Try-it-out funzionante per endpoint pubblici", "test diretto"),
        ("Autenticazione funzionante nei docs per endpoint protetti", "Bearer token"),
    ], styles, "HIGH"))

    story.append(Paragraph("18.3 SSE e Real-Time", styles['SubSection']))
    story.append(make_checkbox_table([
        ("SSE game session stream funzionante", "/game-sessions/{id}/stream"),
        ("SSE chat streaming funzionante", "risposta AI in streaming"),
        ("SSE queue progress funzionante (admin)", "/admin/queue/{jobId}/stream"),
        ("SignalR WebSocket hub connesso", "/hubs/gamestate"),
        ("Reconnection automatica dopo disconnessione", "resilienza"),
    ], styles, "HIGH"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 19: PERFORMANCE
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("19. Performance e Carico", styles['SectionTitle']))
    story.append(section_divider())

    story.append(make_checkbox_table([
        ("Homepage carica in < 3 secondi (First Contentful Paint)", "Lighthouse o DevTools"),
        ("Dashboard carica in < 2 secondi dopo login", "API response time"),
        ("Ricerca giochi risponde in < 1 secondo", "con 100+ giochi"),
        ("Chat AI primo token in < 3 secondi", "SSE streaming"),
        ("Upload PDF progress bar funzionante", "XHR con tracking"),
        ("Lista giochi con 50+ elementi non rallenta", "virtual scrolling o pagination"),
        ("Admin dashboard carica in < 3 secondi", "tutte le sezioni"),
        ("API P95 latency < 1 secondo per endpoint comuni", "escluso LLM"),
        ("Nessun memory leak su navigazione prolungata", "DevTools Memory"),
        ("Bundle size ragionevole (< 500KB gzipped initial)", "next build analysis"),
    ], styles, "HIGH"))

    # ════════════════════════════════════════════════════════════════════
    # SECTION 20: RESPONSIVE E CROSS-BROWSER
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("20. Responsive e Cross-Browser", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("20.1 Responsive Design", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Mobile (375px): layout non rotto, contenuto leggibile", "iPhone SE/13"),
        ("Tablet (768px): layout adattato, sidebar collassata", "iPad"),
        ("Desktop (1280px): layout completo, sidebar visibile", "Laptop"),
        ("Large (1920px+): contenuto non troppo stretched", "Monitor"),
        ("Mobile: navigazione hamburger funzionante", "Sheet drawer"),
        ("Mobile: form compilabili senza zoom", "input accessibili"),
        ("Mobile: tabelle scrollabili orizzontalmente", "non overflow"),
        ("Mobile: modale e dialog usabili", "non coprono tutto"),
    ], styles, "HIGH"))

    story.append(Paragraph("20.2 Cross-Browser", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Chrome (ultima versione): tutto funzionante", "browser principale"),
        ("Firefox (ultima versione): tutto funzionante", "secondo browser"),
        ("Safari (ultima versione): tutto funzionante", "se possibile, utenti Mac/iOS"),
        ("Edge (ultima versione): tutto funzionante", "Chromium-based"),
        ("iOS Safari: navigazione e form funzionanti", "mobile Apple"),
        ("Android Chrome: navigazione e form funzionanti", "mobile Android"),
    ], styles, "HIGH"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 21: SICUREZZA APPLICATIVA (OWASP)
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("21. Sicurezza Applicativa (OWASP Top 10)", styles['SectionTitle']))
    story.append(section_divider())

    story.append(make_checkbox_table([
        ("A01 - Broken Access Control: endpoint admin protetti da ruolo", "403 per non-admin"),
        ("A01 - IDOR: utente non puo accedere dati altri utenti", "test con ID diversi"),
        ("A02 - Cryptographic Failures: password hashate (bcrypt/argon2)", "no plaintext in DB"),
        ("A02 - JWT firmato correttamente e non espone dati sensibili", "decode e verifica"),
        ("A03 - Injection: input sanitizzato (no SQL injection)", "test parametri speciali"),
        ("A03 - XSS: output encodato, no script injection", "test <script> in input"),
        ("A04 - Insecure Design: rate limiting su tutti gli endpoint sensibili", "login, register, API"),
        ("A05 - Security Misconfiguration: CORS configurato correttamente", "no wildcard in prod"),
        ("A05 - Header sicurezza presenti", "X-Frame-Options, CSP, HSTS"),
        ("A06 - Vulnerable Components: dipendenze aggiornate", "npm audit, dotnet audit"),
        ("A07 - Authentication Failures: session timeout funzionante", "scadenza sessione"),
        ("A08 - Data Integrity: CSRF protection attiva", "token CSRF se cookie auth"),
        ("A09 - Logging: eventi sicurezza loggati", "login falliti, accessi admin"),
        ("A10 - SSRF: upload URL non permette localhost/interni", "validazione URL"),
    ], styles, "CRITICAL"))

    # ════════════════════════════════════════════════════════════════════
    # SECTION 22: DEPLOYMENT CHECKLIST
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("22. Deployment Checklist", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph("22.1 Pre-Deployment", styles['SubSection']))
    story.append(make_checkbox_table([
        ("dotnet build compila senza errori", "cd apps/api/src/Api"),
        ("dotnet test passa tutti i test (14,700+)", "nessun test fallito"),
        ("pnpm build compila frontend senza errori", "cd apps/web"),
        ("pnpm test passa tutti i test (1,100+)", "vitest"),
        ("pnpm typecheck senza errori TypeScript", "tsc --noEmit"),
        ("pnpm lint senza errori ESLint", "linting"),
        ("Nessun TODO critico nel codice", "grep TODO --include='*.cs'"),
        ("git status pulito (no file non committati)", "working tree clean"),
    ], styles, "CRITICAL"))

    story.append(Paragraph("22.2 Domain e DNS", styles['SubSection']))
    story.append(make_checkbox_table([
        ("Dominio .com acquistato (Cloudflare Registrar)", "es. meepleai.com"),
        ("Record A per @ (root) punta a VPS IP", "TTL 300"),
        ("Record A per www punta a VPS IP", "TTL 300"),
        ("Record A per api punta a VPS IP", "TTL 300"),
        ("Record A per grafana punta a VPS IP", "se monitoring"),
        ("DNS propagato (dig o nslookup)", "verifica 1-2 min"),
        ("Social handles riservati (@meepleai)", "Twitter, Discord"),
    ], styles, "CRITICAL"))

    story.append(Paragraph("22.3 Server e SSL", styles['SubSection']))
    story.append(make_checkbox_table([
        ("VPS provisionato (Hetzner CPX31 consigliato)", "4vCPU, 16GB RAM"),
        ("Docker e Docker Compose installati", "versioni recenti"),
        ("Firewall configurato (UFW: 22, 80, 443)", "porte aperte minime"),
        ("SSH key-only authentication", "password login disabilitato"),
        ("Fail2Ban installato e configurato", "protezione brute-force"),
        ("Traefik configurato per SSL (Let's Encrypt)", "ACME auto-renewal"),
        ("HTTPS funzionante su tutti i sottodomini", "certificato valido"),
        ("HTTP redirect a HTTPS funzionante", "redirect 301"),
        ("TLS 1.2 minimo configurato", "no TLS 1.0/1.1"),
    ], styles, "CRITICAL"))

    story.append(Paragraph("22.4 Post-Deployment Verification", styles['SubSection']))
    story.append(make_checkbox_table([
        ("curl https://api.meepleai.com/health ritorna 200", "API accessibile"),
        ("https://meepleai.com carica homepage", "frontend accessibile"),
        ("Registrazione nuovo utente funzionante in prod", "flusso completo"),
        ("Login e accesso dashboard funzionante", "sessione persistente"),
        ("Chat AI risponde correttamente", "OpenRouter connesso"),
        ("Email inviata correttamente (SendGrid)", "verifica inbox"),
        ("Grafana accessibile (se monitoring attivo)", "dashboard populate"),
        ("Backup automatico configurato", "cron job attivo"),
        ("docker compose ps mostra tutti healthy", "nessun restart"),
    ], styles, "CRITICAL"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SECTION 23: ISSUES APERTE - VALUTAZIONE RISCHIO
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("23. Issues Aperte - Valutazione Rischio", styles['SectionTitle']))
    story.append(section_divider())

    story.append(Paragraph(
        "Le 80 issues aperte sono state valutate per impatto sul go-live. "
        "Nessuna e' bloccante per il lancio iniziale.",
        styles['TestItem']
    ))
    story.append(Spacer(1, 6))

    story.append(Paragraph("23.1 Non Bloccanti - Feature Future (post-launch)", styles['SubSection']))

    future_issues = [
        ["Categoria", "Issues", "Impatto Launch", "Note"],
        ["Publisher Portal\n(Phase 1)", "#5375-5392\n(18 issues)", "NESSUNO", "Feature futura, non\nnecessaria per MVP"],
        ["GDPR Compliance\nLLM", "#5506-5517\n(12 issues)", "BASSO*", "Privacy policy e DPA\nda completare prima\ndi utenti EU reali"],
        ["LLM Operational\nMaturity", "#5475-5478\n(4 issues)", "BASSO", "Monitoring avanzato,\nnon critico per alpha"],
        ["HybridLlmService\nRefactoring", "#5485-5492\n(5 issues)", "NESSUNO", "Refactoring interno,\nnessun impatto utente"],
        ["A/B Testing\nPlayground", "#5488-5505\n(11 issues)", "NESSUNO", "Feature avanzata,\npost-launch"],
        ["LLM Transparency\n& Editor", "#5479-5484\n(6 issues)", "NESSUNO", "UX improvement,\nnon bloccante"],
        ["Multi-Region\nPreparation", "#5518-5521\n(4 issues)", "NESSUNO", "Architettura futura,\nnon necessaria ora"],
        ["RuleSourceCard\nChat", "#5522-5528\n(7 issues)", "NESSUNO", "Feature UX chat,\npost-launch"],
        ["Processing Queue\n(Phase 3-4)", "#5455-5461\n(7 issues)", "NESSUNO", "Miglioramenti queue,\nbase gia funzionante"],
        ["Zero-Cost CI/CD", "#2967-2973\n(4 issues)", "NESSUNO", "Ottimizzazione CI,\nnon impatta launch"],
        ["Deferred Quality", "#5237-5241\n(3 issues)", "NESSUNO", "Tech debt, non\nimpatta funzionalita"],
    ]

    issue_table = Table(future_issues, colWidths=[35*mm, 28*mm, 28*mm, 55*mm])
    issue_table.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), AMBER_DARK),
        ('TEXTCOLOR', (0, 0), (-1, 0), WHITE),
        ('FONTNAME', (0, 0), (-1, 0), 'Helvetica-Bold'),
        ('FONTSIZE', (0, 0), (-1, -1), 8),
        ('LEADING', (0, 0), (-1, -1), 10),
        ('VALIGN', (0, 0), (-1, -1), 'TOP'),
        ('TOPPADDING', (0, 0), (-1, -1), 4),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 4),
        ('LEFTPADDING', (0, 0), (-1, -1), 4),
        ('GRID', (0, 0), (-1, -1), 0.5, BORDER_GRAY),
        ('ROWBACKGROUNDS', (0, 1), (-1, -1), [WHITE, GRAY_BG]),
    ]))
    story.append(issue_table)

    story.append(Spacer(1, 8))
    story.append(Paragraph(
        "* GDPR: La privacy policy e il DPA con OpenRouter dovrebbero essere completati "
        "prima di accettare utenti EU con dati reali. Per alpha/beta con utenti consapevoli, "
        "e' sufficiente un disclaimer.",
        styles['TestNote']
    ))

    story.append(Spacer(1, 12))
    story.append(Paragraph("23.2 Attenzione Pre-Launch", styles['SubSection']))
    story.append(Paragraph(
        "Elementi da verificare anche se non hanno issue aperte:",
        styles['TestItem']
    ))
    story.append(make_checkbox_table([
        ("Privacy Policy presente e completa su /privacy", "requisito legale"),
        ("Terms of Service presenti su /terms", "requisito legale"),
        ("Cookie banner implementato e funzionante", "GDPR/ePrivacy"),
        ("Disclaimer AI visibile agli utenti", "trasparenza AI"),
        ("Backup strategy testata (restore funzionante)", "disaster recovery"),
        ("Piano di rollback documentato", "se deployment fallisce"),
        ("Contatti supporto visibili", "email/form contatto"),
        ("Monitoring alert configurati (se Grafana attivo)", "notifiche critiche"),
    ], styles, "HIGH"))

    story.append(PageBreak())

    # ════════════════════════════════════════════════════════════════════
    # SUMMARY PAGE
    # ════════════════════════════════════════════════════════════════════
    story.append(Paragraph("Riepilogo Test Plan", styles['SectionTitle']))
    story.append(section_divider())

    summary_data = [
        ["Sezione", "Test", "Priorita", "Stima Tempo"],
        ["1. Infrastruttura", "22", "CRITICO", "1-2 ore"],
        ["2. Autenticazione", "33", "CRITICO", "2-3 ore"],
        ["3. Pagine Pubbliche", "19", "ALTO", "1 ora"],
        ["4. Registrazione", "9", "CRITICO", "30 min"],
        ["5. Dashboard/Nav", "11", "ALTO", "30 min"],
        ["6. Libreria", "17", "ALTO", "1 ora"],
        ["7. Catalogo/Discovery", "11", "ALTO", "45 min"],
        ["8. Sessioni", "14", "MEDIO", "1 ora"],
        ["9. Chat AI/RAG", "13", "CRITICO", "1-2 ore"],
        ["10. Knowledge Base", "8", "MEDIO", "30 min"],
        ["11. Profilo/Settings", "9", "MEDIO", "30 min"],
        ["12. Gamification", "5", "BASSO", "15 min"],
        ["13. Admin Overview", "11", "CRITICO", "45 min"],
        ["14. Admin Contenuti", "14", "ALTO", "1 ora"],
        ["15. Admin AI", "13", "ALTO", "1 ora"],
        ["16. Admin Utenti", "11", "MEDIO", "30 min"],
        ["17. Admin Monitor", "11", "MEDIO", "30 min"],
        ["18. API Health", "12", "CRITICO", "30 min"],
        ["19. Performance", "10", "ALTO", "1 ora"],
        ["20. Responsive", "14", "ALTO", "1-2 ore"],
        ["21. Sicurezza OWASP", "14", "CRITICO", "2-3 ore"],
        ["22. Deployment", "26", "CRITICO", "6-8 ore"],
        ["23. Issues Aperte", "8", "INFO", "30 min"],
        ["TOTALE", "~315", "", "~24-32 ore"],
    ]

    sum_table = Table(summary_data, colWidths=[40*mm, 15*mm, 25*mm, 25*mm])
    sum_table.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), AMBER_DARK),
        ('TEXTCOLOR', (0, 0), (-1, 0), WHITE),
        ('FONTNAME', (0, 0), (-1, 0), 'Helvetica-Bold'),
        ('FONTNAME', (0, -1), (-1, -1), 'Helvetica-Bold'),
        ('BACKGROUND', (0, -1), (-1, -1), AMBER_LIGHT),
        ('FONTSIZE', (0, 0), (-1, -1), 9),
        ('LEADING', (0, 0), (-1, -1), 12),
        ('VALIGN', (0, 0), (-1, -1), 'MIDDLE'),
        ('ALIGN', (1, 0), (1, -1), 'CENTER'),
        ('ALIGN', (2, 0), (2, -1), 'CENTER'),
        ('ALIGN', (3, 0), (3, -1), 'CENTER'),
        ('TOPPADDING', (0, 0), (-1, -1), 4),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 4),
        ('GRID', (0, 0), (-1, -1), 0.5, BORDER_GRAY),
        ('ROWBACKGROUNDS', (0, 1), (-1, -2), [WHITE, GRAY_BG]),
    ]))
    story.append(sum_table)

    story.append(Spacer(1, 15))
    story.append(Paragraph("Raccomandazione", styles['SubSection']))
    story.append(Paragraph(
        "Il sistema MeepleAI e' in uno stato maturo per un lancio alpha/beta. "
        "I 315 test manuali coprono tutte le funzionalita critiche. "
        "Le 80 issues aperte sono tutte feature future o miglioramenti, nessuna bloccante. "
        "Si raccomanda di eseguire almeno tutti i test CRITICO e ALTO (~180 test, ~16-20 ore) "
        "prima del go-live. I test MEDIO e BASSO possono essere eseguiti nella prima settimana post-launch.",
        styles['TestItem']
    ))

    story.append(Spacer(1, 10))
    story.append(Paragraph("Costi stimati infrastruttura:", styles['SubSubSection']))
    cost_data = [
        ["Voce", "Costo Mensile"],
        ["VPS Hetzner (CPX31: 4vCPU, 16GB, 160GB SSD)", "15.41 EUR"],
        ["Backup snapshots (7 giorni)", "3.08 EUR"],
        ["Dominio .com (Cloudflare)", "0.81 EUR"],
        ["SendGrid (free tier)", "0.00 EUR"],
        ["OpenRouter LLM (variabile)", "~5-20 EUR"],
        ["TOTALE (stima)", "~24-40 EUR/mese"],
    ]
    cost_table = Table(cost_data, colWidths=[100*mm, 50*mm])
    cost_table.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), SLATE_DARK),
        ('TEXTCOLOR', (0, 0), (-1, 0), WHITE),
        ('FONTNAME', (0, 0), (-1, 0), 'Helvetica-Bold'),
        ('FONTNAME', (0, -1), (-1, -1), 'Helvetica-Bold'),
        ('BACKGROUND', (0, -1), (-1, -1), GREEN_BG),
        ('FONTSIZE', (0, 0), (-1, -1), 9),
        ('ALIGN', (1, 0), (1, -1), 'RIGHT'),
        ('TOPPADDING', (0, 0), (-1, -1), 4),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 4),
        ('GRID', (0, 0), (-1, -1), 0.5, BORDER_GRAY),
        ('ROWBACKGROUNDS', (0, 1), (-1, -2), [WHITE, GRAY_BG]),
    ]))
    story.append(cost_table)

    story.append(Spacer(1, 20))
    story.append(HRFlowable(width="40%", thickness=1, color=AMBER_PRIMARY))
    story.append(Spacer(1, 6))
    story.append(Paragraph(
        f"Documento generato il {datetime.now().strftime('%d/%m/%Y alle %H:%M')} | MeepleAI Team",
        styles['Footer']
    ))

    # ── Build PDF ───────────────────────────────────────────────────────
    doc.build(story, canvasmaker=NumberedCanvas)
    print(f"PDF generato: {output_path}")
    return output_path

if __name__ == "__main__":
    path = build_test_plan()
    print(f"Completato: {path}")
