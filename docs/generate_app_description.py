#!/usr/bin/env python3
"""
MeepleAI Application Description PDF Generator
Generates a professional PDF document describing the MeepleAI application.
"""

from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import inch, cm
from reportlab.lib.enums import TA_CENTER, TA_LEFT, TA_JUSTIFY
from reportlab.lib import colors
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, PageBreak, Table, TableStyle,
    KeepTogether, ListFlowable, ListItem
)
from datetime import datetime

def create_meepleai_pdf():
    """Generate the MeepleAI application description PDF."""

    # Document setup
    filename = "MeepleAI_Application_Description.pdf"
    doc = SimpleDocTemplate(
        filename,
        pagesize=A4,
        rightMargin=2*cm,
        leftMargin=2*cm,
        topMargin=2*cm,
        bottomMargin=2*cm
    )

    # Get styles
    styles = getSampleStyleSheet()

    # Custom styles
    title_style = ParagraphStyle(
        'CustomTitle',
        parent=styles['Heading1'],
        fontSize=28,
        textColor=colors.HexColor('#2563EB'),
        spaceAfter=30,
        alignment=TA_CENTER,
        fontName='Helvetica-Bold'
    )

    subtitle_style = ParagraphStyle(
        'Subtitle',
        parent=styles['Normal'],
        fontSize=14,
        textColor=colors.HexColor('#64748B'),
        spaceAfter=12,
        alignment=TA_CENTER,
        fontName='Helvetica'
    )

    heading1_style = ParagraphStyle(
        'CustomHeading1',
        parent=styles['Heading1'],
        fontSize=18,
        textColor=colors.HexColor('#1E40AF'),
        spaceAfter=12,
        spaceBefore=16,
        fontName='Helvetica-Bold'
    )

    heading2_style = ParagraphStyle(
        'CustomHeading2',
        parent=styles['Heading2'],
        fontSize=14,
        textColor=colors.HexColor('#3B82F6'),
        spaceAfter=10,
        spaceBefore=12,
        fontName='Helvetica-Bold'
    )

    body_style = ParagraphStyle(
        'CustomBody',
        parent=styles['Normal'],
        fontSize=11,
        alignment=TA_JUSTIFY,
        spaceAfter=10,
        leading=14
    )

    code_style = ParagraphStyle(
        'Code',
        parent=styles['Code'],
        fontSize=9,
        fontName='Courier',
        textColor=colors.HexColor('#374151'),
        backColor=colors.HexColor('#F3F4F6'),
        leftIndent=10,
        rightIndent=10,
        spaceAfter=10
    )

    # Story (document content)
    story = []

    # --- COVER PAGE ---
    story.append(Spacer(1, 3*cm))
    story.append(Paragraph("MeepleAI", title_style))
    story.append(Spacer(1, 0.5*cm))
    story.append(Paragraph("AI-Powered Board Game Rules Assistant", subtitle_style))
    story.append(Spacer(1, 0.3*cm))
    story.append(Paragraph("Application Documentation", subtitle_style))
    story.append(Spacer(1, 2*cm))

    # Version info table
    version_data = [
        ["Version:", "1.0"],
        ["Date:", datetime.now().strftime("%B %d, %Y")],
        ["Stack:", "ASP.NET Core 9.0 + Next.js 14"],
        ["Database:", "PostgreSQL + Qdrant + Redis"]
    ]
    version_table = Table(version_data, colWidths=[4*cm, 8*cm])
    version_table.setStyle(TableStyle([
        ('ALIGN', (0, 0), (-1, -1), 'LEFT'),
        ('FONTNAME', (0, 0), (0, -1), 'Helvetica-Bold'),
        ('FONTNAME', (1, 0), (1, -1), 'Helvetica'),
        ('FONTSIZE', (0, 0), (-1, -1), 11),
        ('TEXTCOLOR', (0, 0), (-1, -1), colors.HexColor('#374151')),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 8),
    ]))
    story.append(version_table)

    story.append(PageBreak())

    # --- TABLE OF CONTENTS ---
    story.append(Paragraph("Table of Contents", heading1_style))
    story.append(Spacer(1, 0.5*cm))

    toc_items = [
        "1. Overview",
        "2. Technology Stack",
        "3. Architecture",
        "4. Key Features",
        "5. Services & Components",
        "6. Authentication & Security",
        "7. MCP Servers",
        "8. Observability",
        "9. Testing & Quality",
        "10. Deployment"
    ]

    for item in toc_items:
        story.append(Paragraph(item, body_style))

    story.append(PageBreak())

    # --- 1. OVERVIEW ---
    story.append(Paragraph("1. Overview", heading1_style))
    story.append(Paragraph(
        "MeepleAI is an AI-powered assistant designed to help board game players understand "
        "complex rulebooks through intelligent question answering. The system processes PDF "
        "rulebooks, extracts and chunks text, creates vector embeddings, and uses Retrieval "
        "Augmented Generation (RAG) to provide accurate answers to player questions.",
        body_style
    ))

    story.append(Paragraph("Key Capabilities:", heading2_style))
    capabilities = [
        "PDF rulebook upload and processing with validation",
        "Semantic search using vector embeddings (Qdrant)",
        "Intelligent Q&A powered by LLM (OpenRouter API)",
        "Streaming responses with Server-Sent Events (SSE)",
        "Response caching for improved performance (Redis)",
        "Setup guide generation using RAG",
        "n8n workflow integration for automation",
        "Comprehensive observability (Seq, Jaeger, Prometheus, Grafana)"
    ]

    bullet_list = ListFlowable(
        [ListItem(Paragraph(cap, body_style), leftIndent=20) for cap in capabilities],
        bulletType='bullet',
        start='•'
    )
    story.append(bullet_list)
    story.append(Spacer(1, 0.5*cm))

    # --- 2. TECHNOLOGY STACK ---
    story.append(PageBreak())
    story.append(Paragraph("2. Technology Stack", heading1_style))

    stack_data = [
        ["Component", "Technology", "Version"],
        ["Backend", "ASP.NET Core", "9.0"],
        ["Frontend", "Next.js", "14"],
        ["Database", "PostgreSQL", "Latest"],
        ["Vector DB", "Qdrant", "Latest"],
        ["Cache", "Redis", "Latest"],
        ["AI/LLM", "OpenRouter API", "-"],
        ["Workflow", "n8n", "Latest"],
        ["Container", "Docker", "24.0+"],
        ["Package Manager", "pnpm", "9"],
    ]

    stack_table = Table(stack_data, colWidths=[4*cm, 5*cm, 3*cm])
    stack_table.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), colors.HexColor('#3B82F6')),
        ('TEXTCOLOR', (0, 0), (-1, 0), colors.whitesmoke),
        ('ALIGN', (0, 0), (-1, -1), 'LEFT'),
        ('FONTNAME', (0, 0), (-1, 0), 'Helvetica-Bold'),
        ('FONTSIZE', (0, 0), (-1, 0), 11),
        ('BOTTOMPADDING', (0, 0), (-1, 0), 12),
        ('BACKGROUND', (0, 1), (-1, -1), colors.beige),
        ('GRID', (0, 0), (-1, -1), 1, colors.grey),
        ('FONTSIZE', (0, 1), (-1, -1), 10),
    ]))
    story.append(stack_table)
    story.append(Spacer(1, 0.5*cm))

    # --- 3. ARCHITECTURE ---
    story.append(PageBreak())
    story.append(Paragraph("3. Architecture", heading1_style))

    story.append(Paragraph("Monorepo Structure:", heading2_style))
    story.append(Paragraph(
        "<font name='Courier' size=9>"
        "apps/api/          - Backend (ASP.NET Core)<br/>"
        "  src/Api/         - Application code<br/>"
        "    Services/      - Business logic<br/>"
        "    Infrastructure/ - DB context &amp; entities<br/>"
        "    Models/        - DTOs<br/>"
        "  tests/Api.Tests/ - xUnit + Testcontainers<br/>"
        "apps/web/          - Frontend (Next.js)<br/>"
        "  src/pages/       - Routes<br/>"
        "  src/lib/         - Utilities &amp; API client<br/>"
        "infra/             - Docker Compose<br/>"
        "schemas/           - JSON schemas<br/>"
        "docs/              - Documentation<br/>"
        "mcp/               - MCP servers"
        "</font>",
        body_style
    ))
    story.append(Spacer(1, 0.5*cm))

    # --- 4. KEY FEATURES ---
    story.append(PageBreak())
    story.append(Paragraph("4. Key Features", heading1_style))

    features = [
        ("PDF Processing",
         "Upload and validate PDF rulebooks with comprehensive validation (file size, MIME type, "
         "magic bytes, page count). Extract text using Docnet.Core and tables using iText7."),

        ("RAG Pipeline",
         "Text chunking (512 chars, 50 overlap), embedding generation via OpenRouter, "
         "vector indexing in Qdrant, semantic search with configurable top-K."),

        ("Streaming Responses",
         "Server-Sent Events (SSE) for token-by-token streaming, progressive UI updates, "
         "stop button for cancellation, state indicators."),

        ("Response Caching",
         "Redis-based caching with simulated streaming for cached responses, configurable TTL, "
         "automatic cache invalidation."),

        ("Setup Guide Generation",
         "AI-powered game setup wizard using RAG, step-by-step instructions with citations, "
         "optional steps detection, progress tracking."),

        ("Authentication",
         "Dual auth system: session cookies + API keys, PBKDF2 hashing (210,000 iterations), "
         "API key scopes and expiration, session auto-revocation."),

        ("n8n Integration",
         "Webhook-based workflows for Q&amp;A and Explain agents, external orchestration, "
         "async processing."),
    ]

    for title, desc in features:
        story.append(Paragraph(f"<b>{title}</b>", heading2_style))
        story.append(Paragraph(desc, body_style))
        story.append(Spacer(1, 0.3*cm))

    # --- 5. SERVICES & COMPONENTS ---
    story.append(PageBreak())
    story.append(Paragraph("5. Services & Components", heading1_style))

    story.append(Paragraph("AI/RAG Services:", heading2_style))
    ai_services = [
        "EmbeddingService - Vector embedding generation",
        "QdrantService - Vector database operations",
        "TextChunkingService - Document chunking",
        "RagService - Semantic search and retrieval",
        "LlmService - LLM completions (OpenRouter/Ollama)",
        "StreamingQaService - Streaming Q&A responses"
    ]
    story.append(ListFlowable(
        [ListItem(Paragraph(s, body_style), leftIndent=20) for s in ai_services],
        bulletType='bullet'
    ))

    story.append(Paragraph("PDF Services:", heading2_style))
    pdf_services = [
        "PdfStorageService - File storage management",
        "PdfTextExtractionService - Text extraction (Docnet.Core)",
        "PdfTableExtractionService - Table extraction (iText7)",
        "PdfValidationService - Pre-upload validation"
    ]
    story.append(ListFlowable(
        [ListItem(Paragraph(s, body_style), leftIndent=20) for s in pdf_services],
        bulletType='bullet'
    ))

    story.append(Paragraph("Domain Services:", heading2_style))
    domain_services = [
        "GameService - Game management",
        "RuleSpecService - RuleSpec v0 operations",
        "SetupGuideService - Setup wizard generation",
        "RuleSpecDiffService - Version comparison"
    ]
    story.append(ListFlowable(
        [ListItem(Paragraph(s, body_style), leftIndent=20) for s in domain_services],
        bulletType='bullet'
    ))

    # --- 6. AUTHENTICATION & SECURITY ---
    story.append(PageBreak())
    story.append(Paragraph("6. Authentication & Security", heading1_style))

    story.append(Paragraph("Authentication Methods:", heading2_style))
    story.append(Paragraph(
        "MeepleAI supports dual authentication: session cookies for web users and "
        "API keys for programmatic access. API keys use PBKDF2 hashing with 210,000 "
        "iterations (SHA256), consistent with password security. Keys support scopes, "
        "expiration, and environment tagging (live/test).",
        body_style
    ))

    story.append(Paragraph("Security Features:", heading2_style))
    security = [
        "PBKDF2 password hashing (210,000 iterations)",
        "API key secure hash storage (never plaintext)",
        "Constant-time hash comparison (timing attack prevention)",
        "Session auto-revocation (configurable inactivity timeout)",
        "API key scopes and expiration",
        "CORS policy enforcement",
        "Input validation and sanitization",
        "SQL injection prevention (EF Core parameterized queries)",
        "Rate limiting (configurable per endpoint)"
    ]
    story.append(ListFlowable(
        [ListItem(Paragraph(s, body_style), leftIndent=20) for s in security],
        bulletType='bullet'
    ))

    # --- 7. MCP SERVERS ---
    story.append(PageBreak())
    story.append(Paragraph("7. MCP Servers (Model Context Protocol)", heading1_style))

    story.append(Paragraph(
        "MeepleAI includes 8 containerized MCP servers for enhanced AI capabilities:",
        body_style
    ))
    story.append(Spacer(1, 0.3*cm))

    mcp_data = [
        ["Server", "Purpose", "Status"],
        ["github-project-manager", "GitHub issue/PR management", "Healthy"],
        ["n8n-manager", "Workflow automation", "Healthy"],
        ["memory-bank", "Persistent memory storage", "Healthy"],
        ["sequential", "Sequential reasoning", "Running"],
        ["playwright", "Browser automation", "Running"],
        ["magic", "UI component generation (21st.dev)", "Running"],
        ["context7", "Library documentation (Upstash)", "Running"],
        ["knowledge-graph", "Knowledge graph operations", "Running"],
    ]

    mcp_table = Table(mcp_data, colWidths=[5*cm, 6*cm, 2.5*cm])
    mcp_table.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), colors.HexColor('#3B82F6')),
        ('TEXTCOLOR', (0, 0), (-1, 0), colors.whitesmoke),
        ('ALIGN', (0, 0), (-1, -1), 'LEFT'),
        ('FONTNAME', (0, 0), (-1, 0), 'Helvetica-Bold'),
        ('FONTSIZE', (0, 0), (-1, 0), 10),
        ('BACKGROUND', (0, 1), (-1, -1), colors.beige),
        ('GRID', (0, 0), (-1, -1), 1, colors.grey),
        ('FONTSIZE', (0, 1), (-1, -1), 9),
    ]))
    story.append(mcp_table)
    story.append(Spacer(1, 0.3*cm))

    story.append(Paragraph(
        "All MCP servers are containerized with security constraints: read-only filesystem, "
        "dropped capabilities, no new privileges, resource limits, non-root user.",
        body_style
    ))

    # --- 8. OBSERVABILITY ---
    story.append(PageBreak())
    story.append(Paragraph("8. Observability", heading1_style))

    story.append(Paragraph("Logging (Serilog + Seq):", heading2_style))
    story.append(Paragraph(
        "Centralized logging with Seq dashboard (http://localhost:8081). All logs enriched "
        "with correlation IDs, user information, request details. Structured logging for "
        "queryability.",
        body_style
    ))

    story.append(Paragraph("Distributed Tracing (Jaeger):", heading2_style))
    story.append(Paragraph(
        "OpenTelemetry integration with OTLP export to Jaeger (http://localhost:16686). "
        "W3C Trace Context propagation, custom Activity Sources for RAG/AI operations.",
        body_style
    ))

    story.append(Paragraph("Metrics (Prometheus + Grafana):", heading2_style))
    story.append(Paragraph(
        "Custom metrics for RAG requests, AI tokens, vector search, PDF processing, cache "
        "operations. Prometheus exporter at /metrics, Grafana dashboards (http://localhost:3001) "
        "with auto-provisioning.",
        body_style
    ))

    story.append(Paragraph("Health Checks:", heading2_style))
    story.append(Paragraph(
        "/health (detailed), /health/ready (K8s readiness), /health/live (K8s liveness). "
        "Monitors PostgreSQL, Redis, Qdrant (HTTP + collection).",
        body_style
    ))

    # --- 9. TESTING & QUALITY ---
    story.append(PageBreak())
    story.append(Paragraph("9. Testing & Quality", heading1_style))

    story.append(Paragraph("Backend Testing (xUnit):", heading2_style))
    testing_backend = [
        "Unit tests with Moq for mocking",
        "Integration tests with Testcontainers (PostgreSQL, Qdrant)",
        "WebApplicationFactory for endpoint testing",
        "Coverlet for code coverage",
        "BDD-style tests for features"
    ]
    story.append(ListFlowable(
        [ListItem(Paragraph(t, body_style), leftIndent=20) for t in testing_backend],
        bulletType='bullet'
    ))

    story.append(Paragraph("Frontend Testing:", heading2_style))
    testing_frontend = [
        "Jest unit tests (90% coverage threshold)",
        "React Testing Library for components",
        "Playwright E2E tests",
        "TypeScript strict mode",
        "ESLint + Prettier"
    ]
    story.append(ListFlowable(
        [ListItem(Paragraph(t, body_style), leftIndent=20) for t in testing_frontend],
        bulletType='bullet'
    ))

    story.append(Paragraph("CI/CD (GitHub Actions):", heading2_style))
    cicd = [
        "Automated build and test on every push",
        "CodeQL security scanning (SAST)",
        "Dependency vulnerability scanning",
        ".NET security analyzers",
        "Dependabot for dependency updates",
        "RAG offline evaluation with quality gates"
    ]
    story.append(ListFlowable(
        [ListItem(Paragraph(c, body_style), leftIndent=20) for c in cicd],
        bulletType='bullet'
    ))

    # --- 10. DEPLOYMENT ---
    story.append(PageBreak())
    story.append(Paragraph("10. Deployment", heading1_style))

    story.append(Paragraph("Docker Compose Setup:", heading2_style))
    story.append(Paragraph(
        "MeepleAI uses Docker Compose for local development and deployment. All services "
        "are containerized with proper networking, volumes, and environment configuration.",
        body_style
    ))
    story.append(Spacer(1, 0.3*cm))

    ports_data = [
        ["Service", "Port", "Purpose"],
        ["API", "8080", "Backend REST API"],
        ["Web", "3000", "Frontend Next.js"],
        ["PostgreSQL", "5432", "Relational database"],
        ["Qdrant", "6333/6334", "Vector database"],
        ["Redis", "6379", "Cache"],
        ["n8n", "5678", "Workflow automation"],
        ["Seq", "8081", "Logging dashboard"],
        ["Jaeger", "16686", "Tracing UI"],
        ["Prometheus", "9090", "Metrics"],
        ["Grafana", "3001", "Dashboards"],
    ]

    ports_table = Table(ports_data, colWidths=[4*cm, 2.5*cm, 6*cm])
    ports_table.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), colors.HexColor('#3B82F6')),
        ('TEXTCOLOR', (0, 0), (-1, 0), colors.whitesmoke),
        ('ALIGN', (0, 0), (-1, -1), 'LEFT'),
        ('FONTNAME', (0, 0), (-1, 0), 'Helvetica-Bold'),
        ('FONTSIZE', (0, 0), (-1, 0), 10),
        ('BACKGROUND', (0, 1), (-1, -1), colors.beige),
        ('GRID', (0, 0), (-1, -1), 1, colors.grey),
        ('FONTSIZE', (0, 1), (-1, -1), 9),
    ]))
    story.append(ports_table)
    story.append(Spacer(1, 0.5*cm))

    story.append(Paragraph("Environment Variables:", heading2_style))
    story.append(Paragraph(
        "Configuration uses .env files with templates in infra/env/*.env.*.example. "
        "Key variables: OPENROUTER_API_KEY, QDRANT_URL, REDIS_URL, SEQ_URL, ConnectionStrings, "
        "NEXT_PUBLIC_API_BASE.",
        body_style
    ))

    # --- FOOTER ---
    story.append(PageBreak())
    story.append(Spacer(1, 3*cm))
    story.append(Paragraph("MeepleAI", title_style))
    story.append(Paragraph(
        "AI-Powered Board Game Rules Assistant",
        subtitle_style
    ))
    story.append(Spacer(1, 0.5*cm))
    story.append(Paragraph(
        f"Generated on {datetime.now().strftime('%B %d, %Y')}",
        subtitle_style
    ))

    # Build PDF
    doc.build(story)
    print(f"✅ PDF generated successfully: {filename}")
    return filename

if __name__ == "__main__":
    create_meepleai_pdf()
