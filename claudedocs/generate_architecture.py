"""
MeepleAI Architecture Diagram — v5 Full-page
Content fills entire A3 landscape, no wasted space
"""

from reportlab.lib.pagesizes import A3, landscape
from reportlab.lib.colors import Color, HexColor
from reportlab.pdfgen import canvas
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
import math, os

# ── Fonts ──
FD = r"C:\Users\Utente\.claude\plugins\cache\anthropic-agent-skills\example-skills\f23222824449\skills\canvas-design\canvas-fonts"
for n, f in [("JL","Jura-Light.ttf"),("JM","Jura-Medium.ttf"),
             ("JB","JetBrainsMono-Regular.ttf"),("JBB","JetBrainsMono-Bold.ttf"),
             ("IS","InstrumentSans-Regular.ttf"),("ISB","InstrumentSans-Bold.ttf")]:
    pdfmetrics.registerFont(TTFont(n, os.path.join(FD, f)))

# ── Colors ──
BG=HexColor("#0F1318")
FE=HexColor("#F59E0B"); FE_BG=HexColor("#3D2500")
BE=HexColor("#3B82F6"); BE_BG=HexColor("#142544")
DB=HexColor("#8B5CF6"); DB_BG=HexColor("#1A0D3A")
AI=HexColor("#14B8A6"); AI_BG=HexColor("#0D2E2A")
EX=HexColor("#F43F5E"); EX_BG=HexColor("#3A0A18")
OB=HexColor("#64748B"); OB_BG=HexColor("#1A2030")
CY=HexColor("#06B6D4"); GR=HexColor("#22C55E")
T1=HexColor("#E6EDF3"); T2=HexColor("#8B949E"); T3=HexColor("#5A6370")
DIM=HexColor("#222830")

W, H = landscape(A3)
OUT = os.path.join(os.path.dirname(__file__), "MeepleAI-Architecture.pdf")
c = canvas.Canvas(OUT, pagesize=(W, H))
c.setTitle("MeepleAI - System Architecture")

# ═══════════════════════════════════════════════════════════════
# HELPERS
# ═══════════════════════════════════════════════════════════════
def rrect(x,y,w,h,r=5,fill=None,stroke=None,sw=0.7):
    c.saveState()
    if fill: c.setFillColor(fill)
    if stroke: c.setStrokeColor(stroke); c.setLineWidth(sw)
    p=c.beginPath()
    p.moveTo(x+r,y);p.lineTo(x+w-r,y);p.arcTo(x+w-r,y,x+w,y+r,r)
    p.lineTo(x+w,y+h-r);p.arcTo(x+w,y+h-r,x+w-r,y+h,r)
    p.lineTo(x+r,y+h);p.arcTo(x+r,y+h,x,y+h-r,r)
    p.lineTo(x,y+r);p.arcTo(x,y+r,x+r,y,r);p.close()
    if fill and stroke: c.drawPath(p,fill=1,stroke=1)
    elif fill: c.drawPath(p,fill=1,stroke=0)
    elif stroke: c.drawPath(p,fill=0,stroke=1)
    c.restoreState()

def svc(x,y,w,h,name,port,col,bg,icon=None,sub=None):
    rrect(x,y,w,h,r=4,fill=bg,stroke=Color(col.red,col.green,col.blue,0.28),sw=0.7)
    c.saveState();c.setStrokeColor(col);c.setLineWidth(2);c.setLineCap(1)
    c.line(x+4,y+h,x+w-4,y+h);c.restoreState()
    if icon:
        ix,iy=x+12,y+h-15
        c.saveState()
        c.setFillColor(Color(col.red,col.green,col.blue,0.18))
        c.circle(ix,iy,7.5,fill=1,stroke=0)
        c.setFillColor(col);c.setFont("JBB",7.5)
        c.drawCentredString(ix,iy-2.5,icon);c.restoreState()
    c.saveState();c.setFillColor(T1);c.setFont("ISB",8.5)
    c.drawString(x+(26 if icon else 7),y+h-18,name);c.restoreState()
    if port:
        c.saveState();pt=f":{port}";c.setFont("JB",6)
        pw=c.stringWidth(pt,"JB",6)+6
        rrect(x+w-pw-4,y+h-20,pw,12,r=2,fill=Color(col.red,col.green,col.blue,0.12))
        c.setFillColor(col);c.drawString(x+w-pw-1,y+h-17,pt);c.restoreState()
    if sub:
        c.saveState();c.setFillColor(T3);c.setFont("JB",5.5)
        c.drawString(x+7,y+4,sub);c.restoreState()

