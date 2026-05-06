# Board Game PDF Copyright Analysis for MeepleAI RAG System

## Expert Spec Panel Review

**Panel**: Karl Wiegers (Requirements), Martin Fowler (Architecture), Michael Nygard (Operations/Risk), Lisa Crispin (Quality), + Legal Domain Experts
**Mode**: Discussion + Critique
**Focus**: Compliance, Architecture, Risk
**Date**: 2026-03-07

---

## Executive Summary

MeepleAI's use of board game rulebook PDFs in its RAG system raises significant copyright questions. The legal landscape is **actively evolving** with major cases (NYT v. OpenAI, Cohere v. Publishers, Thomson Reuters v. Ross) shaping precedent in 2025-2026. The analysis identifies **5 legal solutions** (not just 2):

### The 5 Legal Solutions

| # | Solution | Risk | Scalability | Cost |
|---|----------|------|------------|------|
| 1 | Publisher licensing (explicit consent) | **ZERO** | Medium (depends on deals) | High |
| 2 | User uploads own PDF (with warranty) | **LOW** | High (self-service) | Low |
| 3 | **Original rewrite of game mechanics** | **VERY LOW** | Medium (requires work) | Medium |
| 4 | CC / Open License content | **ZERO** | Low (few titles) | Low |
| 5 | Public domain games | **ZERO** | Low (classic games) | Low |

### Discarded Options (Too Risky)

| Option | Risk | Why |
|--------|------|-----|
| Platform scrapes publisher PDFs without consent | **HIGH** (criminal in Italy) | Art. 171(1)(a-ter) criminal penalties |
| Use BGG-hosted PDFs | **HIGH** (TOS violation) | BGG explicitly prohibits AI/LLM usage |
| EU Art. 4 TDM (no publisher opt-out) | **MEDIUM** (uncertain) | Must monitor each publisher; criminal risk |

**Critical finding**: Italy's new AI law (Law 132/2025, effective Oct 2025) adds **criminal penalties** for TDM violations where opt-out rights have been exercised, making compliance essential for an Italy-based service.

### Key Strategic Insight: Solution 3

**Game mechanics are NOT copyrightable** — only the specific textual expression is (Baker v. Selden, 1879). MeepleAI can legally create its own original descriptions of how games work:

```
Original rulebook (copyrighted):
"During the Resource Production phase, each player receives one resource card
for each settlement adjacent to the terrain hex matching the number rolled.
Cities produce two resources."

MeepleAI rewrite (legal):
"Quando si tirano i dadi, ogni giocatore prende risorse in base ai propri
insediamenti vicini al terreno corrispondente. Un insediamento da' 1 risorsa,
una citta' ne da' 2."
```

Same mechanics, original expression = **no copyright violation**. This creates a **proprietary knowledge base owned by MeepleAI** with zero dependency on publisher consent.

---

## Critical Question: Can We Download PDFs, Rewrite, and Feed to AI?

Three variants with very different legal profiles:

| Variant | Process | TDM? | Risk |
|---------|---------|------|------|
| **A: Fully automated** (AI reads PDF and rewrites) | Download PDF → LLM processes copyrighted text → Generates rewrite | **YES** (Art. 70-septies applies) | **MEDIUM-HIGH** — Even if output is original, input-side is regulated TDM |
| **B: Human reads and rewrites** (Solution 3) | Human plays game → Reads rulebook → Writes original explanation | **NO** (No AI processes copyrighted text) | **VERY LOW** — Same as Wikipedia describing game rules |
| **C: Human + AI assistant** (hybrid) | Human reads + takes notes → AI helps draft from human notes → Human reviews | **NO** (AI reads human notes, not PDF) | **LOW** — AI never sees the copyrighted text directly |

**Key distinction**: Italian law (Art. 70-septies) regulates "reproductions and extractions through AI models and systems." Variant A triggers this because the AI directly processes the copyrighted PDF. Variants B and C do not, because the AI only processes human-authored notes. **The legal boundary is: does the AI ever read the copyrighted text? If yes = TDM. If no = safe.**

