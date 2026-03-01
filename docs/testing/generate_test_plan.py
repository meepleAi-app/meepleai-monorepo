"""
MeepleAI UI Test Plan — PDF Generator
Generates docs/testing/ui-test-plan.pdf using reportlab
"""

from reportlab.lib.pagesizes import A4
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import cm
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    HRFlowable, PageBreak
)
from reportlab.lib.enums import TA_LEFT, TA_CENTER
import os

OUTPUT = os.path.join(os.path.dirname(__file__), "ui-test-plan.pdf")

# ─── Palette ────────────────────────────────────────────────────────────────
AMBER     = colors.HexColor("#F59E0B")
AMBER_LT  = colors.HexColor("#FEF3C7")
SLATE_900 = colors.HexColor("#0F172A")
SLATE_700 = colors.HexColor("#334155")
SLATE_200 = colors.HexColor("#E2E8F0")
SLATE_100 = colors.HexColor("#F1F5F9")
WHITE     = colors.white
GREEN     = colors.HexColor("#10B981")
BLUE      = colors.HexColor("#3B82F6")
PURPLE    = colors.HexColor("#8B5CF6")

# ─── Styles ─────────────────────────────────────────────────────────────────
styles = getSampleStyleSheet()

S_TITLE = ParagraphStyle("s_title", parent=styles["Title"],
    fontSize=26, textColor=SLATE_900, spaceAfter=4, leading=32)
S_SUBTITLE = ParagraphStyle("s_subtitle", parent=styles["Normal"],
    fontSize=12, textColor=SLATE_700, spaceAfter=2)
S_META = ParagraphStyle("s_meta", parent=styles["Normal"],
    fontSize=9, textColor=SLATE_700, spaceAfter=12)
S_H1 = ParagraphStyle("s_h1", parent=styles["Heading1"],
    fontSize=16, textColor=WHITE, spaceAfter=0, spaceBefore=18,
    leftIndent=0, leading=20)
S_H2 = ParagraphStyle("s_h2", parent=styles["Heading2"],
    fontSize=11, textColor=SLATE_900, spaceAfter=4, spaceBefore=10, leading=14)
S_BODY = ParagraphStyle("s_body", parent=styles["Normal"],
    fontSize=9, textColor=SLATE_700, spaceAfter=2, leading=13)
S_CODE = ParagraphStyle("s_code", parent=styles["Code"],
    fontSize=8, textColor=SLATE_900, backColor=SLATE_100,
    leftIndent=6, rightIndent=6, spaceAfter=4, leading=12,
    borderPad=3)
S_NOTE = ParagraphStyle("s_note", parent=styles["Normal"],
    fontSize=8, textColor=BLUE, spaceAfter=4, leading=11)

def section_header(title: str, color=AMBER):
    """Amber-band section header."""
    return Table(
        [[Paragraph(title, S_H1)]],
        colWidths=[17.6*cm],
        style=TableStyle([
            ("BACKGROUND", (0,0), (-1,-1), color),
            ("LEFTPADDING",  (0,0), (-1,-1), 10),
            ("RIGHTPADDING", (0,0), (-1,-1), 10),
            ("TOPPADDING",   (0,0), (-1,-1), 6),
            ("BOTTOMPADDING",(0,0), (-1,-1), 6),
            ("ROWBACKGROUNDS", (0,0), (-1,-1), [color]),
        ])
    )

def data_table(headers, rows, col_widths=None):
    """Styled data table with alternating rows."""
    data = [headers] + rows
    if col_widths is None:
        n = len(headers)
        col_widths = [17.6*cm/n]*n
    tbl = Table(data, colWidths=col_widths, repeatRows=1)
    style = TableStyle([
        # Header
        ("BACKGROUND",    (0,0), (-1,0), SLATE_900),
        ("TEXTCOLOR",     (0,0), (-1,0), WHITE),
        ("FONTNAME",      (0,0), (-1,0), "Helvetica-Bold"),
        ("FONTSIZE",      (0,0), (-1,0), 8),
        ("TOPPADDING",    (0,0), (-1,0), 5),
        ("BOTTOMPADDING", (0,0), (-1,0), 5),
        ("LEFTPADDING",   (0,0), (-1,-1), 5),
        ("RIGHTPADDING",  (0,0), (-1,-1), 5),
        # Alternating rows
        ("ROWBACKGROUNDS",(0,1), (-1,-1), [WHITE, SLATE_100]),
        ("FONTNAME",      (0,1), (-1,-1), "Helvetica"),
        ("FONTSIZE",      (0,1), (-1,-1), 7.5),
        ("TOPPADDING",    (0,1), (-1,-1), 4),
        ("BOTTOMPADDING", (0,1), (-1,-1), 4),
        ("VALIGN",        (0,0), (-1,-1), "TOP"),
        # Grid
        ("GRID", (0,0), (-1,-1), 0.4, SLATE_200),
        ("BOX",  (0,0), (-1,-1), 0.8, SLATE_700),
    ])
    tbl.setStyle(style)
    return tbl