def zone(x,y,w,h,label,col,alpha=0.035):
    rrect(x,y,w,h,r=7,fill=Color(col.red,col.green,col.blue,alpha))
    c.saveState()
    c.setFillColor(Color(col.red,col.green,col.blue,0.55))
    c.setFont("JM",7);c.drawString(x+8,y+h-11,label.upper())
    tw=c.stringWidth(label.upper(),"JM",7)
    c.setStrokeColor(Color(col.red,col.green,col.blue,0.18))
    c.setLineWidth(0.3);c.line(x+8,y+h-13,x+8+tw,y+h-13)
    c.restoreState()

def arr(x1,y1,x2,y2,col,w=1.0,dash=False):
    c.saveState()
    a=0.35 if not dash else 0.22
    cc=Color(col.red,col.green,col.blue,a)
    c.setStrokeColor(cc);c.setLineWidth(w)
    if dash:c.setDash(4,3)
    c.line(x1,y1,x2,y2)
    ang=math.atan2(y2-y1,x2-x1);al=5
    c.setFillColor(cc);p=c.beginPath()
    p.moveTo(x2,y2)
    p.lineTo(x2-al*math.cos(ang-0.33),y2-al*math.sin(ang-0.33))
    p.lineTo(x2-al*math.cos(ang+0.33),y2-al*math.sin(ang+0.33))
    p.close();c.drawPath(p,fill=1,stroke=0)
    c.restoreState()

def lbl(x,y,text,col,align="c"):
    c.saveState()
    c.setFillColor(Color(col.red,col.green,col.blue,0.5))
    c.setFont("JB",5.5)
    if align=="c": c.drawCentredString(x,y,text)
    elif align=="l": c.drawString(x,y,text)
    elif align=="r": c.drawRightString(x,y,text)
    c.restoreState()

# ═══════════════════════════════════════════════════════════════
# BACKGROUND
# ═══════════════════════════════════════════════════════════════
c.setFillColor(BG);c.rect(0,0,W,H,fill=1,stroke=0)
c.saveState()
for gx in range(0,int(W),int(20*mm)):
    for gy in range(0,int(H),int(20*mm)):
        c.setFillColor(Color(1,1,1,0.012))
        c.circle(gx,gy,0.4,fill=1,stroke=0)
c.restoreState()

# ═══════════════════════════════════════════════════════════════
# TITLE BAR (compact, 40px)
# ═══════════════════════════════════════════════════════════════
c.saveState()
c.setFillColor(T1);c.setFont("JM",16);c.drawString(28,H-28,"MEEPLEAI")
c.setFillColor(T2);c.setFont("JL",9);c.drawString(115,H-26,"System Architecture")
c.setFillColor(T3);c.setFont("JB",5)
c.drawString(28,H-38,"v2.0 \u2502 .NET 9 + Next.js 16 + Python AI \u2502 CQRS+DDD+RAG \u2502 13 Bounded Contexts \u2502 Docker Compose")
c.restoreState()
c.saveState();c.setStrokeColor(DIM);c.setLineWidth(0.3);c.line(28,H-42,W-28,H-42);c.restoreState()

# Legend (top right, inline)
c.saveState();c.setFont("JB",5);c.setFillColor(T3)
c.drawRightString(W-28,H-38,"meepleai (bridge)  \u2502  profiles: minimal / dev / ai / full")
lx=380;ly=H-38
c.setStrokeColor(T2);c.setLineWidth(0.7)
c.line(lx,ly+3,lx+14,ly+3);c.drawString(lx+17,ly,"Primary")
c.setDash(3,2);c.line(lx+55,ly+3,lx+69,ly+3);c.setDash()
c.drawString(lx+72,ly,"Optional")
for i,(dc,dl) in enumerate([(FE,"Client"),(BE,"API"),(DB,"Data"),(AI,"AI/ML"),(EX,"Ext"),(OB,"Infra")]):
    dx=lx+115+i*42
    c.setFillColor(dc);c.circle(dx,ly+3,2.3,fill=1,stroke=0)
    c.setFillColor(T3);c.setFont("JB",5);c.drawString(dx+4.5,ly,dl)
