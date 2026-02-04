"""
Generate Skills Reference PDF with MeepleAI brand styling
"""
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import cm, mm
from reportlab.lib.colors import HexColor
from reportlab.pdfgen import canvas
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak
from reportlab.lib.enums import TA_LEFT, TA_CENTER
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
import os

# MeepleAI Brand Colors
ORANGE_PRIMARY = HexColor("#d2691e")
PURPLE_ACCENT = HexColor("#8b5cf6")
WARM_BG = HexColor("#faf8f3")
DARK_TEXT = HexColor("#1a1a1a")
GRAY_TEXT = HexColor("#666666")
LIGHT_GRAY = HexColor("#e5e5e5")
WHITE = HexColor("#ffffff")

def create_skills_pdf(output_path):
    doc = SimpleDocTemplate(
        output_path,
        pagesize=A4,
        rightMargin=1.5*cm,
        leftMargin=1.5*cm,
        topMargin=2*cm,
        bottomMargin=2*cm
    )

    styles = getSampleStyleSheet()

    # Custom styles
    title_style = ParagraphStyle(
        'CustomTitle',
        parent=styles['Title'],
        fontSize=24,
        textColor=ORANGE_PRIMARY,
        spaceAfter=20,
        alignment=TA_CENTER
    )

    subtitle_style = ParagraphStyle(
        'Subtitle',
        parent=styles['Normal'],
        fontSize=12,
        textColor=GRAY_TEXT,
        spaceAfter=30,
        alignment=TA_CENTER
    )

    heading_style = ParagraphStyle(
        'CustomHeading',
        parent=styles['Heading1'],
        fontSize=16,
        textColor=PURPLE_ACCENT,
        spaceBefore=20,
        spaceAfter=10
    )

    subheading_style = ParagraphStyle(
        'SubHeading',
        parent=styles['Heading2'],
        fontSize=12,
        textColor=ORANGE_PRIMARY,
        spaceBefore=15,
        spaceAfter=8
    )

    normal_style = ParagraphStyle(
        'CustomNormal',
        parent=styles['Normal'],
        fontSize=9,
        textColor=DARK_TEXT,
        spaceAfter=6
    )

    code_style = ParagraphStyle(
        'Code',
        parent=styles['Code'],
        fontSize=8,
        textColor=DARK_TEXT,
        backColor=LIGHT_GRAY,
        leftIndent=10,
        rightIndent=10,
        spaceBefore=4,
        spaceAfter=4
    )

    story = []

    # Title
    story.append(Paragraph("Claude Code Skills Reference", title_style))
    story.append(Paragraph("Guida completa per sviluppo e manutenzione applicazioni web", subtitle_style))
    story.append(Paragraph("MeepleAI - Febbraio 2026", normal_style))
    story.append(Spacer(1, 20))

    # Table style for skills
    table_style = TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), ORANGE_PRIMARY),
        ('TEXTCOLOR', (0, 0), (-1, 0), WHITE),
        ('ALIGN', (0, 0), (-1, -1), 'LEFT'),
        ('FONTSIZE', (0, 0), (-1, 0), 9),
        ('FONTSIZE', (0, 1), (-1, -1), 8),
        ('BOTTOMPADDING', (0, 0), (-1, 0), 8),
        ('TOPPADDING', (0, 0), (-1, 0), 8),
        ('BOTTOMPADDING', (0, 1), (-1, -1), 5),
        ('TOPPADDING', (0, 1), (-1, -1), 5),
        ('BACKGROUND', (0, 1), (-1, -1), WHITE),
        ('GRID', (0, 0), (-1, -1), 0.5, LIGHT_GRAY),
        ('ROWBACKGROUNDS', (0, 1), (-1, -1), [WHITE, HexColor("#fafafa")]),
        ('VALIGN', (0, 0), (-1, -1), 'TOP'),
    ])

    col_widths = [3.5*cm, 5*cm, 8*cm]

    # ==================== SECTION 1: SuperClaude Commands ====================
    story.append(Paragraph("1. SuperClaude Commands (/sc:*)", heading_style))

    # Analisi e Ricerca
    story.append(Paragraph("Analisi e Ricerca", subheading_style))
    data = [
        ['Skill', 'Uso', 'Esempio'],
        ['/sc:analyze', 'Analisi code quality, security, performance', '/sc:analyze src/auth --focus security'],
        ['/sc:research', 'Ricerca web con fonti multiple', '/sc:research "React best practices 2026"'],
        ['/sc:explain', 'Spiegazione codice/concetti', '/sc:explain src/services/auth.ts'],
        ['/sc:troubleshoot', 'Diagnosi e risoluzione problemi', '/sc:troubleshoot "API returns 500"'],
    ]
    t = Table(data, colWidths=col_widths)
    t.setStyle(table_style)
    story.append(t)
    story.append(Spacer(1, 10))

    # Pianificazione e Design
    story.append(Paragraph("Pianificazione e Design", subheading_style))
    data = [
        ['Skill', 'Uso', 'Esempio'],
        ['/sc:brainstorm', 'Discovery interattiva requisiti', '/sc:brainstorm "sistema notifiche push"'],
        ['/sc:design', 'Progettazione architettura e API', '/sc:design "microservice authentication"'],
        ['/sc:estimate', 'Stima effort per task/feature', '/sc:estimate "implementare OAuth2"'],
        ['/sc:workflow', 'Genera workflow da PRD', '/sc:workflow @docs/prd/feature.md'],
        ['/sc:spec-panel', 'Review specifiche con esperti', '/sc:spec-panel @api-spec.yaml'],
        ['/sc:business-panel', 'Analisi business (9 esperti)', '/sc:business-panel @strategy.pdf'],
    ]
    t = Table(data, colWidths=col_widths)
    t.setStyle(table_style)
    story.append(t)
    story.append(Spacer(1, 10))

    # Implementazione
    story.append(Paragraph("Implementazione", subheading_style))
    data = [
        ['Skill', 'Uso', 'Esempio'],
        ['/sc:implement', 'Implementazione feature completa', '/sc:implement "add logout button"'],
        ['/sc:improve', 'Miglioramenti sistematici', '/sc:improve src/utils --focus performance'],
        ['/sc:cleanup', 'Pulizia e dead code removal', '/sc:cleanup src/legacy'],
        ['/sc:build', 'Build con error handling', '/sc:build --target production'],
        ['/sc:test', 'Test con coverage analysis', '/sc:test src/services --coverage'],
    ]
    t = Table(data, colWidths=col_widths)
    t.setStyle(table_style)
    story.append(t)
    story.append(Spacer(1, 10))

    # Documentazione e Git
    story.append(Paragraph("Documentazione e Git", subheading_style))
    data = [
        ['Skill', 'Uso', 'Esempio'],
        ['/sc:document', 'Genera documentazione', '/sc:document src/api --type api'],
        ['/sc:index', 'Genera knowledge base', '/sc:index --output docs/'],
        ['/sc:git', 'Git con commit intelligenti', '/sc:git commit --smart-commit'],
    ]
    t = Table(data, colWidths=col_widths)
    t.setStyle(table_style)
    story.append(t)
    story.append(Spacer(1, 10))

    # Session & Orchestration
    story.append(Paragraph("Session e Orchestrazione", subheading_style))
    data = [
        ['Skill', 'Uso', 'Esempio'],
        ['/sc:load', 'Carica contesto progetto', '/sc:load'],
        ['/sc:save', 'Salva sessione corrente', '/sc:save'],
        ['/sc:reflect', 'Riflessione e validazione', '/sc:reflect'],
        ['/sc:pm', 'Project Manager orchestration', '/sc:pm "coordinate sprint"'],
        ['/sc:spawn', 'Task orchestration', '/sc:spawn "migrate database"'],
        ['/sc:task', 'Esecuzione task complessi', '/sc:task "setup CI/CD"'],
    ]
    t = Table(data, colWidths=col_widths)
    t.setStyle(table_style)
    story.append(t)

    story.append(PageBreak())

    # ==================== SECTION 2: Design & Visual ====================
    story.append(Paragraph("2. Design e Visual", heading_style))

    story.append(Paragraph("Creazione Grafiche", subheading_style))
    data = [
        ['Skill', 'Uso', 'Esempio'],
        ['/canvas-design', 'PDF/PNG con design philosophy', '/canvas-design "RAG architecture infographic"'],
        ['/algorithmic-art', 'Arte generativa p5.js', '/algorithmic-art "flow field visualization"'],
        ['/frontend-design', 'UI distintiva non-generic', '/frontend-design "login brutalist style"'],
        ['/slack-gif-creator', 'GIF animate per Slack', '/slack-gif-creator "loading spinner"'],
        ['/theme-factory', 'Applica temi ad artifact', '/theme-factory "apply sunset theme"'],
        ['/brand-guidelines', 'Applica brand guidelines', '/brand-guidelines @presentation.pptx'],
    ]
    t = Table(data, colWidths=col_widths)
    t.setStyle(table_style)
    story.append(t)
    story.append(Spacer(1, 15))

    # ==================== SECTION 3: Documents ====================
    story.append(Paragraph("3. Generazione Documenti", heading_style))

    data = [
        ['Skill', 'Uso', 'Esempio'],
        ['/pdf', 'Manipolazione PDF completa', '/pdf merge doc1.pdf doc2.pdf'],
        ['/pdf', 'Estrazione testo/tabelle', '/pdf extract-text report.pdf'],
        ['/docx', 'Crea/modifica Word', '/docx create "spec" --template formal'],
        ['/pptx', 'Crea/modifica presentazioni', '/pptx create "review" --slides 10'],
        ['/xlsx', 'Crea/analizza spreadsheet', '/xlsx analyze data.xlsx --pivot'],
        ['/web-artifacts-builder', 'Artifact React complessi', '/web-artifacts-builder "dashboard"'],
    ]
    t = Table(data, colWidths=col_widths)
    t.setStyle(table_style)
    story.append(t)
    story.append(Spacer(1, 15))

    # ==================== SECTION 4: Testing ====================
    story.append(Paragraph("4. Testing e Quality", heading_style))

    data = [
        ['Skill', 'Uso', 'Esempio'],
        ['/webapp-testing', 'Test app con Playwright', '/webapp-testing localhost:3000 --flow login'],
        ['/code-review', 'Review PR approfondita', '/code-review #123'],
        ['/security-guidance', 'Guida sicurezza', '/security-guidance "JWT implementation"'],
        ['/feature-dev', 'Sviluppo guidato feature', '/feature-dev "user profile page"'],
    ]
    t = Table(data, colWidths=col_widths)
    t.setStyle(table_style)
    story.append(t)
    story.append(Spacer(1, 15))

    # ==================== SECTION 5: MCP Servers ====================
    story.append(Paragraph("5. MCP Servers", heading_style))

    story.append(Paragraph("Design e UI", subheading_style))
    data = [
        ['Server', 'Uso', 'Trigger'],
        ['superdesign', 'UI, wireframe, logo, icons', '"design a dashboard layout"'],
        ['figma', 'Design-to-code da Figma', 'Figma URL o /figma'],
        ['magic', 'UI components 21st.dev', '/ui, /21, component requests'],
    ]
    t = Table(data, colWidths=col_widths)
    t.setStyle(table_style)
    story.append(t)
    story.append(Spacer(1, 10))

    story.append(Paragraph("Code Intelligence", subheading_style))
    data = [
        ['Server', 'Uso', 'Trigger'],
        ['sequential', 'Multi-step reasoning', '--think, --think-hard, complex analysis'],
        ['morphllm', 'Bulk pattern edits', 'Multi-file transformations'],
        ['serena', 'Symbol ops, memory', '/sc:load, /sc:save, refactoring'],
        ['tavily', 'Web search avanzato', '/sc:research, current info needs'],
    ]
    t = Table(data, colWidths=col_widths)
    t.setStyle(table_style)
    story.append(t)
    story.append(Spacer(1, 10))

    story.append(Paragraph("Browser e Services", subheading_style))
    data = [
        ['Server', 'Uso', 'Trigger'],
        ['playwright', 'E2E testing, screenshots', 'Browser testing, visual validation'],
        ['chrome-devtools', 'Performance, debugging', 'Performance audit, console errors'],
        ['github-project-manager', 'GitHub issues/PRs', 'Issue management, PR creation'],
        ['knowledge-graph', 'Graph knowledge', 'Entity relationships'],
    ]
    t = Table(data, colWidths=col_widths)
    t.setStyle(table_style)
    story.append(t)

    story.append(PageBreak())

    # ==================== SECTION 6: Quick Reference ====================
    story.append(Paragraph("6. Quick Reference", heading_style))

    story.append(Paragraph("Ciclo Sviluppo Tipico", subheading_style))
    workflow_text = """
    1. /sc:load                          # Inizio sessione
    2. /sc:brainstorm "feature X"        # Pianificazione
    3. /sc:design "architettura X"       # Design
    4. /sc:implement "feature X"         # Implementazione
    5. /sc:test src/features/x           # Testing
    6. /code-review                      # Review
    7. /sc:document src/features/x       # Documentazione
    8. /sc:git commit --smart-commit     # Commit
    9. /sc:save                          # Fine sessione
    """
    story.append(Paragraph(workflow_text.replace('\n', '<br/>'), code_style))
    story.append(Spacer(1, 15))

    story.append(Paragraph("Flags Utili", subheading_style))
    data = [
        ['Flag', 'Effetto'],
        ['--think', 'Analisi strutturata (~4K tokens)'],
        ['--think-hard', 'Analisi approfondita (~10K tokens)'],
        ['--ultrathink', 'Massima profondita (~32K tokens)'],
        ['--uc', 'Ultra-compressed output'],
        ['--focus security', 'Focus su sicurezza'],
        ['--focus performance', 'Focus su performance'],
        ['--delegate', 'Sub-agent delegation'],
        ['--validate', 'Pre-execution validation'],
    ]
    t = Table(data, colWidths=[4*cm, 12*cm])
    t.setStyle(table_style)
    story.append(t)
    story.append(Spacer(1, 15))

    story.append(Paragraph("Troubleshooting Quick Commands", subheading_style))
    troubleshoot_text = """
    /sc:troubleshoot "error message"     # Problema generico
    /sc:analyze src/auth --focus security # Analisi sicurezza
    /sc:analyze src/api --focus performance # Performance
    /webapp-testing localhost:3000       # Test E2E
    """
    story.append(Paragraph(troubleshoot_text.replace('\n', '<br/>'), code_style))

    # Footer
    story.append(Spacer(1, 30))
    story.append(Paragraph("---", normal_style))
    story.append(Paragraph("MeepleAI Skills Reference v1.0 | Febbraio 2026",
                          ParagraphStyle('Footer', parent=normal_style,
                                        textColor=GRAY_TEXT, alignment=TA_CENTER)))
    story.append(Paragraph("Single Source: docs/SKILLS-REFERENCE.md",
                          ParagraphStyle('Footer', parent=normal_style,
                                        textColor=GRAY_TEXT, alignment=TA_CENTER, fontSize=8)))

    # Build PDF
    doc.build(story)
    print(f"[OK] PDF created: {output_path}")

if __name__ == "__main__":
    output_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(output_dir)

    # Output to docs folder
    output_path = os.path.join(project_root, "docs", "skills-reference.pdf")
    create_skills_pdf(output_path)

    # Also copy to public docs
    public_path = os.path.join(project_root, "apps", "web", "public", "docs", "skills-reference.pdf")
    os.makedirs(os.path.dirname(public_path), exist_ok=True)

    import shutil
    shutil.copy(output_path, public_path)
    print(f"[OK] Copied to: {public_path}")