> **NYGARD**: "Variant A is a trap. It looks efficient but creates the same legal exposure as direct RAG ingestion. The whole point of Solution 3 is that a HUMAN reads the rulebook and writes original content. If you automate that with AI reading the PDF, you lose the legal protection."

---

## Dual-Mode Architecture: Viewer vs. AI Knowledge Base

The platform must maintain a **strict separation** between PDF viewing (display only) and AI-powered Q&A (from the proprietary knowledge base).

### PDF Usage Rules

| PDF Usage Scenario | Legal? | Notes |
|-------------------|--------|-------|
| User views their own uploaded PDF | **YES** | Like any cloud storage (Google Drive, Dropbox) |
| Link to publisher's PDF on their site | **YES** | Hyperlink is not copying. "Rules at catan.com" |
| MeepleAI downloads and re-distributes publisher PDF to all users | **NO** | Unauthorized distribution = copyright infringement |
| Embed publisher PDF in iframe | **GRAY** | Depends on jurisdiction. Better to link. |
| User views PDF alongside AI answers (from proprietary KB) | **YES** | User sees their file + AI answers from separate KB |

### Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                    MeepleAI Platform                     │
│                                                         │
│  ┌─────────────────┐   ┌──────────────────────────────┐ │
│  │  PDF VIEWER      │   │  AI KNOWLEDGE BASE           │ │
│  │  (display only)  │   │  (answers questions)         │ │
│  │                  │   │                              │ │
│  │  • User's own    │   │  Tier 1B: Original rewrites  │ │
│  │    uploaded PDF   │   │    (by humans, proprietary)  │ │
│  │  • Link to       │   │                              │ │
│  │    publisher site │   │  Tier 1A: User upload chunks │ │
│  │                  │   │    (per-user, isolated)      │ │
│  │  NO AI processing│   │                              │ │
│  │  NO shared DB    │   │  Tier 2: Publisher licensed  │ │
│  │  NO redistribution│  │    (with explicit consent)   │ │
│  │                  │   │                              │ │
│  └─────────────────┘   │  Tier 3: CC + Public Domain  │ │
│                         └──────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

**The separation is the legal firewall.** The PDF viewer is a passive display tool (like Google Drive). The AI knowledge base uses only legally sourced content. The AI never reads PDFs from the viewer.

### Example User Workflow: Playing Catan

| Step | Without Upload | With Upload |
|------|---------------|-------------|
| User asks: "How do I trade?" | AI answers from proprietary rewrite (Tier 1B). Link: "See official rules at catan.com" | AI answers from rewrite + user's PDF chunks. User can view PDF in viewer. |
| User asks: "What about the robber?" | AI answers from MeepleAI's original explanation. No copyrighted text used. | AI combines proprietary rewrite + user's uploaded content for more precise answer. |
| Publisher partnership (future) | AI adds official FAQ, errata, designer notes from Catan Studio. | All three sources: rewrite + user PDF + official. |

---

## 1. Copyright Fundamentals: Game Rules vs. Rulebooks

### The Idea/Expression Dichotomy

**Game mechanics (ideas)** are NOT copyrightable:
- The rules of Catan (place settlements, trade resources) cannot be owned via copyright
- US precedent: *Baker v. Selden* (1879) — methods and systems are not protectable
- *Lotus v. Borland* (1995) — functional elements (menu hierarchies) not copyrightable

**Rulebook text (expression)** IS copyrightable:
- The specific words, illustrations, layout, examples, flavor text in a rulebook are protected
- The creative choices in HOW rules are explained constitute original expression
- Art, diagrams, graphic design, narrative elements — all protected

> **WIEGERS**: "This distinction is critical for requirements. The system must be designed around what it CAN legally use (game mechanics, factual information) vs. what carries copyright risk (specific textual expression). Every requirement must specify whether it operates on ideas or expression."

### What This Means for RAG

A RAG system that stores and retrieves **chunks of rulebook text** is reproducing copyrighted expression. This is fundamentally different from a system that merely "knows" game rules.