c.restoreState()

# ═══════════════════════════════════════════════════════════════
# LAYOUT — Fill entire page height
# Title: 42px at top. Content from H-46 down to 18 (bottom margin)
# ═══════════════════════════════════════════════════════════════
M   = 28          # side margins
TOP = H - 48      # content starts here
BOT = 18          # bottom margin
CONTENT_H = TOP - BOT  # ~776 pts available

# 3 rows + 2 gaps with connection channels
R1H = 245   # Row 1: Client, API, External (tallest — has BCs)
G1  = 80    # Gap 1: connection channel with staggered labels
R2H = 215   # Row 2: Persistence, AI Services
R3H = 150   # Row 3: Data Flows, Observability (fixed)
G2  = CONTENT_H - R1H - G1 - R2H - R3H  # Gap 2: remaining space (~86)

R1_BOT = TOP - R1H          # bottom of row 1
CH_TOP = R1_BOT              # channel top = row1 bottom
CH_BOT = R1_BOT - G1         # channel bottom
R2_TOP = CH_BOT              # row 2 top
R2_BOT = R2_TOP - R2H        # row 2 bottom
R3_TOP = R2_BOT - G2         # row 3 top
R3_BOT = R3_TOP - R3H        # row 3 bottom

# ═══════════════════════════════════════════════════════════════
# ROW 1: CLIENT | API | EXTERNAL
# ═══════════════════════════════════════════════════════════════
# Split width: Client 16%, API 56%, External 16%, gaps 6% each
TOTAL_W = W - 2*M   # ~1135
GAP_W   = 14

CL_W = 185;  CL_X = M
AP_W = 580;  AP_X = CL_X + CL_W + GAP_W
EX_W = TOTAL_W - CL_W - AP_W - 2*GAP_W;  EX_X = AP_X + AP_W + GAP_W

# Client
zone(CL_X, R1_BOT, CL_W, R1H, "Client Layer", FE)
svc(CL_X+8, R1_BOT+R1H-48, 168, 34, "Browser", None, FE, FE_BG, "B", "React 19 + Tailwind 4")
svc(CL_X+8, R1_BOT+R1H-90, 168, 34, "Next.js 16", "3000", FE, FE_BG, "N", "App Router + SSR")
# Extra info
c.saveState();c.setFillColor(Color(FE.red,FE.green,FE.blue,0.3))
c.setFont("JB",5);c.drawString(CL_X+8, R1_BOT+8, "Catch-all proxy + SignalR hub")
c.restoreState()

# API (center, dominant)
zone(AP_X, R1_BOT, AP_W, R1H, "Application Layer \u2014 .NET 9 Minimal API", BE)
svc(AP_X+8, R1_BOT+R1H-50, 250, 36, ".NET 9 API", "8080", BE, BE_BG, "A", "MediatR + CQRS + DDD + FluentValidation")

# All 13 BCs — 5 columns × 3 rows
bcs = [
    "Authentication","GameManagement","KnowledgeBase","SharedGameCatalog","DocumentProc",
    "Administration","UserLibrary","Gamification","SessionTracking","UserNotifications",
    "WorkflowIntegr.","SystemConfig","BusinessSimul.",
]
bc_cols=5; bc_cw=112; bc_rh=17
bc_x0=AP_X+8; bc_y0=R1_BOT+8
for i,bcn in enumerate(bcs):
    ci=i%bc_cols; ri=i//bc_cols
    bx=bc_x0+ci*bc_cw; by=bc_y0+(2-ri)*bc_rh
    if bx+bc_cw-3 > AP_X+AP_W-4: continue
    rrect(bx,by,bc_cw-3,bc_rh-3,r=2,
          fill=Color(BE.red,BE.green,BE.blue,0.05),
          stroke=Color(BE.red,BE.green,BE.blue,0.08),sw=0.3)
    c.saveState();c.setFillColor(T2);c.setFont("IS",6)
    c.drawString(bx+4,by+4,bcn);c.restoreState()

