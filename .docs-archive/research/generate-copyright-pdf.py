"""Generate PDF report: Board Game PDF Copyright Analysis for MeepleAI RAG System"""

from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.colors import HexColor, black, white
from reportlab.lib.units import mm, cm
from reportlab.lib.enums import TA_LEFT, TA_CENTER, TA_JUSTIFY
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    PageBreak, KeepTogether, HRFlowable
)
from reportlab.platypus.doctemplate import PageTemplate, BaseDocTemplate, Frame
from reportlab.lib.pagesizes import A4
from reportlab.pdfgen import canvas as pdfcanvas
import os

OUTPUT_PATH = os.path.join(os.path.dirname(__file__), "board-game-pdf-copyright-analysis.pdf")

# --- Colors ---
DARK_BG = HexColor("#1a1a2e")
ACCENT_AMBER = HexColor("#f59e0b")
ACCENT_RED = HexColor("#ef4444")
ACCENT_GREEN = HexColor("#22c55e")
ACCENT_BLUE = HexColor("#3b82f6")
LIGHT_GRAY = HexColor("#f3f4f6")
MID_GRAY = HexColor("#6b7280")
DARK_TEXT = HexColor("#111827")
TABLE_HEADER_BG = HexColor("#1e293b")
TABLE_ALT_ROW = HexColor("#f8fafc")
HIGH_RISK_BG = HexColor("#fef2f2")
LOW_RISK_BG = HexColor("#f0fdf4")
MEDIUM_RISK_BG = HexColor("#fffbeb")

# --- Page Setup ---
PAGE_W, PAGE_H = A4
MARGIN = 2 * cm


def create_styles():
    styles = getSampleStyleSheet()

    styles.add(ParagraphStyle(
        "CoverTitle", parent=styles["Title"],
        fontSize=28, leading=34, textColor=DARK_BG,
        spaceAfter=6 * mm, alignment=TA_CENTER,
        fontName="Helvetica-Bold"
    ))
    styles.add(ParagraphStyle(
        "CoverSubtitle", parent=styles["Normal"],
        fontSize=14, leading=18, textColor=MID_GRAY,
        spaceAfter=4 * mm, alignment=TA_CENTER,
        fontName="Helvetica"
    ))
    styles.add(ParagraphStyle(
        "SectionH1", parent=styles["Heading1"],
        fontSize=18, leading=22, textColor=DARK_BG,
        spaceBefore=12 * mm, spaceAfter=4 * mm,
        fontName="Helvetica-Bold",
        borderWidth=0, borderColor=ACCENT_AMBER,
        borderPadding=0
    ))
    styles.add(ParagraphStyle(
        "SectionH2", parent=styles["Heading2"],
        fontSize=14, leading=18, textColor=HexColor("#334155"),
        spaceBefore=8 * mm, spaceAfter=3 * mm,
        fontName="Helvetica-Bold"
    ))
    styles.add(ParagraphStyle(
        "SectionH3", parent=styles["Heading3"],
        fontSize=12, leading=15, textColor=HexColor("#475569"),
        spaceBefore=5 * mm, spaceAfter=2 * mm,
        fontName="Helvetica-Bold"
    ))
    styles.add(ParagraphStyle(
        "BodyText2", parent=styles["Normal"],
        fontSize=10, leading=14, textColor=DARK_TEXT,
        spaceAfter=3 * mm, alignment=TA_JUSTIFY,
        fontName="Helvetica"
    ))
    styles.add(ParagraphStyle(
        "ExpertQuote", parent=styles["Normal"],
        fontSize=10, leading=14, textColor=HexColor("#4b5563"),
        spaceAfter=4 * mm, leftIndent=12 * mm, rightIndent=6 * mm,
        fontName="Helvetica-Oblique",
        borderWidth=1, borderColor=ACCENT_AMBER, borderPadding=6,
        backColor=HexColor("#fffbeb")
    ))
    styles.add(ParagraphStyle(
        "BulletItem", parent=styles["Normal"],
        fontSize=10, leading=14, textColor=DARK_TEXT,
        spaceAfter=1.5 * mm, leftIndent=8 * mm,
        bulletIndent=3 * mm, fontName="Helvetica"
    ))
    styles.add(ParagraphStyle(
        "SmallNote", parent=styles["Normal"],
        fontSize=8, leading=10, textColor=MID_GRAY,
        fontName="Helvetica"
    ))
    styles.add(ParagraphStyle(
        "RiskHigh", parent=styles["Normal"],
        fontSize=10, leading=14, textColor=ACCENT_RED,
        fontName="Helvetica-Bold"
    ))
    styles.add(ParagraphStyle(
        "RiskLow", parent=styles["Normal"],
        fontSize=10, leading=14, textColor=ACCENT_GREEN,
        fontName="Helvetica-Bold"
    ))
    styles.add(ParagraphStyle(
        "Footer", parent=styles["Normal"],
        fontSize=8, leading=10, textColor=MID_GRAY,
        alignment=TA_CENTER, fontName="Helvetica"
    ))
    return styles


def make_table(data, col_widths=None, header_bg=TABLE_HEADER_BG):
    """Create a styled table."""
    style_cmds = [
        ("BACKGROUND", (0, 0), (-1, 0), header_bg),
        ("TEXTCOLOR", (0, 0), (-1, 0), white),
        ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTSIZE", (0, 0), (-1, 0), 9),
        ("FONTNAME", (0, 1), (-1, -1), "Helvetica"),
        ("FONTSIZE", (0, 1), (-1, -1), 9),
        ("LEADING", (0, 0), (-1, -1), 12),
        ("ALIGN", (0, 0), (-1, -1), "LEFT"),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("GRID", (0, 0), (-1, -1), 0.5, HexColor("#e2e8f0")),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
    ]
    # Alternate row colors
    for i in range(1, len(data)):
        if i % 2 == 0:
            style_cmds.append(("BACKGROUND", (0, i), (-1, i), TABLE_ALT_ROW))

    t = Table(data, colWidths=col_widths, repeatRows=1)
    t.setStyle(TableStyle(style_cmds))
    return t


def hr():
    return HRFlowable(width="100%", thickness=0.5, color=HexColor("#e2e8f0"),
                       spaceAfter=4 * mm, spaceBefore=2 * mm)