---

## 2. Legal Framework Analysis

### 2.1 US Copyright Law — Fair Use (17 U.S.C. Section 107)

The four-factor test applied to MeepleAI's RAG use:

| Factor | Assessment | Risk |
|--------|-----------|------|
| **1. Purpose & Character** | Commercial service; arguably transformative (Q&A vs. reading rulebook) | MEDIUM |
| **2. Nature of Work** | Rulebooks are factual/instructional (favors fair use) but contain creative expression | MEDIUM-LOW |
| **3. Amount Used** | RAG chunks = substantial portions of the work | HIGH |
| **4. Market Effect** | Could substitute for purchasing/reading the rulebook | HIGH |

**Key US Cases (2025-2026)**:

- **Thomson Reuters v. Ross Intelligence** (D. Del., Feb 2025): Court REJECTED fair use for AI system trained on Westlaw headnotes. First ruling against AI fair use defense. Ross copied 2,243 headnotes — held not transformative. **Directly relevant** to MeepleAI storing rulebook chunks.

- **Advanced Local Media v. Cohere** (S.D.N.Y., Nov 2025): Court denied dismissal of RAG-specific copyright claims. "Substitutive summaries" — non-verbatim outputs that mirror expressive structure — may infringe. **RAG technology specifically scrutinized**.

- **NYT v. OpenAI** (S.D.N.Y., ongoing): Main copyright claims proceeding. OpenAI ordered to produce 20M ChatGPT logs. Implications for any system that retrieves/reproduces copyrighted text.

- **Bartz v. Anthropic** (June 2025): First ruling finding AI training IS fair use — but ONLY for legally acquired works. Pirated content is NOT protected. Settled Aug 2025. Favorable for lawfully accessed content.

- **Perplexity AI lawsuits**: WSJ and NY Post sued Perplexity (RAG-based search). Allegations that RAG retrieval of news articles constitutes infringement.

- **US Copyright Office Part 3 Report** (May 2025): Explicitly states RAG "is less likely to be transformative" than training. Both copying into retrieval DB and outputting content are "potential copyright infringements which do not qualify as fair use." Critical guidance against unlicensed RAG.

> **NYGARD**: "The Thomson Reuters and Cohere rulings are red flags. Courts are NOT treating RAG as automatically fair use. The market substitution argument is strong — if MeepleAI answers rulebook questions, users may not need to read the actual rulebook, directly impacting the publisher's market."

### 2.2 EU Copyright Directive 2019/790 — TDM Exceptions

The EU framework is MORE structured than US fair use:

**Article 3 — Research Exception**:
- TDM permitted for research organizations and cultural heritage institutions
- For scientific research purposes only
- Requires lawful access to the works
- **MeepleAI cannot use this**: It's a commercial service, not a research institution

**Article 4 — General TDM Exception**:
- TDM permitted for ANY purpose (including commercial)
- **UNLESS** rights holders have "expressly reserved their rights in an appropriate manner"
- For online content: opt-out must be in "machine-readable means" (robots.txt, meta tags, TDM headers)
- **Hamburg Court (Dec 2025)**: Ruled that non-machine-readable opt-outs are INVALID

**Implications for MeepleAI**:
- If publisher PDFs are hosted online WITHOUT machine-readable opt-out → Article 4 MAY permit TDM
- If publisher has robots.txt/TDM reservation → CANNOT use under Article 4
- Must check each publisher's opt-out status individually
- "Lawful access" requirement: must access content legally (not through scraping behind paywalls)

**EU AI Act (Article 53, effective Aug 2025)**:
- AI providers must comply with EU copyright law
- Must "identify and respect" opt-out reservations under Article 4(3)
- Code of Best Practices published July 2025

### 2.3 Italian Copyright Law (Legge 633/1941 + Law 132/2025)

**CRITICAL for MeepleAI** (Italian-based service):

Italy adopted its national AI law (Law 132/2025) effective **October 10, 2025**, which AMENDS the Copyright Law:

**Article 70-ter**: TDM for research organizations — scientific research only, no opt-out needed
**Article 70-quater**: TDM for any purpose — BUT rights holders can opt-out ("expressly reserve rights")
**Article 70-septies** (NEW): Explicitly extends TDM rules to AI systems including generative ones:
> "Reproductions and extractions from works or materials contained in networks or databases to which lawful access is permitted, for the purposes of text and data mining using AI models and systems (including generative ones), are permitted in accordance with Articles 70-ter and 70-quater."

**Article 171(1)(a-ter)** (NEW): **CRIMINAL PENALTIES** for TDM violations through AI systems — unauthorized scraping and abusive data mining where opt-out rights have been exercised.

> **FOWLER**: "The Italian law is the most specific framework we're dealing with. Article 70-septies explicitly covers AI systems doing TDM. The criminal penalty provision (171(1)(a-ter)) elevates this from a civil liability concern to a criminal one. Architecture decisions must treat this as a hard constraint, not a soft preference."

**Summary of Italian framework**:
| Use Case | Permitted? | Condition |
|----------|-----------|-----------|
| TDM for scientific research | Yes | Must be research org |
| TDM for commercial AI (no opt-out) | Yes | Lawful access required |
| TDM for commercial AI (opt-out present) | **NO** | Criminal penalties apply |
| User processes their own purchased PDF | Likely yes | Personal use + lawful access |

---

## 3. Publisher Landscape Analysis

### 3.1 How Publishers Distribute Rulebook PDFs

| Publisher | Free PDF Available? | Where? | Notable Policy |
|-----------|-------------------|--------|----------------|
| **Catan Studio** | Yes | catan.com/game-rules | Free download, "for those who misplaced them" |
| **Fantasy Flight Games** (Asmodee) | Yes | images-cdn.fantasyflightgames.com | CDN-hosted, freely accessible |
| **Days of Wonder** (Asmodee) | Yes | Via BGG file uploads | Publisher-uploaded |
| **Stonemaier Games** | Yes | stonemaiergames.com | Known for openness |
| **Czech Games Edition** | Yes | Via BGG | Also uses AI validators internally |
| **Renegade Game Studios** | Yes | Via BGG | Uses internal AI on own errata |
| **CMON** | Mixed | Some via BGG | Varies by title |
| **Hasbro/WotC** | Selective | hasbro.com, OGL for D&D | D&D SRD 5.1 under CC-BY-4.0 (only major open-licensed content) |
| **Ravensburger** | Limited | Some titles only | Forced removal of AI art from licensed product (2024). Restrictive. |
| **Games Workshop** | Limited | Some titles | Comprehensive AI ban across all design (Jan 2026) |
| **Leder Games** | Yes | ledergames.com/resources | Free downloads available |

**Key observation**: Most publishers make rulebook PDFs freely available, but "freely downloadable" does NOT mean "freely usable in AI systems." The license to download is typically for personal reading, not for commercial processing.

**Industry anti-AI stance (2024-2026)**:
- **Stonemaier Games** (Apr 2024): "does not, has not, and will not use any form of AI to replace or augment creative work"
- **Ravensburger** (Mar 2024): Forced removal of AI art from licensed Puerto Rico product
- **Asmodee**: No AI art policy across all productions
- **Games Workshop** (Jan 2026): Comprehensive ban on AI across all design processes
- **Fantasy Flight Games**: IP policy explicitly bans their IP in "software applications of any kind"
- **Hasbro/WotC**: Mixed signals (WotC anti-AI for published work; CEO admits internal use)

**NOTE**: While these policies primarily target AI-generated art, no publisher has explicitly addressed third-party AI text mining of rulebooks for RAG/chatbot purposes. The prohibition is inferred from general copyright + personal-use restrictions.

**Only open-licensed content**: D&D SRD 5.1 (CC-BY-4.0) and content under ORC License. No major commercial board game publisher releases rulebooks under Creative Commons.

### 3.2 BoardGameGeek (BGG) Policies