# External
zone(EX_X, R1_BOT, EX_W, R1H, "External APIs", EX)
svc(EX_X+8, R1_BOT+R1H-48, EX_W-16, 34, "OpenRouter", "443", EX, EX_BG, "O", "LLM Gateway (Claude/GPT)")
svc(EX_X+8, R1_BOT+R1H-90, EX_W-16, 34, "BoardGameGeek", "443", EX, EX_BG, "B", "XML API v2")
c.saveState();c.setFillColor(Color(EX.red,EX.green,EX.blue,0.3))
c.setFont("JB",5);c.drawString(EX_X+8, R1_BOT+8, "Polly: 3 retries + circuit breaker")
c.restoreState()


# ═══════════════════════════════════════════════════════════════
# ROW 2: PERSISTENCE | AI SERVICES
# ═══════════════════════════════════════════════════════════════
PZ_W = 365;  PZ_X = M
AZ_W = TOTAL_W - PZ_W - GAP_W;  AZ_X = PZ_X + PZ_W + GAP_W

pn_w = (PZ_W - 22) // 2   # ~171 per node

# Persistence
zone(PZ_X, R2_BOT, PZ_W, R2H, "Persistence Layer", DB)
svc(PZ_X+8,        R2_BOT+R2H-48, pn_w, 34, "PostgreSQL 16", "5432", DB, DB_BG, "P", "EF Core + pgvector")
svc(PZ_X+8+pn_w+6, R2_BOT+R2H-48, pn_w, 34, "Qdrant", "6333", AI, AI_BG, "Q", "Vector DB + gRPC :6334")
svc(PZ_X+8,        R2_BOT+8, pn_w, 34, "Redis 7.4", "6379", DB, DB_BG, "R", "HybridCache L2 + Sessions")
svc(PZ_X+8+pn_w+6, R2_BOT+8, pn_w, 34, "S3 / MinIO", "9000", OB, OB_BG, "S", "Blob Storage (R2)")
c.saveState();c.setFillColor(Color(DB.red,DB.green,DB.blue,0.2))
c.setFont("JB",5);c.drawCentredString(PZ_X+PZ_W/2, R2_BOT+R2H/2, "Soft delete + Audit + Concurrency (RowVersion)")
c.restoreState()

# AI Services
ai_nw = (AZ_W - 26) // 3
zone(AZ_X, R2_BOT, AZ_W, R2H, "AI / ML Services \u2014 Python + FastAPI", AI)
svc(AZ_X+8,             R2_BOT+R2H-48, ai_nw, 34, "Embedding", "8000", AI, AI_BG, "E", "sentence-transformers")
svc(AZ_X+8+ai_nw+5,     R2_BOT+R2H-48, ai_nw, 34, "Reranker", "8003", AI, AI_BG, "R", "cross-encoder")
svc(AZ_X+8+2*(ai_nw+5), R2_BOT+R2H-48, ai_nw, 34, "Orchestration", "8004", CY,
    Color(0.04,0.11,0.16), "O", "LangGraph Multi-Agent")
svc(AZ_X+8,             R2_BOT+8, ai_nw, 34, "Unstructured", "8001", AI, AI_BG, "U", "PDF Stage 1 (fast)")
svc(AZ_X+8+ai_nw+5,     R2_BOT+8, ai_nw, 34, "SmolDocling", "8002", AI, AI_BG, "D", "PDF Stage 2 (VLM)")
svc(AZ_X+8+2*(ai_nw+5), R2_BOT+8, ai_nw, 34, "Ollama", "11434", OB, OB_BG, "L", "Local LLM fallback")
c.saveState();c.setFillColor(Color(CY.red,CY.green,CY.blue,0.18))
c.setFont("JB",5);c.drawCentredString(AZ_X+AZ_W/2, R2_BOT+R2H/2, "Agents: Tutor  \u00b7  Arbitro  \u00b7  Decisore")
c.restoreState()


# ═══════════════════════════════════════════════════════════════
# ROW 3: DATA FLOWS | OBSERVABILITY
# ═══════════════════════════════════════════════════════════════
FZ_W = 365;  FZ_X = M
OZ_W = TOTAL_W - FZ_W - GAP_W;  OZ_X = FZ_X + FZ_W + GAP_W