def build_pdf():
    styles = create_styles()
    story = []
    avail_w = PAGE_W - 2 * MARGIN

    # ==================== COVER PAGE ====================
    story.append(Spacer(1, 40 * mm))
    story.append(Paragraph("Board Game PDF Copyright Analysis", styles["CoverTitle"]))
    story.append(Paragraph("for MeepleAI RAG System", styles["CoverSubtitle"]))
    story.append(Spacer(1, 8 * mm))
    story.append(HRFlowable(width="60%", thickness=2, color=ACCENT_AMBER,
                             spaceAfter=8 * mm, spaceBefore=4 * mm))
    story.append(Paragraph("Expert Spec Panel Review", styles["CoverSubtitle"]))
    story.append(Spacer(1, 6 * mm))

    cover_info = [
        ["Panel", "Wiegers (Requirements), Fowler (Architecture), Nygard (Risk), Crispin (Quality)"],
        ["Mode", "Discussion + Critique"],
        ["Focus", "Compliance, Architecture, Risk"],
        ["Date", "2026-03-07"],
        ["Depth", "Ultra-Think (comprehensive research with 3 parallel agents)"],
    ]
    ct = make_table(cover_info, col_widths=[3.5 * cm, avail_w - 3.5 * cm])
    story.append(ct)

    story.append(Spacer(1, 15 * mm))
    story.append(Paragraph(
        "<b>CONFIDENTIAL</b> - Internal use only. This document does not constitute legal advice. "
        "Consult qualified IP counsel before making business decisions based on this analysis.",
        styles["SmallNote"]))

    story.append(PageBreak())

    # ==================== EXECUTIVE SUMMARY ====================
    story.append(Paragraph("Executive Summary", styles["SectionH1"]))
    story.append(Paragraph(
        "MeepleAI's use of board game rulebook PDFs in its RAG system raises significant copyright questions. "
        "The legal landscape is <b>actively evolving</b> with major cases (NYT v. OpenAI, Cohere v. Publishers, "
        "Thomson Reuters v. Ross) shaping precedent in 2025-2026.",
        styles["BodyText2"]))

    summary_data = [
        ["#", "Solution", "Risk", "Scalability", "Cost"],
        ["1", "Publisher licensing\n(explicit consent)", "ZERO", "Medium\n(depends on deals)", "High"],
        ["2", "User uploads own PDF\n(with warranty)", "LOW", "High\n(self-service)", "Low"],
        ["3", "Original rewrite of\ngame mechanics", "VERY LOW", "Medium\n(requires work)", "Medium"],
        ["4", "CC / Open License\ncontent", "ZERO", "Low\n(few titles)", "Low"],
        ["5", "Public domain\ngames", "ZERO", "Low\n(classic games)", "Low"],
    ]
    st = make_table(summary_data, col_widths=[avail_w * 0.05, avail_w * 0.25, avail_w * 0.15, avail_w * 0.25, avail_w * 0.15])
    st.setStyle(TableStyle([
        ("TEXTCOLOR", (2, 1), (2, 1), ACCENT_GREEN),
        ("FONTNAME", (2, 1), (2, 1), "Helvetica-Bold"),
        ("TEXTCOLOR", (2, 2), (2, 2), ACCENT_GREEN),
        ("FONTNAME", (2, 2), (2, 2), "Helvetica-Bold"),
        ("TEXTCOLOR", (2, 3), (2, 3), ACCENT_GREEN),
        ("FONTNAME", (2, 3), (2, 3), "Helvetica-Bold"),
        ("TEXTCOLOR", (2, 4), (2, 4), ACCENT_GREEN),
        ("FONTNAME", (2, 4), (2, 4), "Helvetica-Bold"),
        ("TEXTCOLOR", (2, 5), (2, 5), ACCENT_GREEN),
        ("FONTNAME", (2, 5), (2, 5), "Helvetica-Bold"),
    ]))
    story.append(st)
    story.append(Spacer(1, 4 * mm))

    # Discarded option
    disc_data = [
        ["Discarded Option", "Risk", "Why Discarded"],
        ["Platform scrapes publisher\nPDFs without consent", "HIGH\n(criminal in Italy)", "Art. 171(1)(a-ter) criminal penalties;\nThomson Reuters/Cohere precedent"],
        ["Use BGG-hosted PDFs", "HIGH\n(TOS violation)", "BGG explicitly prohibits\nAI/LLM usage in TOS"],
        ["EU Art. 4 TDM exception\n(no publisher opt-out)", "MEDIUM\n(uncertain)", "Must monitor each publisher;\ncriminal risk if opt-out missed"],
    ]
    disc_t = make_table(disc_data, col_widths=[avail_w * 0.30, avail_w * 0.18, avail_w * 0.52])
    disc_t.setStyle(TableStyle([
        ("TEXTCOLOR", (1, 1), (1, 1), ACCENT_RED),
        ("FONTNAME", (1, 1), (1, 1), "Helvetica-Bold"),
        ("TEXTCOLOR", (1, 2), (1, 2), ACCENT_RED),
        ("FONTNAME", (1, 2), (1, 2), "Helvetica-Bold"),
        ("TEXTCOLOR", (1, 3), (1, 3), ACCENT_AMBER),
        ("FONTNAME", (1, 3), (1, 3), "Helvetica-Bold"),
    ]))
    story.append(Paragraph("<b>Discarded options (too risky):</b>", styles["BodyText2"]))
    story.append(disc_t)
    story.append(Spacer(1, 4 * mm))

    story.append(Paragraph(
        "<b>Critical finding:</b> Italy's new AI law (Law 132/2025, effective Oct 2025) adds "
        "<b>criminal penalties</b> for TDM violations where opt-out rights have been exercised, "
        "making compliance essential for an Italy-based service.",
        styles["BodyText2"]))

    # ===== RISPOSTA DIRETTA: PDF da BGG =====
    story.append(Spacer(1, 4 * mm))
    bgg_data = [
        ["Question", "Answer"],
        ["Can we use PDFs found on BGG?", "NO - BGG TOS explicitly prohibits AI/LLM usage"],
        ["Can we scrape BGG for PDFs?", "NO - Automated access prohibited"],
        ["Can a user manually download\nfrom BGG and upload to MeepleAI?",
         "GRAY AREA - User action, but PDF was\naccessed under BGG TOS restrictions"],
    ]
    bgg_t = make_table(bgg_data, col_widths=[avail_w * 0.4, avail_w * 0.6])
    bgg_t.setStyle(TableStyle([
        ("TEXTCOLOR", (1, 1), (1, 1), ACCENT_RED),
        ("FONTNAME", (1, 1), (1, 1), "Helvetica-Bold"),
        ("TEXTCOLOR", (1, 2), (1, 2), ACCENT_RED),
        ("FONTNAME", (1, 2), (1, 2), "Helvetica-Bold"),
        ("TEXTCOLOR", (1, 3), (1, 3), ACCENT_AMBER),
        ("FONTNAME", (1, 3), (1, 3), "Helvetica-Bold"),
    ]))
    story.append(Paragraph("BoardGameGeek PDF Usability", styles["SectionH2"]))
    story.append(bgg_t)
    story.append(Spacer(1, 3 * mm))
    story.append(Paragraph(
        'BGG Terms of Service state: <i>"You agree not to use the Geek Websites to train or otherwise '
        'use as data for an AI (Artificial Intelligence) or Large Language Model (LLM) system."</i> '
        "This is an absolute blocker for any platform-side ingestion of BGG-hosted content. "
        "Even PDFs originally uploaded by publishers are governed by BGG's TOS when accessed through their platform.",
        styles["BodyText2"]))
    story.append(Paragraph(
        "If a user manually downloads a PDF from BGG and then uploads it to MeepleAI, the legal analysis shifts: "
        "the user is the one who accessed BGG (and may have violated BGG TOS), while MeepleAI processes "
        "user-uploaded content. However, a safer approach is to have users obtain PDFs directly from "
        "publisher websites, where the TOS restrictions are typically less explicit about AI usage.",
        styles["BodyText2"]))

    # ===== CRITICAL: PDF REWRITE PIPELINE ANALYSIS =====
    story.append(Paragraph("Can We Download PDFs, Rewrite, and Feed to AI?", styles["SectionH2"]))
    story.append(Paragraph(
        "A critical architectural question: can MeepleAI download publisher PDFs, use AI to automatically "
        "rewrite the content, and then use only the rewritten version? The answer depends on HOW the "
        "rewriting happens.",
        styles["BodyText2"]))

    rewrite_data = [
        ["Variant", "Process", "TDM?", "Risk"],
        ["A: Fully automated\n(AI reads PDF\nand rewrites)",
         "Download PDF -> LLM processes\ncopyrighted text -> Generates\nrewrite -> Store rewrite",
         "YES\n(Art. 70-septies\napplies)",
         "MEDIUM-HIGH\nEven if output is original,\ninput-side is regulated TDM"],
        ["B: Human reads\nand rewrites\n(Solution 3)",
         "Human plays game -> Reads\nrulebook -> Writes original\nexplanation from understanding",
         "NO\n(No AI processes\ncopyrighted text)",
         "VERY LOW\nSame as Wikipedia\ndescribing game rules"],
        ["C: Human + AI\nassistant\n(hybrid)",
         "Human reads + takes notes ->\nAI helps draft from human\nnotes -> Human reviews",
         "NO\n(AI reads human\nnotes, not PDF)",
         "LOW\nAI never sees the\ncopyrighted text directly"],
    ]
    rw_t = make_table(rewrite_data, col_widths=[avail_w * 0.18, avail_w * 0.32, avail_w * 0.18, avail_w * 0.32])
    rw_t.setStyle(TableStyle([
        ("TEXTCOLOR", (3, 1), (3, 1), ACCENT_RED),
        ("FONTNAME", (3, 1), (3, 1), "Helvetica-Bold"),
        ("TEXTCOLOR", (3, 2), (3, 2), ACCENT_GREEN),
        ("FONTNAME", (3, 2), (3, 2), "Helvetica-Bold"),
        ("TEXTCOLOR", (3, 3), (3, 3), ACCENT_GREEN),
        ("FONTNAME", (3, 3), (3, 3), "Helvetica-Bold"),
        ("TEXTCOLOR", (2, 1), (2, 1), ACCENT_RED),
        ("FONTNAME", (2, 1), (2, 1), "Helvetica-Bold"),
        ("TEXTCOLOR", (2, 2), (2, 2), ACCENT_GREEN),
        ("FONTNAME", (2, 2), (2, 2), "Helvetica-Bold"),
        ("TEXTCOLOR", (2, 3), (2, 3), ACCENT_GREEN),
        ("FONTNAME", (2, 3), (2, 3), "Helvetica-Bold"),
    ]))
    story.append(rw_t)
    story.append(Spacer(1, 3 * mm))

    story.append(Paragraph(
        "<b>Key distinction:</b> the Italian law (Art. 70-septies) regulates 'reproductions and extractions "
        "through AI models and systems.' Variant A triggers this because the AI directly processes the "
        "copyrighted PDF. Variants B and C do not, because the AI only processes human-authored notes. "
        "The legal boundary is: <b>does the AI ever read the copyrighted text?</b> If yes = TDM. If no = safe.",
        styles["BodyText2"]))

    story.append(Paragraph(
        '<b>NYGARD:</b> "Variant A is a trap. It looks efficient but creates the same legal exposure as '
        'direct RAG ingestion. The whole point of Solution 3 is that a HUMAN reads the rulebook and writes '
        'original content. If you automate that with AI reading the PDF, you lose the legal protection."',
        styles["ExpertQuote"]))

    # ===== DUAL-MODE ARCHITECTURE =====
    story.append(Paragraph("Dual-Mode Architecture: Viewer vs. AI Knowledge Base", styles["SectionH2"]))
    story.append(Paragraph(
        "The platform must maintain a strict separation between <b>PDF viewing</b> (display only) "
        "and <b>AI-powered Q&amp;A</b> (from the proprietary knowledge base). These are two completely "
        "different legal regimes.",
        styles["BodyText2"]))

    # PDF usage rules
    pdf_use_data = [
        ["PDF Usage Scenario", "Legal?", "Notes"],
        ["User views their own uploaded PDF", "YES", "Like any cloud storage service\n(Google Drive, Dropbox)"],
        ["Link to publisher's PDF on their site", "YES", "Hyperlink is not copying.\nExample: 'Rules at catan.com'"],
        ["MeepleAI downloads and re-distributes\npublisher PDF to all users", "NO", "Unauthorized distribution =\ncopyright infringement"],
        ["Embed publisher PDF in iframe", "GRAY", "Depends on jurisdiction.\nBetter to link directly."],
        ["User views PDF alongside AI answers\n(from proprietary KB)", "YES", "User sees their file +\nAI answers from separate KB"],
    ]
    pdf_t = make_table(pdf_use_data, col_widths=[avail_w * 0.38, avail_w * 0.10, avail_w * 0.52])
    pdf_t.setStyle(TableStyle([
        ("TEXTCOLOR", (1, 1), (1, 1), ACCENT_GREEN),
        ("FONTNAME", (1, 1), (1, 1), "Helvetica-Bold"),
        ("TEXTCOLOR", (1, 2), (1, 2), ACCENT_GREEN),
        ("FONTNAME", (1, 2), (1, 2), "Helvetica-Bold"),
        ("TEXTCOLOR", (1, 3), (1, 3), ACCENT_RED),
        ("FONTNAME", (1, 3), (1, 3), "Helvetica-Bold"),
        ("TEXTCOLOR", (1, 4), (1, 4), ACCENT_AMBER),
        ("FONTNAME", (1, 4), (1, 4), "Helvetica-Bold"),
        ("TEXTCOLOR", (1, 5), (1, 5), ACCENT_GREEN),
        ("FONTNAME", (1, 5), (1, 5), "Helvetica-Bold"),
    ]))
    story.append(pdf_t)
    story.append(Spacer(1, 4 * mm))

    # Architecture diagram (text-based)
    story.append(Paragraph("<b>Platform Architecture Overview:</b>", styles["BodyText2"]))
    arch_data = [
        ["PDF VIEWER (display only)", "AI KNOWLEDGE BASE (answers questions)"],
        ["User's own uploaded PDF\n(stored per-user, never shared)\n\n"
         "Link to publisher website\n(catan.com, fantasyflightgames.com)\n\n"
         "NO AI processing of PDFs\n"
         "NO storage in shared database\n"
         "NO redistribution",
         "Tier 1B: Original rewrites by humans\n(proprietary MeepleAI content)\n\n"
         "Tier 1A: User upload chunks\n(per-user isolated, with warranty)\n\n"
         "Tier 2: Publisher licensed content\n(with explicit consent)\n\n"
         "Tier 3: CC + Public Domain\n(freely licensed content)"],
    ]
    arch_t = Table(arch_data, colWidths=[avail_w * 0.48, avail_w * 0.48])
    arch_t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (0, 0), HexColor("#dbeafe")),
        ("BACKGROUND", (1, 0), (1, 0), HexColor("#dcfce7")),
        ("TEXTCOLOR", (0, 0), (0, 0), ACCENT_BLUE),
        ("TEXTCOLOR", (1, 0), (1, 0), ACCENT_GREEN),
        ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTSIZE", (0, 0), (-1, -1), 9),
        ("LEADING", (0, 0), (-1, -1), 12),
        ("FONTNAME", (0, 1), (-1, -1), "Helvetica"),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("GRID", (0, 0), (-1, -1), 0.5, HexColor("#e2e8f0")),
        ("TOPPADDING", (0, 0), (-1, -1), 6),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
        ("LEFTPADDING", (0, 0), (-1, -1), 8),
        ("RIGHTPADDING", (0, 0), (-1, -1), 8),
        ("BACKGROUND", (0, 1), (0, 1), HexColor("#eff6ff")),
        ("BACKGROUND", (1, 1), (1, 1), HexColor("#f0fdf4")),
    ]))
    story.append(arch_t)
    story.append(Spacer(1, 4 * mm))

    story.append(Paragraph(
        "<b>The separation is the legal firewall.</b> The PDF viewer is a passive display tool (like Google Drive). "
        "The AI knowledge base uses only legally sourced content. These two systems never cross: "
        "the AI never reads PDFs from the viewer, and the viewer never displays AI-generated content as if it were the original.",
        styles["BodyText2"]))

    # User workflow example
    story.append(Paragraph("Example User Workflow: Playing Catan", styles["SectionH3"]))
    flow_data = [
        ["Step", "Without Upload", "With Upload"],
        ["1. User asks:\n'How do I trade?'",
         "AI answers from proprietary\nrewrite (Tier 1B).\nLink: 'See official rules at\ncatan.com/game-rules'",
         "AI answers from proprietary\nrewrite + user's PDF chunks.\nUser can view their PDF\nin the viewer panel."],
        ["2. User asks:\n'What about the\nrobber?'",
         "AI answers from MeepleAI's\noriginal explanation.\nNo copyrighted text used.",
         "AI combines proprietary rewrite\n+ user's uploaded content\nfor more precise answer."],
        ["3. Publisher\npartnership\n(future)",
         "AI adds official FAQ,\nerrata, designer notes\nfrom Catan Studio.",
         "All three sources combined:\nrewrite + user PDF + official."],
    ]
    story.append(make_table(flow_data, col_widths=[avail_w * 0.18, avail_w * 0.41, avail_w * 0.41]))

    story.append(PageBreak())

    # ==================== 1. COPYRIGHT FUNDAMENTALS ====================
    story.append(Paragraph("1. Copyright Fundamentals: Game Rules vs. Rulebooks", styles["SectionH1"]))
    story.append(Paragraph("The Idea/Expression Dichotomy", styles["SectionH2"]))

    story.append(Paragraph(
        "<b>Game mechanics (ideas) are NOT copyrightable:</b>", styles["BodyText2"]))
    for item in [
        "The rules of Catan (place settlements, trade resources) cannot be owned via copyright",
        "US precedent: Baker v. Selden (1879) - methods and systems are not protectable",
        "Lotus v. Borland (1995) - functional elements not copyrightable",
    ]:
        story.append(Paragraph(f"\u2022 {item}", styles["BulletItem"]))

    story.append(Spacer(1, 3 * mm))
    story.append(Paragraph(
        "<b>Rulebook text (expression) IS copyrightable:</b>", styles["BodyText2"]))
    for item in [
        "The specific words, illustrations, layout, examples, flavor text are protected",
        "Creative choices in HOW rules are explained constitute original expression",
        "Art, diagrams, graphic design, narrative elements - all protected",
    ]:
        story.append(Paragraph(f"\u2022 {item}", styles["BulletItem"]))

    story.append(Spacer(1, 3 * mm))
    story.append(Paragraph(
        '<b>WIEGERS:</b> "This distinction is critical for requirements. The system must be designed around '
        'what it CAN legally use (game mechanics, factual information) vs. what carries copyright risk '
        '(specific textual expression)."',
        styles["ExpertQuote"]))

    story.append(Paragraph(
        "A RAG system that stores and retrieves <b>chunks of rulebook text</b> is reproducing copyrighted "
        "expression. This is fundamentally different from a system that merely 'knows' game rules.",
        styles["BodyText2"]))

    story.append(hr())

    # ==================== 2. LEGAL FRAMEWORK ====================
    story.append(Paragraph("2. Legal Framework Analysis", styles["SectionH1"]))

    # --- 2.1 US ---
    story.append(Paragraph("2.1 US Copyright Law - Fair Use (17 U.S.C. Section 107)", styles["SectionH2"]))

    fair_use_data = [
        ["Factor", "Assessment", "Risk"],
        ["1. Purpose & Character", "Commercial; arguably transformative\n(Q&A vs. reading rulebook)", "MEDIUM"],
        ["2. Nature of Work", "Factual/instructional (favors fair use)\nbut contains creative expression", "MEDIUM-LOW"],
        ["3. Amount Used", "RAG chunks = substantial portions", "HIGH"],
        ["4. Market Effect", "Could substitute for reading rulebook", "HIGH"],
    ]
    story.append(make_table(fair_use_data, col_widths=[avail_w * 0.25, avail_w * 0.50, avail_w * 0.25]))
    story.append(Spacer(1, 4 * mm))

    story.append(Paragraph("Key US Cases (2025-2026):", styles["SectionH3"]))
    cases = [
        ("<b>Thomson Reuters v. Ross Intelligence</b> (Feb 2025): Court REJECTED fair use for AI trained on "
         "Westlaw headnotes. First ruling against AI fair use defense. Directly relevant to storing rulebook chunks."),
        ("<b>Advanced Local Media v. Cohere</b> (Nov 2025): Court denied dismissal of RAG-specific copyright claims. "
         "'Substitutive summaries' may infringe. RAG technology specifically scrutinized."),
        ("<b>Bartz v. Anthropic</b> (June 2025): First ruling finding AI training IS fair use - but ONLY for "
         "legally acquired works. Pirated content NOT protected."),
        ("<b>US Copyright Office Part 3 Report</b> (May 2025): RAG 'is less likely to be transformative' than "
         "training. Both copying into DB and outputting are 'potential copyright infringements.'"),
        ("<b>Perplexity AI lawsuits</b>: WSJ and NY Post sued Perplexity (RAG-based search)."),
    ]
    for c in cases:
        story.append(Paragraph(f"\u2022 {c}", styles["BulletItem"]))

    story.append(Spacer(1, 3 * mm))
    story.append(Paragraph(
        '<b>NYGARD:</b> "The Thomson Reuters and Cohere rulings are red flags. Courts are NOT treating RAG as '
        'automatically fair use. The market substitution argument is strong."',
        styles["ExpertQuote"]))

    # --- 2.2 EU ---
    story.append(Paragraph("2.2 EU Copyright Directive 2019/790 - TDM Exceptions", styles["SectionH2"]))

    story.append(Paragraph(
        "<b>Article 3 - Research Exception:</b> TDM for research organizations only (scientific research). "
        "MeepleAI cannot use this - it's a commercial service.", styles["BodyText2"]))
    story.append(Paragraph(
        "<b>Article 4 - General TDM Exception:</b> TDM for ANY purpose (including commercial) "
        "UNLESS rights holders have 'expressly reserved their rights in an appropriate manner' via "
        "machine-readable means (robots.txt, meta tags, TDM headers).", styles["BodyText2"]))
    story.append(Paragraph(
        "<b>Hamburg Court (Dec 2025):</b> Non-machine-readable opt-outs are INVALID. "
        "<b>EU AI Act (Art. 53, effective Aug 2025):</b> AI providers must 'identify and respect' opt-out reservations.",
        styles["BodyText2"]))

    # --- 2.3 Italy ---
    story.append(Paragraph("2.3 Italian Copyright Law (Legge 633/1941 + Law 132/2025)", styles["SectionH2"]))
    story.append(Paragraph(
        "<b>CRITICAL for MeepleAI</b> (Italy-based service). Law 132/2025, effective October 10, 2025:",
        styles["BodyText2"]))

    it_data = [
        ["Article", "What It Does", "Impact"],
        ["70-ter", "TDM for research orgs\n(scientific only)", "Cannot use - commercial"],
        ["70-quater", "TDM for any purpose\nIF no opt-out", "Must check each publisher"],
        ["70-septies (NEW)", "Extends TDM rules to\nAI systems (incl. generative)", "Directly covers MeepleAI"],
        ["171(1)(a-ter) (NEW)", "CRIMINAL PENALTIES\nfor TDM violations via AI", "Non-negotiable compliance"],
    ]
    it_t = make_table(it_data, col_widths=[avail_w * 0.25, avail_w * 0.40, avail_w * 0.35])
    it_t.setStyle(TableStyle([
        ("TEXTCOLOR", (2, 4), (2, 4), ACCENT_RED),
        ("FONTNAME", (2, 4), (2, 4), "Helvetica-Bold"),
        ("FONTNAME", (1, 4), (1, 4), "Helvetica-Bold"),
        ("TEXTCOLOR", (1, 4), (1, 4), ACCENT_RED),
    ]))
    story.append(it_t)

    story.append(Spacer(1, 3 * mm))
    story.append(Paragraph(
        '<b>FOWLER:</b> "The Italian law is the most specific framework. Article 70-septies explicitly covers '
        'AI systems doing TDM. The criminal penalty provision elevates this from civil liability to criminal. '
        'Architecture decisions must treat this as a hard constraint."',
        styles["ExpertQuote"]))

    story.append(PageBreak())

    # ==================== 3. PUBLISHER LANDSCAPE ====================
    story.append(Paragraph("3. Publisher Landscape Analysis", styles["SectionH1"]))
    story.append(Paragraph("3.1 Publisher PDF Distribution", styles["SectionH2"]))

    pub_data = [
        ["Publisher", "Free PDF?", "Where?", "Notable Policy"],
        ["Catan Studio", "Yes", "catan.com", "Free download"],
        ["Fantasy Flight (Asmodee)", "Yes", "CDN-hosted", "IP bans 'software applications'"],
        ["Stonemaier Games", "Yes", "Website", "Anti-AI stance (Apr 2024)"],
        ["Czech Games Edition", "Yes", "BGG", "Uses AI validators internally"],
        ["Hasbro/WotC", "Selective", "Website", "D&D SRD 5.1 CC-BY-4.0"],
        ["Ravensburger", "Limited", "Some titles", "Forced AI art removal (2024)"],
        ["Games Workshop", "Limited", "Some titles", "Total AI ban (Jan 2026)"],
        ["Leder Games", "Yes", "Website", "Free downloads"],
        ["CMON", "Mixed", "BGG", "Varies by title"],
    ]
    story.append(make_table(pub_data, col_widths=[avail_w * 0.22, avail_w * 0.12, avail_w * 0.18, avail_w * 0.48]))

    story.append(Spacer(1, 4 * mm))
    story.append(Paragraph(
        "<b>Key insight:</b> 'Freely downloadable' does NOT mean 'freely usable in AI systems.' "
        "The license to download is for personal reading, not commercial TDM processing. "
        "No major board game publisher releases rulebooks under Creative Commons "
        "(exception: D&amp;D SRD 5.1 under CC-BY-4.0).",
        styles["BodyText2"]))

    story.append(Paragraph("3.2 Industry Anti-AI Stance (2024-2026)", styles["SectionH2"]))
    for item in [
        "<b>Stonemaier Games</b> (Apr 2024): 'does not, has not, and will not use any form of AI'",
        "<b>Ravensburger</b> (Mar 2024): Forced removal of AI art from licensed product",
        "<b>Asmodee</b>: No AI art policy across all productions",
        "<b>Games Workshop</b> (Jan 2026): Comprehensive AI ban across all design",
        "<b>Fantasy Flight Games</b>: IP policy bans their IP in 'software applications of any kind'",
    ]:
        story.append(Paragraph(f"\u2022 {item}", styles["BulletItem"]))

    story.append(Spacer(1, 3 * mm))
    story.append(Paragraph(
        "NOTE: These policies primarily target AI-generated art. No publisher has explicitly addressed "
        "third-party AI text mining of rulebooks for RAG/chatbot purposes.",
        styles["BodyText2"]))

    story.append(Paragraph("3.3 Existing Competitors", styles["SectionH2"]))
    comp_data = [
        ["Product", "Approach", "Copyright Strategy"],
        ["RulesBot.ai", "Pre-indexed rulebooks", "Unclear licensing"],
        ["Ludomentor (Awaken Realms)", "Publisher's own games", "First-party (safe)"],
        ["Board Game Assistant", "Publisher partnerships", "Licensed content"],
        ["Boardside", "Official sources only", "Claims official only"],
    ]
    story.append(make_table(comp_data, col_widths=[avail_w * 0.30, avail_w * 0.30, avail_w * 0.40]))

    story.append(PageBreak())

    # ==================== 4. THE 5 LEGAL SOLUTIONS ====================
    story.append(Paragraph("4. The 5 Legal Solutions", styles["SectionH1"]))

    # --- Solution 1: Publisher Licensing ---
    story.append(Paragraph("Solution 1: Publisher Licensing (ZERO RISK)", styles["SectionH2"]))
    story.append(Paragraph(
        "Formal agreements with publishers granting MeepleAI the right to ingest and serve "
        "their rulebook content. The safest possible approach.",
        styles["BodyText2"]))
    s1_data = [
        ["Aspect", "Details"],
        ["Legal basis", "Contractual license - explicit consent eliminates all copyright risk"],
        ["Model", "Revenue share, value exchange (analytics on rule confusion), or free partnership"],
        ["Precedent", "Board Game Assistant uses publisher partnerships;\nLudomentor (Awaken Realms) uses first-party content"],
        ["Scalability", "Limited by business development speed;\nhigh value for top 50-100 games"],
        ["Content scope", "Full rulebook text, FAQ, errata - everything the publisher provides"],
    ]
    story.append(make_table(s1_data, col_widths=[avail_w * 0.22, avail_w * 0.78]))

    story.append(Spacer(1, 5 * mm))

    # --- Solution 2: User Upload ---
    story.append(Paragraph("Solution 2: User Uploads Own PDF (LOW RISK)", styles["SectionH2"]))
    story.append(Paragraph(
        "Users who own a board game upload their copy of the rulebook PDF. The system processes it "
        "and provides answers only to that specific user. Content is never shared across users.",
        styles["BodyText2"]))
    s2_data = [
        ["Dimension", "Analysis"],
        ["US Fair Use", "Strong. Personal use, user purchased game, not redistributing."],
        ["EU/Italian TDM", "User has lawful access (purchased game). Processing for personal use."],
        ["Platform Liability", "DMCA Section 512(c) safe harbor applies (user-directed storage)."],
        ["Market Effect", "Minimal. User already bought the game. RAG enhances their experience."],
        ["Analogy", "Google NotebookLM, Notion AI, ChatGPT file upload - widely accepted."],
        ["Thin Copyright", "Rulebooks are factual/instructional. Weaker protection than creative works."],
    ]
    story.append(make_table(s2_data, col_widths=[avail_w * 0.22, avail_w * 0.78]))
    story.append(Spacer(1, 3 * mm))
    story.append(Paragraph(
        '<b>WIEGERS:</b> "Scenario B has well-established analogies: personal document AI assistants. '
        'Key requirements: (1) user uploads, (2) per-user isolation, (3) no cross-user sharing, '
        '(4) user warranty of rights, (5) DMCA compliance."',
        styles["ExpertQuote"]))

    story.append(Spacer(1, 5 * mm))

    # --- Solution 3: Original Rewrite (THE KEY INSIGHT) ---
    story.append(Paragraph("Solution 3: Original Rewrite of Game Mechanics (VERY LOW RISK)", styles["SectionH2"]))
    story.append(Paragraph(
        "This is the <b>key strategic insight</b> of this analysis. Game mechanics are NOT copyrightable - "
        "only the specific textual expression is. MeepleAI can legally create its own original descriptions "
        "of how games work, using completely different words from the publisher's rulebook.",
        styles["BodyText2"]))

    # Example box
    example_data = [
        ["Original Rulebook (copyrighted)", "MeepleAI Rewrite (legal)"],
        ["During the Resource Production phase,\neach player receives one resource card\n"
         "for each settlement adjacent to the\nterrain hex matching the number rolled.\n"
         "Cities produce two resources.",
         "Quando si tirano i dadi, ogni giocatore\nprende risorse in base ai propri\n"
         "insediamenti vicini al terreno\ncorrispondente. Un insediamento da' 1\n"
         "risorsa, una citta' ne da' 2."],
    ]
    ex_t = make_table(example_data, col_widths=[avail_w * 0.50, avail_w * 0.50])
    ex_t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (0, 0), HexColor("#fef2f2")),
        ("BACKGROUND", (1, 0), (1, 0), HexColor("#f0fdf4")),
        ("TEXTCOLOR", (0, 0), (0, 0), ACCENT_RED),
        ("TEXTCOLOR", (1, 0), (1, 0), ACCENT_GREEN),
        ("BACKGROUND", (0, 1), (0, 1), HexColor("#fef2f2")),
        ("BACKGROUND", (1, 1), (1, 1), HexColor("#f0fdf4")),
    ]))
    story.append(ex_t)
    story.append(Spacer(1, 3 * mm))

    story.append(Paragraph(
        "<b>Same mechanics, original expression = no copyright violation.</b>",
        styles["BodyText2"]))

    s3_data = [
        ["Aspect", "Details"],
        ["Legal basis", "Baker v. Selden (1879): methods/systems not copyrightable.\n"
         "Copyright protects expression, not ideas. Game mechanics are ideas."],
        ["Implementation", "Team (or AI + human review) writes original rule explanations.\n"
         "Describe mechanics without copying publisher's text."],
        ["Scalability", "Medium - requires editorial work per game.\nCould prioritize top 50-100 popular games."],
        ["Advantage", "Creates PROPRIETARY knowledge base owned by MeepleAI.\n"
         "No dependency on publisher consent or user uploads."],
        ["Precedent", "Wikipedia describes game rules without copyright issues.\n"
         "BGG wiki pages summarize rules in original text."],
        ["Risk", "Very low. Only risk: if rewrite is 'substantially similar'\n"
         "to original expression (easily avoidable with review process)."],
    ]
    story.append(make_table(s3_data, col_widths=[avail_w * 0.22, avail_w * 0.78]))

    story.append(Spacer(1, 3 * mm))
    story.append(Paragraph(
        '<b>FOWLER:</b> "This is the game-changer. Solution 3 creates a proprietary asset that MeepleAI owns. '
        'Combined with user uploads for the long tail and publisher deals for premium content, '
        'this gives you three independent content sources with zero legal dependency on any single one."',
        styles["ExpertQuote"]))

    story.append(PageBreak())

    # --- Solution 4: CC/Open License ---
    story.append(Paragraph("Solution 4: Creative Commons / Open License Content (ZERO RISK)", styles["SectionH2"]))
    story.append(Paragraph(
        "Some game content is released under open licenses that explicitly permit commercial reuse.",
        styles["BodyText2"]))
    s4_data = [
        ["Content", "License", "Scope"],
        ["D&D SRD 5.1", "CC-BY-4.0", "Core D&D rules, monsters, spells - freely usable with attribution"],
        ["ORC License games", "ORC License", "Pathfinder ecosystem, various RPG publishers"],
        ["CC-licensed indie games", "Various CC", "Small but growing catalog of indie board/card games"],
    ]
    story.append(make_table(s4_data, col_widths=[avail_w * 0.30, avail_w * 0.20, avail_w * 0.50]))
    story.append(Spacer(1, 3 * mm))
    story.append(Paragraph(
        "Limited catalog but completely safe. Good for bootstrapping the platform and demonstrating "
        "value to potential publisher partners.",
        styles["BodyText2"]))

    # --- Solution 5: Public Domain ---
    story.append(Paragraph("Solution 5: Public Domain Games (ZERO RISK)", styles["SectionH2"]))
    story.append(Paragraph(
        "Games whose copyright has expired. EU: 70 years after author's death. US: pre-1929 automatic.",
        styles["BodyText2"]))
    for item in [
        "Chess, Go, Backgammon, Checkers, Mancala and all classic abstract games",
        "Traditional card games (Poker, Bridge, Rummy, etc.)",
        "Classic party games with expired copyright",
        "Historical wargames and simulations with expired copyright",
    ]:
        story.append(Paragraph(f"\u2022 {item}", styles["BulletItem"]))
    story.append(Spacer(1, 3 * mm))
    story.append(Paragraph(
        "Small but useful catalog. Good for onboarding new users and showing the platform's capabilities.",
        styles["BodyText2"]))

    story.append(PageBreak())

    # ==================== 5. EXPERT SYNTHESIS ====================
    story.append(Paragraph("5. Expert Panel Synthesis", styles["SectionH1"]))

    story.append(Paragraph("5.1 Convergent Insights (All Experts Agree)", styles["SectionH2"]))
    for i, item in enumerate([
        "5 legal solutions exist - NOT just 2 (licensing + user upload)",
        "Solution 3 (original rewrite) is the strategic differentiator - creates proprietary IP",
        "BGG is off-limits for any automated processing",
        "Italian criminal penalties (Art. 171(1)(a-ter)) make compliance non-negotiable",
        "The legal landscape is actively hostile to unlicensed commercial RAG (2025-2026 trend)",
        "A hybrid architecture combining multiple solutions is stronger than any single approach",
    ], 1):
        story.append(Paragraph(f"<b>{i}.</b> {item}", styles["BulletItem"]))

    story.append(Paragraph("5.2 The Hybrid Strategy", styles["SectionH2"]))
    story.append(Paragraph(
        '<b>FOWLER:</b> "The hybrid model is the winning strategy: original rewrites for the top 50 games '
        '(proprietary KB), publisher partnerships for premium content with errata/FAQ, '
        'user uploads for the long tail of 50,000+ games. Three independent content sources '
        'with zero legal dependency on any single one."',
        styles["ExpertQuote"]))
    story.append(Paragraph(
        '<b>NYGARD:</b> "Solution 3 eliminates the single point of failure. If a publisher partnership '
        'falls through, or if a court rules against user-upload models, you still have your proprietary '
        'rewrites. That operational resilience is worth the editorial investment."',
        styles["ExpertQuote"]))
    story.append(Paragraph(
        '<b>CRISPIN:</b> "The original rewrite approach also improves quality. Publisher rulebooks are '
        'often poorly written. MeepleAI could become known for BETTER rule explanations than the originals - '
        'that is a competitive advantage, not just a legal workaround."',
        styles["ExpertQuote"]))

    story.append(Paragraph("5.3 Key Strategic Insight: Mechanics Are Free", styles["SectionH2"]))
    story.append(Paragraph(
        "The fundamental legal principle that unlocks MeepleAI's strategy: <b>game mechanics are ideas, "
        "and ideas cannot be copyrighted</b>. Only the specific words used to describe them are protected. "
        "This means MeepleAI can build a comprehensive knowledge base of how every game works, "
        "written in its own original language, without any copyright concern. "
        "Combined with user uploads for verbatim reference and publisher deals for premium content, "
        "this creates a defensible, scalable, legally sound content strategy.",
        styles["BodyText2"]))

    story.append(hr())

    # ==================== 6. RECOMMENDED ARCHITECTURE ====================
    story.append(Paragraph("6. Recommended Architecture", styles["SectionH1"]))

    tiers = [
        ("Tier 1A: User-Uploaded Content (Launch MVP)",
         "User uploads PDF -> Per-user processing -> Per-user vector store -> Per-user Q&A",
         [
             "User TOS: warranty of ownership/rights, indemnification clause",
             "Per-user content isolation (no cross-pollination between users)",
             "DMCA takedown procedure and designated agent",
             "Content retention policy (auto-delete after N days of inactivity)",
             "No caching of full text - store only embeddings + minimal chunks",
             "User can delete their content at any time",
         ]),
        ("Tier 1B: Original Mechanics Rewrite (Launch MVP - Top 50 Games)",
         "Editorial team writes original rule descriptions -> Shared knowledge base -> All users",
         [
             "Proprietary content owned by MeepleAI (no copyright dependency)",
             "Editorial process: play game -> write rules in original language -> peer review",
             "AI-assisted drafting with human review to ensure originality",
             "Similarity check against publisher text (must be substantially different)",
             "Priority: BGG Top 50 games + trending new releases",
             "Multi-language support (IT, EN, DE, FR, ES) for each game",
             "Community contribution model: users can suggest corrections/improvements",
         ]),
        ("Tier 2: Licensed Publisher Content (Phase 2 - 3-6 months)",
         "Publisher API/agreement -> Shared knowledge base -> All users",
         [
             "Formal licensing agreements with publishers",
             "Publisher content management dashboard",
             "Revenue sharing or value exchange model",
             "Attribution and source citation in responses",
             "Publisher analytics (most asked questions, rule confusion areas)",
             "Value proposition: MeepleAI improves game accessibility -> more sales",
         ]),
        ("Tier 3: Open + Public Domain Content (Parallel Track)",
         "CC-licensed / public domain games -> Community knowledge base -> All users",
         [
             "D&D SRD 5.1 (CC-BY-4.0), ORC License content, CC-licensed indie games",
             "Public domain: Chess, Go, Backgammon, classic card games",
             "Community contribution model with license verification",
             "Good for onboarding and demonstrating platform value",
         ]),
    ]

    for title, flow, reqs in tiers:
        story.append(Paragraph(title, styles["SectionH2"]))
        story.append(Paragraph(f"<font color='#6b7280'><i>{flow}</i></font>", styles["BodyText2"]))
        for r in reqs:
            story.append(Paragraph(f"\u2022 {r}", styles["BulletItem"]))
        story.append(Spacer(1, 3 * mm))

    story.append(PageBreak())

    # ==================== 7. TOS & SAFEGUARDS ====================
    story.append(Paragraph("7. TOS and Legal Safeguards", styles["SectionH1"]))

    story.append(Paragraph("Required TOS Clauses", styles["SectionH2"]))
    tos_items = [
        ("<b>User Warranty:</b> 'You represent and warrant that you have all necessary rights, licenses, "
         "and permissions to upload content to MeepleAI.'"),
        ("<b>Indemnification:</b> 'You agree to indemnify and hold harmless MeepleAI from any claims "
         "arising from your uploaded content.'"),
        ("<b>DMCA Compliance:</b> 'MeepleAI respects IP rights. Contact our designated DMCA agent for "
         "copyright concerns.'"),
        ("<b>No Redistribution:</b> 'Content you upload is processed solely for your personal use.'"),
        ("<b>Data Retention:</b> 'Uploaded content is retained for [N] days and automatically deleted.'"),
        ("<b>AI Disclaimer:</b> 'Responses may contain errors. Always refer to the official rulebook.'"),
    ]
    for i, item in enumerate(tos_items, 1):
        story.append(Paragraph(f"{i}. {item}", styles["BulletItem"]))

    story.append(Paragraph("Technical Safeguards", styles["SectionH2"]))
    tech_items = [
        "Per-user vector namespaces in Qdrant - strict tenant isolation",
        "No full-text storage - only embeddings + minimal context chunks",
        "Chunk size limits (200-500 tokens) - non-substitutive",
        "Response attribution: 'Based on your uploaded rulebook, page X'",
        "Rate limiting on uploads - prevent bulk processing",
        "File type validation - accept only PDF",
    ]
    for item in tech_items:
        story.append(Paragraph(f"\u2022 {item}", styles["BulletItem"]))

    story.append(hr())

    # ==================== 8. RISK MATRIX ====================
    story.append(Paragraph("8. Risk Matrix", styles["SectionH1"]))

    risk_data = [
        ["Risk", "Prob.", "Impact", "Mitigation"],
        ["Publisher DMCA takedown\n(user uploads)", "Low", "Medium", "DMCA compliance,\nuser warranty"],
        ["Criminal liability Italy\n(Art. 171)", "Very Low", "Very High", "No platform scraping;\nonly 5 legal solutions"],
        ["User uploads pirated\ncontent", "Medium", "Low-Med", "TOS warranty,\nDMCA process"],
        ["Original rewrite deemed\n'substantially similar'", "Very Low", "Medium", "Editorial review process;\nsimilarity checking"],
        ["Competitor with publisher\ndeals outpaces us", "High", "Medium", "Pursue partnerships;\noriginal rewrites as moat"],
        ["US court ruling against\nRAG fair use", "Med-High", "High", "Hybrid model: rewrites\n+ user upload + licensing"],
        ["Publisher opposes original\nrewrite of mechanics", "Low", "Low", "Mechanics are not copyrightable;\nBaker v. Selden precedent"],
    ]
    story.append(make_table(risk_data, col_widths=[avail_w * 0.28, avail_w * 0.12, avail_w * 0.14, avail_w * 0.46]))

    story.append(PageBreak())

    # ==================== 9. ACTION ITEMS ====================
    story.append(Paragraph("9. Action Items", styles["SectionH1"]))

    story.append(Paragraph("Immediate (Pre-Launch)", styles["SectionH2"]))
    for item in [
        "<b>Legal:</b> Engage Italian IP attorney for compliance with Law 132/2025",
        "<b>Architecture:</b> Implement per-user content isolation in Qdrant",
        "<b>TOS:</b> Draft user warranty, indemnification, DMCA procedures",
        "<b>Technical:</b> Minimize stored text (embeddings-first, minimal chunks)",
        "<b>Editorial:</b> Begin original rewrite of top 10 game mechanics (Catan, Ticket to Ride, "
        "Carcassonne, Pandemic, Azul, 7 Wonders, Wingspan, Codenames, Splendor, Dixit)",
        "<b>Content:</b> Ingest D&D SRD 5.1 (CC-BY-4.0) + public domain games as launch KB",
    ]:
        story.append(Paragraph(f"\u2022 {item}", styles["BulletItem"]))

    story.append(Paragraph("Short-Term (1-3 months post-launch)", styles["SectionH2"]))
    for item in [
        "<b>Editorial:</b> Expand original rewrites to top 50 games (BGG ranking)",
        "<b>Business:</b> Approach 3-5 friendly publishers for pilot partnerships",
        "<b>Community:</b> Launch community contribution model for rewrite suggestions",
        "<b>Compliance:</b> Register DMCA designated agent with US Copyright Office",
        "<b>QA:</b> Implement automated similarity checker (original vs publisher text)",
    ]:
        story.append(Paragraph(f"\u2022 {item}", styles["BulletItem"]))

    story.append(Paragraph("Medium-Term (3-12 months)", styles["SectionH2"]))
    for item in [
        "<b>Business model:</b> Develop publisher value proposition (rule confusion analytics, FAQ)",
        "<b>Scale licensing:</b> Expand publisher partnerships based on pilot results",
        "<b>Scale rewrites:</b> Top 200 games with community + AI-assisted editorial pipeline",
        "<b>Multi-language:</b> Translate original rewrites to IT, EN, DE, FR, ES",
        "<b>Legal audit:</b> Quarterly review of TDM opt-out landscape and case law",
    ]:
        story.append(Paragraph(f"\u2022 {item}", styles["BulletItem"]))

    story.append(hr())

    # ==================== 10. SOURCES ====================
    story.append(Paragraph("10. Sources", styles["SectionH1"]))

    story.append(Paragraph("Legal Cases", styles["SectionH3"]))
    for s in [
        "Thomson Reuters v. Ross Intelligence (D. Del., Feb 2025)",
        "Advanced Local Media v. Cohere (S.D.N.Y., Nov 2025)",
        "NYT v. OpenAI (S.D.N.Y., ongoing)",
        "Bartz v. Anthropic (June 2025)",
        "Hamburg Court TDM Opt-Out Ruling (Dec 2025)",
        "US Copyright Office Part 3 AI Report (May 2025)",
    ]:
        story.append(Paragraph(f"\u2022 {s}", styles["BulletItem"]))

    story.append(Paragraph("EU/Italian Law", styles["SectionH3"]))
    for s in [
        "EU Copyright Directive 2019/790, Articles 3 and 4",
        "EU AI Act, Article 53 (effective Aug 2025)",
        "Italy Law 132/2025 (AI Law, effective Oct 2025)",
        "Legge 633/1941 (Italian Copyright Law), Articles 70-ter, 70-quater, 70-septies",
    ]:
        story.append(Paragraph(f"\u2022 {s}", styles["BulletItem"]))

    story.append(Paragraph("Industry & Platform Policies", styles["SectionH3"]))
    for s in [
        "BoardGameGeek Terms of Service",
        "Fantasy Flight Games IP Policy",
        "Stonemaier Games AI Policy (Apr 2024)",
        "Games Workshop AI Ban (Jan 2026)",
        "DMCA Section 512 Safe Harbors (17 U.S.C.)",
    ]:
        story.append(Paragraph(f"\u2022 {s}", styles["BulletItem"]))

    story.append(Spacer(1, 15 * mm))
    story.append(Paragraph(
        "Generated by MeepleAI Research Team | 2026-03-07 | Expert Spec Panel with Deep Research",
        styles["Footer"]))

    # ==================== BUILD ====================
    doc = SimpleDocTemplate(
        OUTPUT_PATH,
        pagesize=A4,
        leftMargin=MARGIN, rightMargin=MARGIN,
        topMargin=MARGIN, bottomMargin=MARGIN,
        title="Board Game PDF Copyright Analysis - MeepleAI",
        author="MeepleAI Research Team",
        subject="Copyright analysis for RAG system using board game rulebook PDFs",
    )
    doc.build(story)
    print(f"PDF generated: {OUTPUT_PATH}")
    return OUTPUT_PATH


if __name__ == "__main__":
    build_pdf()