BGG is the largest repository of board game rulebook PDFs. Their TOS (updated) states:

- **Explicitly prohibits** using the website "to train or otherwise use as data for an AI or LLM system"
- **Prohibits** automated scraping beyond what a human can produce
- **Prohibits** commercial use without written authorization
- Rulebooks uploaded to BGG are often uploaded by publishers themselves or with tacit consent

> **CRISPIN**: "BGG's explicit AI prohibition is a hard blocker. Any approach that involves scraping BGG for rulebook PDFs is clearly against their TOS. Even if PDFs were originally uploaded by publishers, BGG's TOS governs access through their platform."

### 3.3 Existing Competitors and Their Approaches

Several AI rulebook assistants already exist, with varying copyright approaches:

| Product | Approach | Copyright Strategy |
|---------|----------|-------------------|
| **RulesBot.ai** | Pre-indexed rulebooks | Unclear licensing model |
| **Ludomentor** (Awaken Realms) | Publisher's own games | First-party content (no copyright issue) |
| **Board Game Assistant** | Publisher partnerships | Licensed content |
| **Boardside** | "Official rulebooks only" | Claims to use only official sources |
| **Rulebook.gg** | AI drafting tool | Different use case (creation, not Q&A) |

> **FOWLER**: "The market is validating the use case, but the successful approaches either (a) use first-party content (Ludomentor/Awaken Realms doing their own games) or (b) establish publisher partnerships (Board Game Assistant). The 'just index everything' approach has obvious legal exposure."

---

## 4. The 5 Legal Solutions — Detailed Analysis

### Solution 1: Publisher Licensing (ZERO RISK)

Formal agreements with publishers granting MeepleAI the right to ingest and serve their rulebook content.

| Aspect | Details |
|--------|---------|
| **Legal basis** | Contractual license — explicit consent eliminates all copyright risk |
| **Model** | Revenue share, value exchange (analytics on rule confusion), or free partnership |
| **Precedent** | Board Game Assistant uses publisher partnerships; Ludomentor (Awaken Realms) uses first-party content |
| **Scalability** | Limited by business development speed; high value for top 50-100 games |

### Solution 2: User Uploads Own PDF (LOW RISK)

Users who own a board game upload their copy of the rulebook PDF. Per-user processing, per-user storage.

| Dimension | Analysis |
|-----------|----------|
| **US Fair Use** | Stronger. Personal/educational use, user already purchased the game, not redistributing. |
| **EU/Italian TDM** | User has lawful access (purchased game). Processing for personal use. |
| **Platform Liability** | DMCA Section 512(c) safe harbor may apply — user-directed storage. |
| **Analogy** | Google NotebookLM, Notion AI, ChatGPT file upload — widely accepted model. |
| **Market Effect** | Minimal — user already bought the game. RAG enhances their experience. |
| **Thin Copyright** | Rulebooks are factual/instructional — "thin copyright" with weaker protection. |

> **WIEGERS**: "Scenario B has well-established analogies: personal document AI assistants. Key requirements: (1) user uploads, (2) per-user isolation, (3) no cross-user sharing, (4) user warranty of rights, (5) DMCA compliance."

### Solution 3: Original Rewrite of Game Mechanics (VERY LOW RISK) ← KEY INSIGHT

**Game mechanics are NOT copyrightable** — only the specific textual expression is. MeepleAI can legally create its own original descriptions of how games work, using completely different words from the publisher's rulebook.

| Aspect | Details |
|--------|---------|
| **Legal basis** | Baker v. Selden (1879): methods/systems not copyrightable. Copyright protects expression, not ideas. |
| **Implementation** | Team (or AI + human review) writes original rule explanations. Describe mechanics without copying publisher text. |
| **Scalability** | Medium — requires editorial work per game. Prioritize top 50-100 popular games. |
| **Advantage** | Creates **PROPRIETARY knowledge base** owned by MeepleAI. No dependency on publisher consent or user uploads. |
| **Precedent** | Wikipedia describes game rules without copyright issues. BGG wiki pages summarize rules in original text. |
| **Risk** | Very low. Only risk: if rewrite is "substantially similar" to original (easily avoidable with review). |