# Data Flows
zone(FZ_X, R3_BOT, FZ_W, R3H, "Key Data Flows", Color(1,1,1), alpha=0.015)
flows = [
    ("RAG Chat",    "Query \u2192 Embed \u2192 Vector Search \u2192 Rerank \u2192 LLM \u2192 SSE", AI),
    ("PDF Upload",  "Upload \u2192 Extract (S1/S2) \u2192 Chunk \u2192 Embed \u2192 Qdrant + S3", AI),
    ("Multi-Agent", "Request \u2192 Orchestrator \u2192 Tutor|Arbitro|Decisore \u2192 Response", CY),
    ("Game Import", "BGG XML \u2192 Parse \u2192 Validate \u2192 PostgreSQL + Cache", EX),
    ("Auth Flow",   "Login \u2192 Session Cookie \u2192 Redis \u2192 Middleware Check", BE),
    ("Real-time",   "Game State \u2192 SignalR Hub \u2192 WebSocket \u2192 Browser", FE),
]
for i,(fn,fd,fc) in enumerate(flows):
    fy=R3_BOT+R3H-24-i*16
    c.saveState()
    c.setFillColor(fc);c.circle(FZ_X+12,fy+3,2.3,fill=1,stroke=0)
    c.setFillColor(T1);c.setFont("ISB",6.5);c.drawString(FZ_X+19,fy,fn)
    c.setFillColor(T3);c.setFont("JB",5);c.drawString(FZ_X+19,fy-8,fd)
    c.restoreState()

# Observability
ob_nw = (OZ_W - 26) // 3
zone(OZ_X, R3_BOT, OZ_W, R3H, "Observability & Automation", OB, alpha=0.025)
svc(OZ_X+8,             R3_BOT+R3H-44, ob_nw, 30, "Prometheus", "9090", OB, OB_BG, "P", "Metrics")
svc(OZ_X+8+ob_nw+5,     R3_BOT+R3H-44, ob_nw, 30, "Grafana", "3001", GR, Color(0.04,0.10,0.04), "G", "Dashboards")
svc(OZ_X+8+2*(ob_nw+5), R3_BOT+R3H-44, ob_nw, 30, "HyperDX", "8180", CY, Color(0.04,0.10,0.14), "H", "OTLP Traces")
svc(OZ_X+8,             R3_BOT+6, ob_nw, 30, "n8n", "5678", OB, OB_BG, "W", "Workflows")
svc(OZ_X+8+ob_nw+5,     R3_BOT+6, ob_nw, 30, "Mailpit", "8025", OB, OB_BG, "M", "Dev SMTP")
svc(OZ_X+8+2*(ob_nw+5), R3_BOT+6, ob_nw, 30, "Alertmanager", "9093", OB, OB_BG, "!", "Alerts")

# Grafana → Prometheus subtle
c.saveState()
c.setStrokeColor(Color(GR.red,GR.green,GR.blue,0.18));c.setLineWidth(0.4)
c.line(OZ_X+8+ob_nw+5, R3_BOT+R3H-30, OZ_X+8+ob_nw, R3_BOT+R3H-30)
c.restoreState()


# ═══════════════════════════════════════════════════════════════
# CONNECTIONS — Elbow routing (right-angle bends)
# Each connection: vertical down → horizontal → vertical down
# Unique turn-Y in the channel prevents any crossing
# ═══════════════════════════════════════════════════════════════

def elbow(exit_x, exit_y, turn_y, target_x, target_y, col, w=1.0, dash=False, label=None):
    """L-shaped connection: down from exit, horizontal at turn_y, down to target."""
    c.saveState()
    a = 0.35 if not dash else 0.20
    cc = Color(col.red, col.green, col.blue, a)
    c.setStrokeColor(cc); c.setLineWidth(w)
    if dash: c.setDash(4, 3)
    # 3 segments
    c.line(exit_x, exit_y, exit_x, turn_y)      # vertical down from API
    c.line(exit_x, turn_y, target_x, turn_y)     # horizontal to target column
    c.line(target_x, turn_y, target_x, target_y)  # vertical down to target
    # arrowhead at end (pointing down)
    c.setFillColor(cc); p = c.beginPath()
    p.moveTo(target_x, target_y)
    p.lineTo(target_x - 3.5, target_y + 5.5)
    p.lineTo(target_x + 3.5, target_y + 5.5)
    p.close(); c.drawPath(p, fill=1, stroke=0)
    # label on horizontal segment
    if label:
        lx = (exit_x + target_x) / 2
        c.setFillColor(Color(col.red, col.green, col.blue, 0.5))
        c.setFont("JB", 5.5)
        c.drawCentredString(lx, turn_y + 5, label)
    c.restoreState()