def P(text, style=S_BODY): return Paragraph(text, style)
def SP(n=0.3): return Spacer(1, n*cm)
def HR(): return HRFlowable(width="100%", thickness=0.5, color=SLATE_200, spaceAfter=6)

# ─── Build story ────────────────────────────────────────────────────────────
def build():
    doc = SimpleDocTemplate(
        OUTPUT, pagesize=A4,
        leftMargin=1.8*cm, rightMargin=1.8*cm,
        topMargin=2*cm, bottomMargin=2*cm,
    )
    story = []

    # ── Cover ────────────────────────────────────────────────────────────────
    story += [
        SP(1),
        P("MeepleAI", ParagraphStyle("cover_tag", parent=S_META,
            fontSize=11, textColor=AMBER, fontName="Helvetica-Bold")),
        P("UI Test Plan", S_TITLE),
        P("Piano di test completo — tutte le pagine e feature implementate", S_SUBTITLE),
        SP(0.2),
        HR(),
        P("Versione: 2026-02-21  |  Strumento: Playwright E2E  |  Branch: main-dev", S_META),
        SP(0.5),
    ]

    # ── Legenda ──────────────────────────────────────────────────────────────
    story += [
        section_header("0. Legenda e Convenzioni"),
        SP(0.3),
        P("<b>Struttura dei test</b>: ogni riga della tabella corrisponde a un singolo test Playwright, "
          "organizzato in file <i>.spec.ts</i> sotto <code>apps/web/e2e/</code>.", S_BODY),
        SP(0.2),
        data_table(
            ["Colonna", "Significato"],
            [
                ["Test", "Nome del test / caso d'uso"],
                ["Azioni UI", "Sequenza di passi Playwright (goto → fill → click → expect)"],
                ["File spec", "Percorso del file .spec.ts relativo a apps/web/e2e/"],
            ],
            [4*cm, 13.6*cm]
        ),
        SP(0.4),
        P("⭐ = Feature recente implementata (Epic #4920, RAG Observability, Admin Dashboard)", S_NOTE),
        SP(0.3),
    ]

    # ── 1. Auth ──────────────────────────────────────────────────────────────
    story += [
        section_header("1. Auth Flow"),
        SP(0.3),
        data_table(
            ["Test", "Azioni UI", "File spec"],
            [
                ["Login",
                 "goto /auth/login → fill email+pwd → click Submit → expect /dashboard",
                 "auth/auth-complete.spec.ts"],
                ["Register",
                 "goto /auth/register → compila form → Submit → expect /auth/verification-pending",
                 "auth/auth-complete.spec.ts"],
                ["Reset Password",
                 "goto /auth/reset-password → inserisci email → Submit → expect messaggio conferma",
                 "auth/auth-complete.spec.ts"],
                ["OAuth",
                 "goto /auth/login → click OAuth button → mock redirect → expect /auth/oauth-callback",
                 "auth/auth-complete.spec.ts"],
            ],
            [3.5*cm, 9.5*cm, 4.6*cm]
        ),
        SP(0.3),
    ]

    # ── 2. Public ────────────────────────────────────────────────────────────
    story += [
        section_header("2. Homepage & Pagine Pubbliche"),
        SP(0.3),
        data_table(
            ["Test", "Azioni UI", "File spec"],
            [
                ["Homepage",
                 "goto / → expect hero visible → expect nav visible → expect CTA visible",
                 "public/public-pages.spec.ts"],
                ["Catalog giochi",
                 "goto /games → expect lista → fill ricerca → expect filtro applicato → click su un gioco",
                 "public/public-pages.spec.ts"],
                ["Dettaglio gioco (public)",
                 "goto /games/[id] → expect titolo → click tab FAQs → expect contenuto → click tab Rules",
                 "public/public-pages.spec.ts"],
                ["Sessioni pubbliche",
                 "goto /sessions → expect lista sessioni visibile",
                 "public/public-pages.spec.ts"],
            ],
            [3.5*cm, 9.5*cm, 4.6*cm]
        ),
        SP(0.3),
    ]

    # ── 3. Dashboard ─────────────────────────────────────────────────────────
    story += [
        section_header("3. Dashboard Utente (autenticato)"),
        SP(0.3),
        data_table(
            ["Test", "Azioni UI", "File spec"],
            [
                ["Dashboard",
                 "mock auth/me → goto /dashboard → expect widget visibili → expect nav laterale",
                 "user/dashboard.spec.ts"],
                ["Library",
                 "goto /library → expect collezione → click Wishlist → expect wishlist tab",
                 "user/library.spec.ts"],
                ["Library privata",
                 "goto /library/private → expect lista → click Aggiungi → goto /library/private/add → compila → Salva",
                 "user/library.spec.ts"],
                ["Play Records",
                 "goto /play-records → click Nuovo → goto /play-records/new → fill form → Salva → expect in lista",
                 "user/play-records.spec.ts"],
                ["Profilo",
                 "goto /profile → expect info utente → expect avatar → click Achievements tab",
                 "user/profile.spec.ts"],
                ["Notifiche",
                 "goto /notifications → expect lista notifiche → click notifica → expect read",
                 "user/notifications.spec.ts"],
            ],
            [3.5*cm, 9.5*cm, 4.6*cm]
        ),
        SP(0.3),
    ]

    # ── 4. Sessioni ───────────────────────────────────────────────────────────
    story += [
        section_header("4. Sessioni di Gioco"),
        SP(0.3),
        data_table(
            ["Test", "Azioni UI", "File spec"],
            [
                ["Session Notes",
                 "goto /sessions/[id]/notes → fill note → click Salva → expect nota in lista",
                 "user/game-sessions.spec.ts"],
                ["Session Players",
                 "goto /sessions/[id]/players → expect lista giocatori → expect score visibili",
                 "user/game-sessions.spec.ts"],
                ["Session State",
                 "goto /sessions/[id]/state → expect whiteboard/state panel → expect elementi interattivi",
                 "user/game-sessions.spec.ts"],
                ["Join Sessione",
                 "goto /sessions/join/[token] → mock token valido → expect redirect corretto",
                 "user/game-sessions.spec.ts"],
            ],
            [3.5*cm, 9.5*cm, 4.6*cm]
        ),
        SP(0.3),
    ]

    # ── 5. Toolkit & Chat ────────────────────────────────────────────────────
    story += [
        section_header("5. Toolkit & Chat AI"),
        SP(0.3),
        data_table(
            ["Test", "Azioni UI", "File spec"],
            [
                ["Toolkit",
                 "goto /toolkit → expect interfaccia → mock upload → expect documento caricato",
                 "user/toolkit-chat.spec.ts"],
                ["Chat - Nuova",
                 "goto /chat/new → seleziona agente → fill messaggio → mock SSE risposta → expect bolla AI",
                 "user/toolkit-chat.spec.ts"],
                ["Chat - Thread",
                 "goto /chat/[threadId] → expect storico → fill nuovo msg → expect risposta aggiunta",
                 "user/toolkit-chat.spec.ts"],
                ["Agenti lista",
                 "goto /agents → expect lista agenti → click agente → expect dettaglio",
                 "user/toolkit-chat.spec.ts"],
            ],
            [3.5*cm, 9.5*cm, 4.6*cm]
        ),
        SP(0.3),
    ]

    story.append(PageBreak())

    # ── 6. Admin Overview ────────────────────────────────────────────────────
    story += [
        section_header("6. Admin Dashboard — Overview ⭐", SLATE_700),
        SP(0.3),
        data_table(
            ["Test", "Azioni UI", "File spec"],
            [
                ["Overview KPIs",
                 "mock admin auth → goto /admin/overview → expect TopNav → expect Sidebar → expect KPI cards visibili",
                 "admin/admin-overview.spec.ts"],
                ["Activity Feed",
                 "goto /admin/overview/activity → expect lista eventi → expect timestamp → expect paginazione",
                 "admin/admin-overview.spec.ts"],
                ["System Health",
                 "goto /admin/overview/system → expect status servizi (API, DB, Redis, Qdrant) → expect uptime",
                 "admin/admin-overview.spec.ts"],
            ],
            [3.5*cm, 9.5*cm, 4.6*cm]
        ),
        SP(0.3),
    ]

    # ── 7. Admin Users ────────────────────────────────────────────────────────
    story += [
        section_header("7. Admin — Users", SLATE_700),
        SP(0.3),
        data_table(
            ["Test", "Azioni UI", "File spec"],
            [
                ["Users List",
                 "goto /admin/users → expect tabella utenti → fill ricerca → expect filtro → expect paginazione",
                 "admin/admin-users.spec.ts"],
                ["User Roles",
                 "goto /admin/users/roles → expect lista ruoli → click modifica ruolo → salva → expect toast conferma",
                 "admin/admin-users.spec.ts"],
                ["User Activity",
                 "goto /admin/users/activity → expect timeline attività → expect filtro per data",
                 "admin/admin-users.spec.ts"],
            ],
            [3.5*cm, 9.5*cm, 4.6*cm]
        ),
        SP(0.3),
    ]

    # ── 8. Admin Agents RAG ─────────────────────────────────────────────────
    story += [
        section_header("8. Admin — Agents & RAG Observability ⭐", SLATE_700),
        SP(0.3),
        data_table(
            ["Test", "Azioni UI", "File spec"],
            [
                ["Agents List",
                 "goto /admin/agents → expect lista agenti → expect nome e stato visibili",
                 "admin/rag-pipeline.spec.ts"],
                ["Pipeline Explorer ⭐",
                 "goto /admin/agents/pipeline → expect PipelineDiagram 6 nodi → click nodo → expect pannello dettaglio → verifica metriche",
                 "admin/rag-pipeline.spec.ts"],
                ["Debug Console ⭐",
                 "goto /admin/agents/debug → expect WaterfallChart → click step timeline → expect TimelineStep espanso → verifica token count",
                 "admin/rag-debug-console.spec.ts"],
                ["Strategy Config ⭐",
                 "goto /admin/agents/strategy → expect tier matrix → click cella → modifica modello → salva → expect toast conferma",
                 "admin/rag-strategy-config.spec.ts"],
                ["Models",
                 "goto /admin/agents/models → expect lista modelli AI → expect provider e versione",
                 "admin/rag-pipeline.spec.ts"],
                ["Chat History",
                 "goto /admin/agents/chat-history → expect storico conversazioni → filtro per agente",
                 "admin/rag-pipeline.spec.ts"],
            ],
            [3.8*cm, 9.2*cm, 4.6*cm]
        ),
        SP(0.3),
    ]

    # ── 9. Admin KB ──────────────────────────────────────────────────────────
    story += [
        section_header("9. Admin — Knowledge Base ⭐", SLATE_700),
        SP(0.3),
        data_table(
            ["Test", "Azioni UI", "File spec"],
            [
                ["KB Overview",
                 "goto /admin/knowledge-base → expect stats cards (docs totali, vettori, spazio) → expect link sezioni",
                 "admin/kb-documents-library.spec.ts"],
                ["Documents Library ⭐",
                 "goto /admin/knowledge-base/documents → expect tabella PDF → fill ricerca → bulk select 2 docs → click Reindex → expect toast",
                 "admin/kb-documents-library.spec.ts"],
                ["PDF Upload con progress ⭐",
                 "goto /admin/knowledge-base/upload → drag-drop file PDF → expect progress bar XHR → expect 100% → expect doc in lista",
                 "admin/kb-pdf-upload.spec.ts"],
                ["Vector Collections ⭐",
                 "goto /admin/knowledge-base/vectors → expect lista collezioni Qdrant → click collezione → expect vettori totali e dimensioni",
                 "admin/kb-vectors.spec.ts"],
                ["Embedding Config",
                 "goto /admin/knowledge-base/embedding → expect form configurazione → modifica modello → salva",
                 "admin/kb-documents-library.spec.ts"],
                ["Processing Queue",
                 "goto /admin/knowledge-base/queue → expect coda documenti → expect stato processing",
                 "admin/kb-documents-library.spec.ts"],
            ],
            [3.8*cm, 9.2*cm, 4.6*cm]
        ),
        SP(0.3),
    ]

    # ── 10. Admin Shared Games ───────────────────────────────────────────────
    story += [
        section_header("10. Admin — Shared Games & PDF Upload ⭐", SLATE_700),
        SP(0.3),
        data_table(
            ["Test", "Azioni UI", "File spec"],
            [
                ["Shared Games List",
                 "goto /admin/shared-games → expect tabella community games → expect status (Draft/Published/Archived)",
                 "admin/shared-game-pdf-upload.spec.ts"],
                ["Shared Game Detail + PDF Upload ⭐",
                 "goto /admin/shared-games/[id] → scroll PdfUploadSection → drag PDF → expect progress XHR → expect documento in lista KB",
                 "admin/shared-game-pdf-upload.spec.ts"],
                ["New Shared Game",
                 "goto /admin/shared-games/new → fill form (title, players, time) → click Crea → expect redirect a dettaglio",
                 "admin/shared-game-pdf-upload.spec.ts"],
                ["Game Approvals",
                 "goto /admin/shared-games/approvals → expect coda approvazioni → approva gioco → expect status Published",
                 "admin/shared-game-pdf-upload.spec.ts"],
            ],
            [3.8*cm, 9.2*cm, 4.6*cm]
        ),
        SP(0.3),
    ]

    story.append(PageBreak())

    # ── 11. Agent Builder ────────────────────────────────────────────────────
    story += [
        section_header("11. Admin — Agent Builder + KB Cards ⭐", SLATE_700),
        SP(0.3),
        data_table(
            ["Test", "Azioni UI", "File spec"],
            [
                ["Agent Builder form",
                 "goto /admin/agents/builder → fill nome agente → seleziona modello → fill prompt → click Salva",
                 "admin/agent-builder-kb-cards.spec.ts"],
                ["KB Cards checklist ⭐",
                 "apri AgentBuilderModal → click tab KB Cards → expect lista card disponibili → seleziona 2 card → Salva → expect kbCardIds in response",
                 "admin/agent-builder-kb-cards.spec.ts"],
                ["Agent linked-game ⭐",
                 "GET /api/v1/admin/agents/[id]/linked-agent → expect SharedGameId in payload → verifica associazione",
                 "admin/agent-builder-kb-cards.spec.ts"],
                ["Test agente",
                 "goto /admin/games/[id]/agent/test → fill prompt → expect risposta agente con KB card attiva",
                 "admin/agent-builder-kb-cards.spec.ts"],
            ],
            [3.8*cm, 9.2*cm, 4.6*cm]
        ),
        SP(0.3),
    ]

    # ── 12. MeepleCard ───────────────────────────────────────────────────────
    story += [
        section_header("12. MeepleCard System ⭐", SLATE_700),
        SP(0.3),
        data_table(
            ["Test", "Azioni UI", "File spec"],
            [
                ["Grid view",
                 "goto /games → expect MeepleCard variant=grid per ogni gioco → expect immagine, titolo, rating visibili",
                 "meeple-card-features.spec.ts"],
                ["List view",
                 "goto /games → click toggle vista lista → expect MeepleCard variant=list → expect info compatta",
                 "meeple-card-features.spec.ts"],
                ["Hover preview",
                 "hover su card → expect popover con info aggiuntive → expect descrizione e anno",
                 "meeple-card-features.spec.ts"],
                ["Quick actions",
                 "hover card → expect QuickActions visibili → click wishlist icon → expect toast aggiunto a wishlist",
                 "meeple-card-features.spec.ts"],
                ["Bulk select",
                 "goto /library → attiva bulk mode → click 3 cards → expect counter selezione → click Azione bulk",
                 "meeple-card-features.spec.ts"],
            ],
            [3.5*cm, 9.5*cm, 4.6*cm]
        ),
        SP(0.3),
    ]

    # ── 13. Flusso Integrato ─────────────────────────────────────────────────
    story += [
        section_header("13. Flusso Integrato End-to-End ⭐ (Epic #4920)", BLUE),
        SP(0.3),
        P("<b>Obiettivo</b>: verificare il flusso completo SharedGame → PDF → KB → Agent Builder", S_BODY),
        SP(0.2),
        data_table(
            ["Step", "Azione", "Verifica"],
            [
                ["1", "goto /admin/shared-games/[id]", "PdfUploadSection visibile"],
                ["2", "Upload PDF via drag-drop", "Progress bar XHR → 100%"],
                ["3", "goto /admin/knowledge-base/documents", "PDF appare in tabella con SharedGameId"],
                ["4", "goto /admin/agents/builder → apri modal", "AgentBuilderModal si apre"],
                ["5", "Seleziona KB card del gioco", "Checkbox checked, counter aggiornato"],
                ["6", "Salva agente", "Response contiene kbCardIds con UUID card"],
                ["7", "goto /admin/games/[id]/agent/test → test query", "Risposta cita contenuto del PDF caricato"],
            ],
            [1.5*cm, 7*cm, 9.1*cm]
        ),
        SP(0.2),
        P("File spec: <b>e2e/integrated-epic4920.spec.ts</b>", S_NOTE),
        SP(0.3),
    ]

    # ── 14. Smoke Test ───────────────────────────────────────────────────────
    story += [
        section_header("14. Smoke Test (8 step — eseguire prima di ogni deploy)", GREEN),
        SP(0.3),
        data_table(
            ["#", "URL", "Verifica minima"],
            [
                ["1", "/",                             "Homepage carica (expect h1 o hero)"],
                ["2", "/auth/login",                   "Form login visibile (expect input email)"],
                ["3", "/dashboard",                    "Dashboard carica dopo auth mock"],
                ["4", "/games",                        "Catalog carica con MeepleCard grid"],
                ["5", "/admin/overview",               "Admin dashboard carica (expect TopNav)"],
                ["6", "/admin/agents/pipeline",        "Pipeline Diagram visibile (expect 6 nodi)"],
                ["7", "/admin/knowledge-base/documents","Documents table carica (expect thead)"],
                ["8", "/chat/new",                     "Chat interface carica (expect input messaggio)"],
            ],
            [1*cm, 5.5*cm, 11.1*cm]
        ),
        SP(0.2),
        P("File spec: <b>e2e/smoke.spec.ts</b>  |  Comando: <code>pnpm test:e2e e2e/smoke.spec.ts</code>", S_NOTE),
        SP(0.3),
    ]

    # ── File Map ─────────────────────────────────────────────────────────────
    story += [
        section_header("15. Mappa File Playwright (apps/web/e2e/)"),
        SP(0.3),
        data_table(
            ["File", "Sezione"],
            [
                ["smoke.spec.ts",                        "Smoke test 8-step pre-deploy"],
                ["auth/auth-complete.spec.ts",           "Login, Register, Reset, OAuth"],
                ["public/public-pages.spec.ts",          "Homepage, Catalog, Dettaglio, Sessioni"],
                ["user/dashboard.spec.ts",               "Dashboard utente"],
                ["user/library.spec.ts",                 "Library, Private, Wishlist"],
                ["user/play-records.spec.ts",            "Play Records CRUD"],
                ["user/game-sessions.spec.ts",           "Note, Players, State, Join"],
                ["user/toolkit-chat.spec.ts",            "Toolkit, Chat, Agenti"],
                ["user/profile.spec.ts",                 "Profilo, Achievements"],
                ["user/notifications.spec.ts",           "Notifiche"],
                ["admin/admin-overview.spec.ts",         "Admin Overview + Activity + System Health"],
                ["admin/admin-users.spec.ts",            "Admin Users + Roles + Activity"],
                ["admin/rag-pipeline.spec.ts",           "Admin Agents + Pipeline Explorer ⭐"],
                ["admin/rag-debug-console.spec.ts",      "Admin Debug Console (Waterfall) ⭐"],
                ["admin/rag-strategy-config.spec.ts",    "Admin Strategy Config (Tier Matrix) ⭐"],
                ["admin/kb-documents-library.spec.ts",   "KB Documents + Overview + Embedding ⭐"],
                ["admin/kb-pdf-upload.spec.ts",          "KB PDF Upload XHR progress ⭐"],
                ["admin/kb-vectors.spec.ts",             "KB Vector Collections ⭐"],
                ["admin/shared-game-pdf-upload.spec.ts", "Shared Games + PDF Upload ⭐"],
                ["admin/agent-builder-kb-cards.spec.ts", "Agent Builder + KB Cards checklist ⭐"],
                ["meeple-card-features.spec.ts",         "MeepleCard grid/list/hover/quickactions/bulk ⭐"],
                ["integrated-epic4920.spec.ts",          "Flusso integrato E2E Epic #4920 ⭐"],
            ],
            [8.2*cm, 9.4*cm]
        ),
        SP(0.3),
        P("⭐ = feature recente — priorità alta nei test di regressione", S_NOTE),
    ]

    doc.build(story)
    print(f"[OK] PDF generato: {OUTPUT}")

if __name__ == "__main__":
    build()