> **FOWLER**: "This is the game-changer. Solution 3 creates a proprietary asset that MeepleAI owns. Combined with user uploads for the long tail and publisher deals for premium content, this gives you three independent content sources with zero legal dependency on any single one."

### Solution 4: Creative Commons / Open License (ZERO RISK)

| Content | License | Scope |
|---------|---------|-------|
| D&D SRD 5.1 | CC-BY-4.0 | Core D&D rules, monsters, spells — freely usable with attribution |
| ORC License games | ORC License | Pathfinder ecosystem, various RPG publishers |
| CC-licensed indie games | Various CC | Small but growing catalog |

### Solution 5: Public Domain Games (ZERO RISK)

Games whose copyright has expired (EU: 70 years after author death, US: pre-1929):
- Chess, Go, Backgammon, Checkers, Mancala and all classic abstract games
- Traditional card games (Poker, Bridge, Rummy, etc.)
- Classic party games with expired copyright

---

## 5. Expert Panel Synthesis

### 5.1 Convergent Insights (All Experts Agree)

1. **5 legal solutions exist** — NOT just 2 (licensing + user upload)
2. **Solution 3 (original rewrite) is the strategic differentiator** — creates proprietary IP
3. **BGG is off-limits** for any automated processing
4. **Italian criminal penalties** (Art. 171(1)(a-ter)) make compliance non-negotiable
5. **The legal landscape is actively hostile** to unlicensed commercial RAG (2025-2026 trend)
6. **A hybrid architecture combining multiple solutions** is stronger than any single approach

### 5.2 The Hybrid Strategy

> **FOWLER**: "The hybrid model is the winning strategy: original rewrites for the top 50 games (proprietary KB), publisher partnerships for premium content with errata/FAQ, user uploads for the long tail of 50,000+ games. Three independent content sources with zero legal dependency on any single one."

> **NYGARD**: "Solution 3 eliminates the single point of failure. If a publisher partnership falls through, or if a court rules against user-upload models, you still have your proprietary rewrites. That operational resilience is worth the editorial investment."

> **CRISPIN**: "The original rewrite approach also improves quality. Publisher rulebooks are often poorly written. MeepleAI could become known for BETTER rule explanations than the originals — that is a competitive advantage, not just a legal workaround."

### 5.3 Key Strategic Insight: Mechanics Are Free

The fundamental legal principle that unlocks MeepleAI's strategy: **game mechanics are ideas, and ideas cannot be copyrighted**. Only the specific words used to describe them are protected. This means MeepleAI can build a comprehensive knowledge base of how every game works, written in its own original language, without any copyright concern.

---

## 6. Recommended Architecture for MeepleAI

### Tier 1A: User-Uploaded Content (Launch MVP)

```
User uploads PDF → Per-user processing → Per-user vector store → Per-user Q&A
```

**Requirements**:
- [ ] User TOS: warranty of ownership/rights, indemnification clause
- [ ] Per-user content isolation (no cross-pollination between users)
- [ ] DMCA takedown procedure and designated agent
- [ ] Content retention policy (auto-delete after N days of inactivity)
- [ ] No caching of full text — store only embeddings + minimal chunks
- [ ] User can delete their content at any time

### Tier 1B: Original Mechanics Rewrite (Launch MVP — Top 50 Games)

```
Editorial team writes original rule descriptions → Shared knowledge base → All users
```

**Requirements**:
- [ ] Proprietary content owned by MeepleAI (no copyright dependency)
- [ ] Editorial process: play game → write rules in original language → peer review
- [ ] AI-assisted drafting with human review to ensure originality
- [ ] Similarity check against publisher text (must be substantially different)
- [ ] Priority: BGG Top 50 games + trending new releases
- [ ] Multi-language support (IT, EN, DE, FR, ES) for each game
- [ ] Community contribution model: users can suggest corrections/improvements