# ── HORIZONTAL connections (Row 1, same row) ──

# Client → API
y_h1 = R1_BOT + R1H - 73
arr(CL_X+CL_W, y_h1, AP_X, y_h1, FE, w=1.8)
lbl((CL_X+CL_W+AP_X)/2, y_h1+7, "/api/v1/* catch-all proxy", FE)

# API → OpenRouter
y_h2 = R1_BOT + R1H - 31
arr(AP_X+AP_W, y_h2, EX_X, y_h2, EX, w=1.4)
lbl((AP_X+AP_W+EX_X)/2, y_h2+7, "LLM Inference (SSE stream)", EX)

# API → BGG
y_h3 = R1_BOT + R1H - 73
arr(AP_X+AP_W, y_h3, EX_X, y_h3, EX, w=0.8, dash=True)
lbl((AP_X+AP_W+EX_X)/2, y_h3+7, "XML API v2 + rate limit", EX)


# ── ELBOW connections (API → Row 2 through the channel) ──
# Channel: CH_TOP (549) → CH_BOT (469), 80px tall
# 8 connections, each gets a unique turn_y spaced ~9px apart

# Target node centers
pg_cx = PZ_X + 8 + pn_w/2                          # PostgreSQL
qd_cx = PZ_X + 8 + pn_w + 6 + pn_w/2              # Qdrant
rd_cx = PZ_X + 8 + pn_w/2                          # Redis (bottom)
s3_cx = PZ_X + 8 + pn_w + 6 + pn_w/2              # S3 (bottom)
em_cx = AZ_X + 8 + ai_nw/2                         # Embedding
rr_cx = AZ_X + 8 + ai_nw + 5 + ai_nw/2            # Reranker
or_cx = AZ_X + 8 + 2*(ai_nw+5) + ai_nw/2          # Orchestration
un_cx = AZ_X + 8 + ai_nw/2                         # Unstructured (bottom)

# Top of target nodes
r2_top_row = R2_BOT + R2H             # top of zone (for top-row nodes)
r2_bot_row = R2_BOT + 42              # mid of zone (for bottom-row nodes)

# Exit X positions from API bottom (evenly spaced)
def apx(f): return AP_X + 30 + (AP_W-60)*f

# Turn Y positions (8 levels, evenly spaced in channel)
# Outermost connections (going furthest) turn first (near API)
# Innermost connections (going least far) turn last (near targets)
ty = [CH_TOP - 8 - i*8 for i in range(8)]  # 8 levels, 8px apart

# LEFT group: sorted by horizontal distance from API (furthest first)
# 1. PostgreSQL — furthest left, turns first
elbow(apx(0.05), CH_TOP, ty[0], pg_cx, r2_top_row, DB, w=1.3, label="EF Core + pgvector")

# 2. Redis — same column as PG but bottom row
elbow(apx(0.10), CH_TOP, ty[1], rd_cx, r2_bot_row, DB, w=0.8, dash=True, label="Cache + Sessions")

# 3. Qdrant — closer to API
elbow(apx(0.20), CH_TOP, ty[2], qd_cx, r2_top_row, AI, w=1.2, label="Vector Search (cosine)")

# 4. S3/MinIO — same column as Qdrant, bottom row
elbow(apx(0.26), CH_TOP, ty[3], s3_cx, r2_bot_row, OB, w=0.6, dash=True, label="Blob Storage")

# RIGHT group: sorted by distance from API center
# 5. Embedding — closest right target
elbow(apx(0.52), CH_TOP, ty[4], em_cx, r2_top_row, AI, w=1.3, label="POST /embeddings")

