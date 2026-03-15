"""
MeepleCard Anatomy — Schematic Render
Generates a clean technical diagram showing all card zones
"""
from PIL import Image, ImageDraw, ImageFont
import os

# --- Canvas ---
W, H = 1800, 2400
BG = (15, 17, 23)         # #0f1117
CARD_BG = (26, 29, 39)    # #1a1d27
WHITE = (241, 245, 249)
MUTED = (148, 163, 184)   # #94a3b8
DIM = (71, 85, 105)       # #475569
ACCENT_ORANGE = (217, 119, 6)    # game entity
ACCENT_INDIGO = (99, 102, 241)   # session entity
ACCENT_PURPLE = (168, 85, 247)   # player
ACCENT_AMBER = (245, 158, 11)
ACCENT_TEAL = (20, 184, 166)
ACCENT_GREEN = (34, 197, 94)
ACCENT_ROSE = (244, 63, 94)
ACCENT_BLUE = (59, 130, 246)

# Font paths
FONT_DIR = r"C:\Users\Utente\.claude\plugins\cache\anthropic-agent-skills\example-skills\f23222824449\skills\canvas-design\canvas-fonts"

def load_font(name, size):
    path = os.path.join(FONT_DIR, name)
    try:
        return ImageFont.truetype(path, size)
    except:
        return ImageFont.load_default()

font_title = load_font("BricolageGrotesque-Bold.ttf", 48)
font_heading = load_font("BricolageGrotesque-Bold.ttf", 28)
font_label = load_font("DMMono-Regular.ttf", 18)
font_label_sm = load_font("DMMono-Regular.ttf", 15)
font_mono = load_font("GeistMono-Bold.ttf", 14)
font_zone = load_font("BricolageGrotesque-Regular.ttf", 20)
font_entity = load_font("BigShoulders-Bold.ttf", 16)

img = Image.new("RGB", (W, H), BG)
draw = ImageDraw.Draw(img)