### Tier 2: Licensed Publisher Content (Phase 2 — 3-6 months)

```
Publisher API/agreement → Shared knowledge base → All users
```

**Requirements**:
- [ ] Formal licensing agreements with publishers
- [ ] Publisher content management dashboard
- [ ] Revenue sharing or value exchange model
- [ ] Attribution and source citation in responses
- [ ] Publisher analytics (most asked questions, rule confusion areas)
- [ ] Value proposition: MeepleAI improves game accessibility → more sales

### Tier 3: Open + Public Domain Content (Parallel Track)

```
CC-licensed / public domain games → Community knowledge base → All users
```

**Requirements**:
- [ ] D&D SRD 5.1 (CC-BY-4.0), ORC License content, CC-licensed indie games
- [ ] Public domain: Chess, Go, Backgammon, classic card games
- [ ] Community contribution model with license verification
- [ ] Good for onboarding and demonstrating platform value

---

## 7. TOS and Legal Safeguards

### Required TOS Clauses

1. **User Warranty**: "You represent and warrant that you have all necessary rights, licenses, and permissions to upload content to MeepleAI, including copyright ownership or a valid license."

2. **Indemnification**: "You agree to indemnify and hold harmless MeepleAI from any claims arising from your uploaded content, including copyright infringement claims."

3. **DMCA Compliance**: "MeepleAI respects intellectual property rights. If you believe content on our platform infringes your copyright, please contact our designated DMCA agent at [email]."

4. **No Redistribution**: "Content you upload is processed solely for your personal use. MeepleAI does not share, redistribute, or make your uploaded content available to other users."

5. **Data Retention**: "Uploaded content and derived data (embeddings, chunks) are retained for [N] days and automatically deleted upon account closure or inactivity."

6. **AI Disclaimer**: "MeepleAI provides AI-assisted responses based on uploaded content. Responses may contain errors. Always refer to the official rulebook for definitive rules."

### Technical Safeguards

1. **Per-user vector namespaces** in Qdrant — strict tenant isolation
2. **No full-text storage** — only embeddings + minimal context chunks
3. **Chunk size limits** — small enough to be non-substitutive (e.g., 200-500 tokens)
4. **Response attribution** — always cite "Based on your uploaded rulebook, page X"
5. **Rate limiting** on uploads — prevent bulk processing
6. **File type validation** — accept only PDF, reject bulk archives
7. **robots.txt compliance** — if crawling publisher sites (Tier 2 only, with agreement)

---

## 8. Risk Matrix

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Publisher DMCA takedown (Tier 1) | Low (user content) | Medium | DMCA compliance, user warranty |
| Criminal liability Italy (Art. 171) | Very Low (user uploads) | Very High | No platform-side scraping without license |
| User uploads pirated content | Medium | Low-Medium | TOS warranty, DMCA process |
| Competitor with publisher deals outpaces us | High | Medium | Pursue publisher partnerships (Tier 2) |
| EU TDM opt-out landscape changes | Medium | Medium | Architecture supports per-publisher config |
| US court ruling against RAG fair use | Medium-High | High | User-upload model is more defensible |

---

## 9. Action Items

### Immediate (Pre-Launch)

1. **Legal**: Engage Italian IP attorney for compliance with Law 132/2025
2. **Architecture**: Implement per-user content isolation in Qdrant
3. **TOS**: Draft user warranty, indemnification, DMCA procedures
4. **Technical**: Minimize stored text (embeddings-first, minimal chunks)
5. **Editorial**: Begin original rewrite of top 10 game mechanics (Catan, Ticket to Ride, Carcassonne, Pandemic, Azul, 7 Wonders, Wingspan, Codenames, Splendor, Dixit)
6. **Content**: Ingest D&D SRD 5.1 (CC-BY-4.0) + public domain games as launch KB

### Short-Term (1-3 months post-launch)