# 6. Unstructured — same column as Embedding, bottom row
elbow(apx(0.58), CH_TOP, ty[5], un_cx, r2_bot_row, AI, w=0.7, dash=True, label="PDF Extract (S1/S2)")

# 7. Reranker
elbow(apx(0.70), CH_TOP, ty[6], rr_cx, r2_top_row, AI, w=1.0, label="POST /rerank")

# 8. Orchestration — furthest right, turns last
elbow(apx(0.82), CH_TOP, ty[7], or_cx, r2_top_row, CY, w=1.3, label="POST /execute")


# ── OBSERVABILITY (subtle edge-routed paths) ──

# API → HyperDX (OTLP push, right edge)
c.saveState()
cc = Color(CY.red, CY.green, CY.blue, 0.12)
c.setStrokeColor(cc); c.setLineWidth(0.5); c.setDash(3, 3)
edge_r = W - M - 4
c.line(AP_X+AP_W-5, R1_BOT+2, edge_r, R1_BOT+2)
c.line(edge_r, R1_BOT+2, edge_r, R3_BOT+R3H-28)
c.line(edge_r, R3_BOT+R3H-28, OZ_X+8+2*(ob_nw+5)+ob_nw, R3_BOT+R3H-28)
c.restoreState()
lbl(edge_r-3, (R2_BOT+R2_BOT+R2H)/2, "OTLP", CY, "r")

# Prometheus → API (scrape, left edge)
c.saveState()
cc = Color(OB.red, OB.green, OB.blue, 0.12)
c.setStrokeColor(cc); c.setLineWidth(0.5); c.setDash(3, 3)
edge_l = M + 4
c.line(OZ_X+8, R3_BOT+R3H-28, edge_l, R3_BOT+R3H-28)
c.line(edge_l, R3_BOT+R3H-28, edge_l, R1_BOT+2)
c.line(edge_l, R1_BOT+2, AP_X+5, R1_BOT+2)
c.restoreState()
lbl(edge_l+3, (R2_BOT+R2_BOT+R2H)/2, "scrape /metrics", OB, "l")

# ── Internal AI zone: Orchestration → Embedding/Reranker (health check) ──
c.saveState()
c.setStrokeColor(Color(CY.red,CY.green,CY.blue,0.12))
c.setLineWidth(0.4); c.setDash(2, 2)
orch_l = AZ_X + 8 + 2*(ai_nw+5)
c.line(orch_l, R2_BOT+R2H-32, AZ_X+8+ai_nw, R2_BOT+R2H-32)
c.line(orch_l, R2_BOT+R2H-37, AZ_X+8+ai_nw+5+ai_nw, R2_BOT+R2H-37)
c.restoreState()

# ═══════════════════════════════════════════════════════════════
# CROSS-ZONE CONNECTIONS (Row 2 ↔ Row 2, Row 2 → Row 1, Row 2 ↔ Row 3)
# These are direct connections NOT mediated by the .NET API
# ═══════════════════════════════════════════════════════════════

# ── Orchestration → Persistence (direct, horizontal within Row 2) ──
# These connections go LEFT from the AI zone to the Persistence zone

def horiz_conn(x1, y, x2, col, w=0.8, dash=True, label=None, label_y_off=5):
    """Horizontal connection within a row."""
    c.saveState()
    a = 0.25 if dash else 0.35
    cc = Color(col.red, col.green, col.blue, a)
    c.setStrokeColor(cc); c.setLineWidth(w)
    if dash: c.setDash(3, 3)
    c.line(x1, y, x2, y)
    # arrowhead
    c.setFillColor(cc); p = c.beginPath()
    if x2 < x1:  # pointing left
        p.moveTo(x2, y); p.lineTo(x2+5, y-3); p.lineTo(x2+5, y+3)
    else:  # pointing right
        p.moveTo(x2, y); p.lineTo(x2-5, y-3); p.lineTo(x2-5, y+3)
    p.close(); c.drawPath(p, fill=1, stroke=0)
    if label:
        c.setFillColor(Color(col.red, col.green, col.blue, 0.45))
        c.setFont("JB", 5)
        c.drawCentredString((x1+x2)/2, y+label_y_off, label)
    c.restoreState()