# =============================================
# TITLE
# =============================================
draw.text((W//2, 50), "MEEPLECARD ANATOMY", fill=WHITE, font=font_title, anchor="mt")
draw.text((W//2, 105), "Zone schematiche per entity type", fill=MUTED, font=font_zone, anchor="mt")

# =============================================
# CARD 1: GAME CARD (Fronte)
# =============================================
cx1, cy1 = 480, 850
cw, ch = 320, 460
r = 14

def draw_card_outline(cx, cy, w, h, color, label_text, label_y_offset=-30):
    x1, y1 = cx - w//2, cy - h//2
    x2, y2 = cx + w//2, cy + h//2
    # Card background
    draw.rounded_rectangle([x1, y1, x2, y2], radius=r, fill=CARD_BG, outline=(50, 55, 70), width=1)
    # Entity accent bar (left)
    draw.rectangle([x1, y1+r, x1+5, y2-r], fill=color)
    return x1, y1, x2, y2

# --- GAME FRONT ---
draw.text((cx1, 170), "FRONTE", fill=ACCENT_ORANGE, font=font_heading, anchor="mt")
draw.text((cx1, 205), "Game Card · grid variant", fill=DIM, font=font_label, anchor="mt")

gx1, gy1, gx2, gy2 = draw_card_outline(cx1, cy1, cw, ch, ACCENT_ORANGE, "GAME")

# Zone: Cover Image (top 55%)
cover_h = int(ch * 0.55)
cover_y2 = gy1 + cover_h
# Crosshatch pattern for image area
for i in range(gy1+r, cover_y2, 8):
    draw.line([(gx1+10, i), (gx2-10, i)], fill=(40, 44, 55), width=1)
draw.text((cx1, gy1 + cover_h//2), "COVER IMAGE", fill=DIM, font=font_label, anchor="mm")
draw.text((cx1, gy1 + cover_h//2 + 22), "55% height", fill=(55, 60, 75), font=font_label_sm, anchor="mm")

# Gradient overlay hint
for i in range(20):
    alpha_sim = 30 + i * 3
    y = cover_y2 - 20 + i
    draw.line([(gx1+6, y), (gx2-6, y)], fill=(26, 29, 39), width=1)

# Zone: Entity Badge (top-left)
badge_x, badge_y = gx1 + 18, gy1 + 14
draw.rounded_rectangle([badge_x, badge_y, badge_x+55, badge_y+20], radius=5, fill=ACCENT_ORANGE)
draw.text((badge_x+27, badge_y+10), "GAME", fill=WHITE, font=font_entity, anchor="mm")

# Zone: Status chips (top-right)
stat_x = gx2 - 18
draw.rounded_rectangle([stat_x-72, badge_y, stat_x, badge_y+20], radius=5, fill=ACCENT_GREEN)
draw.text((stat_x-36, badge_y+10), "OWNED", fill=WHITE, font=font_entity, anchor="mm")

# Zone: Action buttons (top-right, below status)
act_y = badge_y + 28
for i in range(2):
    ax = gx2 - 18 - i * 34
    draw.rounded_rectangle([ax-26, act_y, ax, act_y+26], radius=6, outline=DIM, width=1)
    symbols = ["♡", "⋯"]
    draw.text((ax-13, act_y+13), symbols[i], fill=MUTED, font=font_label_sm, anchor="mm")

# Zone: Tag Strip (left edge)
strip_x = gx1 + 12
for i, color in enumerate([ACCENT_BLUE, ACCENT_ROSE, ACCENT_AMBER]):
    sy = cover_y2 - 70 + i * 22
    draw.rounded_rectangle([strip_x, sy, strip_x+6, sy+16], radius=3, fill=color)

# Zone: Card Body
body_y = cover_y2 + 4
# Title
draw.text((gx1+16, body_y + 8), "Terraforming Mars", fill=WHITE, font=font_zone, anchor="lt")
# Subtitle
draw.text((gx1+16, body_y + 34), "FryxGames · 2016", fill=MUTED, font=font_label_sm, anchor="lt")
# Rating
draw.text((gx1+16, body_y + 56), "★★★★☆", fill=ACCENT_AMBER, font=font_label, anchor="lt")
draw.text((gx1+100, body_y + 56), "8.4", fill=MUTED, font=font_label, anchor="lt")
# Tags
tag_y = body_y + 82
tags_data = ["Strategico", "2-5p", "120min"]
tx = gx1 + 16
for tag in tags_data:
    tw = len(tag) * 8 + 14
    draw.rounded_rectangle([tx, tag_y, tx+tw, tag_y+20], radius=5, fill=(35, 40, 55))
    draw.text((tx+tw//2, tag_y+10), tag, fill=MUTED, font=font_label_sm, anchor="mm")
    tx += tw + 5

# Zone: Link Footer
link_y = gy2 - 36
draw.line([(gx1+10, link_y-4), (gx2-10, link_y-4)], fill=(40, 44, 55), width=1)
link_icons = [
    (ACCENT_INDIGO, "🎯"),
    (ACCENT_TEAL, "📚"),
    (ACCENT_AMBER, "🤖"),
]
lx = gx1 + 22
for color, icon in link_icons:
    draw.rounded_rectangle([lx-8, link_y, lx+14, link_y+22], radius=5, fill=(*color, 40) if len(color)==3 else color, outline=color, width=1)
    lx += 30
draw.text((gx2-22, link_y+11), "+2", fill=DIM, font=font_label_sm, anchor="mm")

# Zone: Info button
info_x, info_y = gx2 - 22, link_y - 34
draw.ellipse([info_x-12, info_y-12, info_x+12, info_y+12], outline=DIM, width=1)
draw.text((info_x, info_y), "i", fill=MUTED, font=font_label, anchor="mm")

# Zone: Flip hint
draw.text((gx2-16, gy2-10), "↻", fill=DIM, font=font_label_sm, anchor="rb")


# =============================================
# ANNOTATIONS (Game Front)
# =============================================
def draw_annotation(x1, y1, x2, y2, text, side="right", color=MUTED):
    """Draw a line from (x1,y1) to (x2,y2) with text label"""
    draw.line([(x1, y1), (x2, y2)], fill=color, width=1)
    draw.ellipse([x1-3, y1-3, x1+3, y1+3], fill=color)
    if side == "right":
        draw.text((x2+8, y2), text, fill=color, font=font_label_sm, anchor="lm")
    else:
        draw.text((x2-8, y2), text, fill=color, font=font_label_sm, anchor="rm")

# Annotations for game front
ann_x = gx2 + 30
draw_annotation(gx1+45, gy1+24, ann_x+60, gy1+24, "① Entity Badge", color=ACCENT_ORANGE)
draw_annotation(gx2-55, gy1+24, ann_x+60, gy1+54, "② Status Chip", color=ACCENT_GREEN)
draw_annotation(gx2-30, act_y+13, ann_x+60, act_y+13, "③ Action Buttons (hover)", color=MUTED)
draw_annotation(gx1+15, cover_y2-40, gx1-60, cover_y2-40, "④ Tag Strip", side="left", color=ACCENT_BLUE)
draw_annotation(cx1, gy1+cover_h//2, gx1-60, gy1+cover_h//2, "⑤ Cover Image 55%", side="left", color=DIM)
draw_annotation(gx1+16, body_y+18, gx1-60, body_y+18, "⑥ Title + Subtitle", side="left", color=WHITE)
draw_annotation(gx1+60, body_y+60, gx1-60, body_y+60, "⑦ Rating", side="left", color=ACCENT_AMBER)
draw_annotation(tx-20, tag_y+10, ann_x+60, tag_y+80, "⑧ Tag Labels", color=MUTED)
draw_annotation(lx-50, link_y+11, ann_x+60, link_y+11, "⑨ Link Footer", color=ACCENT_INDIGO)
draw_annotation(info_x, info_y, ann_x+60, info_y-20, "⑩ Info Button → Drawer", color=MUTED)
draw_annotation(gx2-16, gy2-14, ann_x+60, gy2-14, "⑪ Flip Hint", color=DIM)
draw_annotation(gx1+3, cy1, gx1-60, cy1, "⑫ Entity Accent Bar", side="left", color=ACCENT_ORANGE)


# =============================================
# CARD 2: GAME CARD (Retro / Back)
# =============================================
cx2 = cx1 + cw + 280
draw.text((cx2, 170), "RETRO", fill=ACCENT_ORANGE, font=font_heading, anchor="mt")
draw.text((cx2, 205), "Game Card · flip back", fill=DIM, font=font_label, anchor="mt")

bx1, by1, bx2, by2 = draw_card_outline(cx2, cy1, cw, ch, ACCENT_ORANGE, "BACK")

# Back content sections
sec_y = by1 + 16
sections = [
    ("STATISTICHE", ACCENT_ORANGE, [
        "Partite: 12", "Win rate: 42%", "Tempo medio: 95min",
        "Ultimo: 3 giorni fa"
    ]),
    ("AZIONI", ACCENT_AMBER, [
        "→ Nuova sessione",
        "→ Aggiungi a collezione",
        "→ Chiedi all'AI",
        "→ Condividi"
    ]),
    ("KB PREVIEW", ACCENT_TEAL, [
        "12 docs · 245 chunks",
        "Ultima query: 2h fa"
    ]),
]

for title, color, items in sections:
    draw.text((bx1+16, sec_y), title, fill=color, font=font_entity, anchor="lt")
    sec_y += 22
    draw.line([(bx1+16, sec_y), (bx2-16, sec_y)], fill=(40, 44, 55), width=1)
    sec_y += 8
    for item in items:
        draw.text((bx1+20, sec_y), item, fill=MUTED, font=font_label_sm, anchor="lt")
        sec_y += 20
    sec_y += 16

# Detail link at bottom
draw.text((cx2, by2-20), "Vai al dettaglio →", fill=ACCENT_ORANGE, font=font_label_sm, anchor="mb")

# Annotations for back
ann_bx = bx2 + 30
draw_annotation(bx1+60, by1+26, ann_bx+40, by1+26, "① Sezione Stats", color=ACCENT_ORANGE)
draw_annotation(bx1+60, by1+150, ann_bx+40, by1+150, "② Quick Actions", color=ACCENT_AMBER)
draw_annotation(bx1+60, by1+290, ann_bx+40, by1+290, "③ KB Preview", color=ACCENT_TEAL)
draw_annotation(cx2, by2-24, ann_bx+40, by2-24, "④ Detail Link", color=ACCENT_ORANGE)


# =============================================
# CARD 3: SESSION CARD (Fronte)
# =============================================
cx3, cy3 = 480, 1650
draw.text((cx3, cy3 - ch//2 - 70), "FRONTE", fill=ACCENT_INDIGO, font=font_heading, anchor="mt")
draw.text((cx3, cy3 - ch//2 - 35), "Session Card · in corso", fill=DIM, font=font_label, anchor="mt")

sx1, sy1, sx2, sy2 = draw_card_outline(cx3, cy3, cw, ch, ACCENT_INDIGO, "SESSION")

# Cover
s_cover_h = int(ch * 0.55)
s_cover_y2 = sy1 + s_cover_h
for i in range(sy1+r, s_cover_y2, 8):
    draw.line([(sx1+10, i), (sx2-10, i)], fill=(35, 38, 60), width=1)
draw.text((cx3, sy1 + s_cover_h//2), "COVER IMAGE", fill=(60, 65, 90), font=font_label, anchor="mm")

# Entity badge
draw.rounded_rectangle([sx1+18, sy1+14, sx1+85, sy1+34], radius=5, fill=ACCENT_INDIGO)
draw.text((sx1+51, sy1+24), "SESSION", fill=WHITE, font=font_entity, anchor="mm")

# Status: In Corso
draw.rounded_rectangle([sx2-80, sy1+14, sx2-18, sy1+34], radius=5, fill=ACCENT_AMBER)
draw.text((sx2-49, sy1+24), "IN CORSO", fill=(26,29,39), font=font_entity, anchor="mm")

# Actions
for i, sym in enumerate(["⏸", "⋯"]):
    ax = sx2 - 18 - i * 34
    draw.rounded_rectangle([ax-26, sy1+42, ax, sy1+68], radius=6, outline=DIM, width=1)
    draw.text((ax-13, sy1+55), sym, fill=MUTED, font=font_label_sm, anchor="mm")

# Body
sb_y = s_cover_y2 + 4
draw.text((sx1+16, sb_y+8), "Serata Terraforming", fill=WHITE, font=font_zone, anchor="lt")
draw.text((sx1+16, sb_y+34), "Round 5/14 · 45min", fill=MUTED, font=font_label_sm, anchor="lt")
# Meta
draw.text((sx1+16, sb_y+56), "👥 4 giocatori", fill=DIM, font=font_label_sm, anchor="lt")
draw.text((sx1+130, sb_y+56), "🏆 Lead: Marco", fill=DIM, font=font_label_sm, anchor="lt")
# Tags
stag_y = sb_y + 82
for i, tag in enumerate(["Competitiva", "Casa"]):
    tw = len(tag) * 8 + 14
    stx = sx1 + 16 + i * (tw + 5)
    draw.rounded_rectangle([stx, stag_y, stx+tw, stag_y+20], radius=5, fill=(35, 38, 60))
    draw.text((stx+tw//2, stag_y+10), tag, fill=MUTED, font=font_label_sm, anchor="mm")

# Link footer
s_link_y = sy2 - 36
draw.line([(sx1+10, s_link_y-4), (sx2-10, s_link_y-4)], fill=(40, 44, 55), width=1)
s_link_icons = [
    (ACCENT_ORANGE, "🎲"),
    (ACCENT_PURPLE, "👤"),
    (ACCENT_ROSE, "📅"),
]
slx = sx1 + 22
for color, icon in s_link_icons:
    draw.rounded_rectangle([slx-8, s_link_y, slx+14, s_link_y+22], radius=5, outline=color, width=1)
    slx += 30

# Annotations
ann_sx = sx2 + 30
draw_annotation(sx2-49, sy1+24, sx1-60, sy1+24, "Status: In Corso/Pausa/Completa", side="left", color=ACCENT_AMBER)
draw_annotation(sx1+16, sb_y+56, sx1-60, sb_y+56, "Meta: giocatori, leader", side="left", color=DIM)
draw_annotation(slx-50, s_link_y+11, ann_sx+40, s_link_y+11, "Links: Game → Players → Event", color=ACCENT_INDIGO)


# =============================================
# CARD 4: SESSION CARD (Retro)
# =============================================
cx4 = cx3 + cw + 280
draw.text((cx4, cy3 - ch//2 - 70), "RETRO", fill=ACCENT_INDIGO, font=font_heading, anchor="mt")
draw.text((cx4, cy3 - ch//2 - 35), "Session Card · flip back", fill=DIM, font=font_label, anchor="mt")

rx1, ry1, rx2, ry2 = draw_card_outline(cx4, cy3, cw, ch, ACCENT_INDIGO, "BACK")

sec_y = ry1 + 16
back_sections = [
    ("CLASSIFICA", ACCENT_INDIGO, [
        "1. Marco — 87 pts",
        "2. Sara — 72 pts",
        "3. Luca — 65 pts",
        "4. Anna — 58 pts"
    ]),
    ("TIMELINE", ACCENT_AMBER, [
        "20:30  Inizio partita",
        "20:45  Round 3 completato",
        "21:10  Pausa 10min",
        "21:20  Ripresa"
    ]),
    ("AZIONI", ACCENT_GREEN, [
        "→ Aggiungi punteggio",
        "→ Snapshot stato",
        "→ Note sessione"
    ]),
]

for title, color, items in back_sections:
    draw.text((rx1+16, sec_y), title, fill=color, font=font_entity, anchor="lt")
    sec_y += 22
    draw.line([(rx1+16, sec_y), (rx2-16, sec_y)], fill=(40, 44, 55), width=1)
    sec_y += 8
    for item in items:
        draw.text((rx1+20, sec_y), item, fill=MUTED, font=font_label_sm, anchor="lt")
        sec_y += 20
    sec_y += 14

# Annotations
ann_rx = rx2 + 30
draw_annotation(rx1+60, ry1+26, ann_rx+40, ry1+26, "① Ranking live", color=ACCENT_INDIGO)
draw_annotation(rx1+60, ry1+150, ann_rx+40, ry1+150, "② Timeline eventi", color=ACCENT_AMBER)
draw_annotation(rx1+60, ry1+280, ann_rx+40, ry1+280, "③ Session Actions", color=ACCENT_GREEN)


# =============================================
# EXTRA DRAWER SCHEMATIC (right side)
# =============================================
dx_start = 1180
draw.text((dx_start + 200, 170), "EXTRA DRAWER", fill=ACCENT_TEAL, font=font_heading, anchor="mt")
draw.text((dx_start + 200, 205), "ExtraMeepleCardDrawer · right panel", fill=DIM, font=font_label, anchor="mt")

# Drawer outline
dw, dh = 380, 700
dx1, dy1 = dx_start + 10, 240
dx2, dy2 = dx1 + dw, dy1 + dh
draw.rounded_rectangle([dx1, dy1, dx2, dy2], radius=10, fill=(20, 23, 33), outline=(50, 55, 70), width=1)

# Header
draw.rounded_rectangle([dx1+1, dy1+1, dx2-1, dy1+48], radius=10, fill=(30, 35, 50))
draw.text((dx1+16, dy1+24), "📚", fill=ACCENT_TEAL, font=font_label, anchor="lm")
draw.text((dx1+40, dy1+24), "Terraforming Mars", fill=WHITE, font=font_zone, anchor="lm")
draw.text((dx2-20, dy1+24), "✕", fill=MUTED, font=font_label, anchor="rm")

# Tabs
tab_y = dy1 + 56
tabs = [("Overview", True), ("KB", False), ("Agent", False), ("Links", False)]
tab_x = dx1 + 10
for name, active in tabs:
    tw = len(name) * 9 + 16
    if active:
        draw.rounded_rectangle([tab_x, tab_y, tab_x+tw, tab_y+28], radius=6, fill=ACCENT_TEAL)
        draw.text((tab_x+tw//2, tab_y+14), name, fill=WHITE, font=font_label_sm, anchor="mm")
    else:
        draw.text((tab_x+tw//2, tab_y+14), name, fill=DIM, font=font_label_sm, anchor="mm")
    tab_x += tw + 8

# Content area
content_y = tab_y + 40
draw.line([(dx1+16, content_y), (dx2-16, content_y)], fill=(40, 44, 55), width=1)
content_y += 12

drawer_sections = [
    ("DETTAGLI GIOCO", [
        "Designer: Jacob Fryxelius",
        "Anno: 2016",
        "Complessita: 3.26/5",
        "Peso: Heavy",
    ]),
    ("KNOWLEDGE BASE", [
        "12 documenti indicizzati",
        "245 chunks totali",
        "Ultimo aggiornamento: 2h fa",
    ]),
    ("AGENT AI", [
        "Rules Expert · Active",
        "142 query processate",
        "Tempo medio: 1.2s",
    ]),
    ("ENTITY LINKS", [
        "🎯 3 sessioni collegate",
        "🃏 2 espansioni",
        "👤 8 giocatori",
    ]),
]

for title, items in drawer_sections:
    draw.text((dx1+20, content_y), title, fill=ACCENT_TEAL, font=font_entity, anchor="lt")
    content_y += 20
    for item in items:
        draw.text((dx1+24, content_y), item, fill=MUTED, font=font_label_sm, anchor="lt")
        content_y += 18
    content_y += 16

# Drawer annotations
draw_annotation(dx2, dy1+24, dx2+60, dy1+24, "Entity-colored header", color=ACCENT_TEAL)
draw_annotation(dx2, tab_y+14, dx2+60, tab_y+50, "Tab navigation", color=MUTED)
draw_annotation(dx2, dy1+160, dx2+60, dy1+120, "Scrollable content", color=DIM)

# =============================================
# ENTITY COLOR LEGEND (bottom)
# =============================================
legend_y = 2180
draw.text((W//2, legend_y), "ENTITY COLOR SYSTEM", fill=WHITE, font=font_heading, anchor="mt")

entities = [
    ("Game", ACCENT_ORANGE),
    ("Session", ACCENT_INDIGO),
    ("Player", ACCENT_PURPLE),
    ("Agent", ACCENT_AMBER),
    ("KB", ACCENT_TEAL),
    ("Toolkit", ACCENT_GREEN),
    ("Event", ACCENT_ROSE),
    ("Chat", ACCENT_BLUE),
]

cols = 4
col_w = (W - 200) // cols
for i, (name, color) in enumerate(entities):
    col = i % cols
    row = i // cols
    ex = 120 + col * col_w
    ey = legend_y + 45 + row * 40
    draw.rounded_rectangle([ex, ey, ex+20, ey+20], radius=4, fill=color)
    draw.text((ex+28, ey+10), name, fill=MUTED, font=font_label, anchor="lm")
    # HSL value
    draw.text((ex+120, ey+10), f"rgb{color}", fill=DIM, font=font_label_sm, anchor="lm")

# =============================================
# ZONE LEGEND
# =============================================
zone_y = legend_y + 140
draw.text((W//2, zone_y), "CARD ZONES", fill=WHITE, font=font_heading, anchor="mt")

zones = [
    "① Entity Badge — tipo entita (top-left)",
    "② Status Chip — stato corrente (top-right)",
    "③ Action Buttons — azioni rapide (top-right, hover)",
    "④ Tag Strip — etichette colorate (bordo sinistro)",
    "⑤ Cover Image — immagine 55% altezza",
    "⑥ Title + Subtitle — info principali",
    "⑦ Rating — valutazione stellare",
    "⑧ Tag Labels — genere, giocatori, durata",
    "⑨ Link Footer — navigazione entity correlate",
    "⑩ Info Button — apre Extra Drawer (hover)",
    "⑪ Flip Hint — indica retro disponibile (hover)",
    "⑫ Entity Accent Bar — bordo colorato sinistro",
]

cols = 2
for i, zone in enumerate(zones):
    col = i % cols
    row = i // cols
    zx = 120 + col * ((W-200)//2)
    zy = zone_y + 40 + row * 24
    draw.text((zx, zy), zone, fill=MUTED, font=font_label_sm, anchor="lt")


# =============================================
# SAVE
# =============================================
output_path = r"D:\Repositories\meepleai-monorepo-frontend\docs\frontend\meeple-card-anatomy.png"
img.save(output_path, "PNG", quality=95)
print(f"Saved to {output_path}")
print(f"Size: {W}x{H}")