7. **Editorial**: Expand original rewrites to top 50 games (BGG ranking)
8. **Business**: Approach 3-5 friendly publishers for pilot partnerships
9. **Community**: Launch community contribution model for rewrite suggestions
10. **Compliance**: Register DMCA designated agent with US Copyright Office
11. **QA**: Implement automated similarity checker (original vs publisher text)

### Medium-Term (3-12 months)

12. **Business model**: Develop publisher value proposition (rule confusion analytics, FAQ)
13. **Scale licensing**: Expand publisher partnerships based on pilot results
14. **Scale rewrites**: Top 200 games with community + AI-assisted editorial pipeline
15. **Multi-language**: Translate original rewrites to IT, EN, DE, FR, ES
16. **Legal audit**: Quarterly review of TDM opt-out landscape and case law

---

## Sources

### Legal Cases
- [Thomson Reuters v. Ross Intelligence (Feb 2025)](https://www.dwt.com/blogs/artificial-intelligence-law-advisor/2025/02/reuters-ross-court-ruling-ai-copyright-fair-use)
- [Advanced Local Media v. Cohere (Nov 2025)](https://copyrightlately.com/court-rules-ai-news-summaries-may-infringe-copyright/)
- [NYT v. OpenAI (ongoing)](https://www.npr.org/2025/03/26/nx-s1-5288157/new-york-times-openai-copyright-case-goes-forward)
- [Hamburg Court TDM Opt-Out Ruling (Dec 2025)](https://www.insidetechlaw.com/blog/2025/12/machine-readable-opt-outs-and-ai-training-hamburg-court-clarifies-copyright-exceptions)

### EU/Italian Law
- [Italy AI Law - Cleary Gottlieb](https://www.clearygottlieb.com/news-and-insights/publication-listing/italy-adopts-the-first-national-ai-law-in-europe-complementing-the-eu-ai-act)
- [Italian TDM Implementation - COMMUNIA](https://communia-association.org/2022/12/14/italian-implementation-of-the-new-eu-tdm-exceptions/)
- [EU AI Act Copyright Compliance - IAPP](https://iapp.org/news/a/the-eu-ai-copyright-playbook-the-tdm-exception-and-ai-act-s-transparency-requirements)
- [EU TDM Opt-Out Problems - Kluwer](https://legalblogs.wolterskluwer.com/copyright-blog/the-tdm-opt-out-in-the-eu-five-problems-one-solution/)

### Board Game Copyright
- [Board Games and Copyright - ABA Landslide](https://www.americanbar.org/groups/intellectual_property_law/resources/landslide/archive/not-playing-around-board-games-intellectual-property-law/)
- [Board Game IP Guide - Meeple Mountain](https://www.meeplemountain.com/articles/the-board-game-designers-guide-to-intellectual-property-law/)
- [Are Board Games Copyrighted - Legal Moves](https://legalmoveslawfirm.com/board-games-copyrighted/)

### RAG Copyright Analysis
- [RAG Copyright Concerns - 36kr](https://eu.36kr.com/en/p/3422429684387205)
- [RAG Copyright Analysis - Asia IP Law](https://www.asiaiplaw.com/section/in-depth/the-latest-rage-called-rag)
- [RAG as Copyright Frontier - LinkedIn](https://www.linkedin.com/pulse/rag-new-frontier-copyright-battle-genai-kurt-sutter-dqlfe)
- [Fair Use in RAG - arXiv](https://arxiv.org/html/2505.02164v1)

### Platform Policies
- [BGG Terms of Service](https://boardgamegeek.com/terms)
- [DMCA Section 512 Safe Harbors](https://www.copyright.gov/512/)
- [DMCA Safe Harbors for AI - Oxford Academic](https://academic.oup.com/jiplp/article/20/9/605/8221820)

### Existing Products
- [RulesBot.ai](https://www.rulesbot.ai/)
- [Ludomentor (Awaken Realms)](https://play.google.com/store/apps/details?id=com.awakenrealms.ludomentor)
- [Board Game Assistant](https://www.boardgameassistant.ai/)
- [Boardside](https://boardgamegeek.com/thread/3631492/boardside-ai-app-for-board-game-rules)