# Orchestration → PostgreSQL (conversation memory, asyncpg direct)
orch_node_left = AZ_X + 8 + 2*(ai_nw+5)
pg_node_right = PZ_X + 8 + pn_w
orch_mid_y = R2_BOT + R2H/2 + 12
horiz_conn(orch_node_left, orch_mid_y, pg_node_right, CY, w=0.7,
           label="asyncpg (conversation state)")

# Orchestration → Redis (intent cache + rule cache, direct)
rd_right = PZ_X + 8 + pn_w
horiz_conn(orch_node_left, orch_mid_y - 12, rd_right, CY, w=0.6,
           label="redis.asyncio (intent + rule cache)")

# Orchestration → OpenRouter (direct LLM calls, goes UP to External APIs)
# Vertical from Orchestration up, then horizontal to OpenRouter
c.saveState()
cc = Color(EX.red, EX.green, EX.blue, 0.18)
c.setStrokeColor(cc); c.setLineWidth(0.5); c.setDash(3, 3)
# From Orchestration top, go right to near-edge, up to External zone
orch_top = R2_BOT + R2H
or_right = AZ_X + 8 + 2*(ai_nw+5) + ai_nw  # right edge of Orchestration node
ext_mid_y = R1_BOT + R1H - 31  # same Y as API→OpenRouter
# Path: right of orch → up along a channel → into OpenRouter
route_x = or_right + 8
c.line(or_right, orch_top - 16, route_x, orch_top - 16)  # short horizontal right
c.line(route_x, orch_top - 16, route_x, ext_mid_y - 15)  # vertical up
c.line(route_x, ext_mid_y - 15, EX_X, ext_mid_y - 15)    # horizontal to External
c.restoreState()
lbl(route_x - 3, (orch_top + ext_mid_y) / 2, "LLM (direct)", EX, "r")

# Orchestration → .NET API (bidirectional — calls back for hybrid search)
c.saveState()
cc = Color(BE.red, BE.green, BE.blue, 0.15)
c.setStrokeColor(cc); c.setLineWidth(0.5); c.setDash(2, 3)
# Small upward arrow from Orchestration to API zone
c.line(or_cx, orch_top, or_cx - 15, CH_BOT + 5)
# arrowhead pointing up
c.setFillColor(cc); p = c.beginPath()
p.moveTo(or_cx - 15, CH_BOT + 5)
p.lineTo(or_cx - 18.5, CH_BOT + 10.5)
p.lineTo(or_cx - 11.5, CH_BOT + 10.5)
p.close(); c.drawPath(p, fill=1, stroke=0)
c.restoreState()
lbl(or_cx + 10, CH_BOT + 10, "callback: /api/v1/kb/search", BE, "l")

# ── n8n → PostgreSQL (own DB for workflow state) ──
# n8n is in Row 3, PostgreSQL in Row 2 — subtle connection
c.saveState()
cc = Color(OB.red, OB.green, OB.blue, 0.10)
c.setStrokeColor(cc); c.setLineWidth(0.4); c.setDash(2, 3)
n8n_cx = OZ_X + 8 + ob_nw/2  # n8n center X
c.line(n8n_cx, R3_BOT + R3H, n8n_cx, R3_BOT + R3H + 4)  # tiny up from n8n
c.line(n8n_cx, R3_BOT + R3H + 4, pg_cx + 30, R3_BOT + R3H + 4)  # horizontal left
c.line(pg_cx + 30, R3_BOT + R3H + 4, pg_cx + 30, R2_BOT)  # up to persistence zone bottom
c.restoreState()
lbl(OZ_X - 50, R2_BOT - 8, "n8n \u2192 PG (workflow state)", OB, "l")


# ═══════════════════════════════════════════════════════════════
c.save()
print(f"Saved: {OUT}")
print(f"  A3 landscape: {W:.0f} x {H:.0f} pts")
print(f"  Row 1 (y={R1_BOT:.0f}-{R1_BOT+R1H:.0f}), Channel ({CH_BOT:.0f}-{CH_TOP:.0f}), Row 2 ({R2_BOT:.0f}-{R2_TOP+R2H:.0f}), Row 3 ({R3_BOT:.0f}-{R3_BOT+R3H:.0f})")
