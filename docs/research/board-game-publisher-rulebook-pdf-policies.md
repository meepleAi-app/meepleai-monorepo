# Board Game Publisher Rulebook PDF Distribution & Third-Party Usage Policies

**Research Date**: 2026-03-07
**Confidence Level**: High (75-85%) for publisher PDF policies; Moderate (60-70%) for TOS specifics on AI/text mining
**Methodology**: Web search across publisher websites, BGG forums, industry news, legal analysis

---

## Executive Summary

Most major board game publishers freely distribute rulebook PDFs on their websites and/or BoardGameGeek (BGG). However, these are provided for **personal use only**, and no major publisher explicitly authorizes third-party AI/LLM training on their content. The board game industry has taken a strongly **anti-generative-AI** stance since 2024, with publishers like Stonemaier Games, Ravensburger, Asmodee, and Games Workshop issuing explicit bans on AI in their creative processes. BGG's Terms of Service **explicitly prohibit** using their platform to train AI/LLM systems. Open licensing (OGL, Creative Commons, ORC) exists primarily in the tabletop RPG space, not mainstream board games.

---

## 1. Publisher PDF Distribution Practices

### Publisher-by-Publisher Analysis

| Publisher | Free PDFs? | Where Hosted | Notes |
|-----------|-----------|--------------|-------|
| **Hasbro/WotC** | Yes (selective) | wizards.com, media.wizards.com | D&D Basic Rules free; SRD 5.1 under CC-BY-4.0 |
| **Asmodee Group** | Yes | fantasyflightgames.com (Product Document Archive) | FFG maintains extensive archive; Catan Studio hosts on catan.com |
| **Fantasy Flight Games** | Yes | images-cdn.fantasyflightgames.com | Comprehensive Product Document Archive with multilingual PDFs |
| **Catan Studio** | Yes | catan.com/understand-catan/game-rules | Official rulebooks freely downloadable |
| **Ravensburger** | Limited | Individual game pages | Less centralized than other publishers |
| **CMON** | Yes | cmon.com, cmon-files.s3.amazonaws.com | PDFs hosted on AWS S3; available for Zombicide, Marvel United, etc. |
| **Stonemaier Games** | Yes | stonemaiergames.com | Each game has rules page with PDFs, FAQs, quick-reference guides |
| **Czech Games Edition** | Yes | czechgames.com/en/downloads/ | Dedicated downloads page |
| **Devir** | Limited | devirgames.com, BGG | Less centralized; some via BGG community uploads |
| **Restoration Games** | Yes | restorationgames.com (direct PDF links) | PDFs hosted in wp-content directory |
| **Leder Games** | Yes | ledergames.com/pages/resources | Comprehensive resources page with rulebooks, errata, learn-to-play guides |
| **Rio Grande Games** | Limited | Primarily via BGG | Less direct hosting than other publishers |

### Key Observations

1. **Industry standard**: Providing free rulebook PDFs has become the norm. Publishers view it as customer service (replacing lost rulebooks) and marketing (preview before purchase).

2. **Distribution channels**: Publishers typically use their own CDN/website, plus allow uploads to BGG. Some rely on BGG as a secondary/primary distribution channel.

3. **Third-party aggregators**: Sites like cdn.1j1ju.com (1jour-1jeu.com) host many rulebook PDFs, though their authorization status varies.

4. **Emerging alternatives**: Rulepop (rulepop.com) offers interactive, always-updated rule references as an alternative to static PDFs. Stonemaier Games' Vantage uses this platform.

---

## 2. Terms of Service Analysis

### 2.1 Fantasy Flight Games / Asmodee — Community IP Policy

The most explicit policy comes from FFG/Asmodee's "Guidelines for Community Use of Our Intellectual Property":

**Allowed**:
- Fan-dedicated web pages
- Homebrew scenarios and special rulesets
- Custom accessories for personal use
- Artwork, fan fiction
- Must label as "unofficial" or "fan-made"
- Must include proper copyright notices

**Prohibited**:
- Selling fan creations using their IP
- Creating online versions of games (Tabletop Simulator, Vassal)
- Digitalized card game versions for download
- 3D print files of miniatures
- **Software applications of any kind** (for licensing/business reasons)

**Key restriction**: "FFG cannot allow their intellectual property in software applications of any kind for licensing and business reasons." This would likely encompass AI applications that ingest their rulebook content.

**Source**: [FFG IP Policy PDF](https://images-cdn.fantasyflightgames.com/filer_public/fa/b1/fab15a15-94a6-404c-ab86-6a3b0e77a7a0/ip_policy_031419_final_v21.pdf)

### 2.2 Stonemaier Games

- Rulebooks provided for **personal use only** (stated on rules pages).
- Explicit anti-AI statement: "Generative AI? Not for Us!" blog post (April 2024).
- Policy: "does not, has not, and will not use any form of AI to replace or augment creative work."
- No specific TOS language about text mining, but the personal-use restriction and anti-AI stance signal opposition.

**Source**: [Stonemaier AI Policy](https://stonemaiergames.com/generative-ai-not-for-us/)

### 2.3 Hasbro / Wizards of the Coast

- D&D content has the most permissive licensing via Creative Commons (SRD 5.1 under CC-BY-4.0).
- However, Hasbro's general content remains under standard copyright.
- WotC's AI policy for products: "artists, writers, and creatives contributing to D&D TTRPG must refrain from using AI generative tools to create final D&D products."
- Conflicting signals: Hasbro CEO stated the company has "already been using AI" internally, while WotC FAQ reaffirms anti-AI stance for published products.
- No specific TOS language found addressing third-party AI training on their rulebook PDFs.

### 2.4 Ravensburger

- Enforces anti-AI policy through licensing agreements (forced Awaken Realms to pull AI art from Puerto Rico 1897 campaign).
- Stated position: "generative AI cannot be used in any part of the art process."
- General Terms of Use do not specifically address AI/text mining in public-facing documents.

### 2.5 Asmodee Group

- Confirmed policy: "not to use AI art in any of its own productions."
- Terms of Service reference French LCEN law and EU Digital Services Act.
- No specific publicly available clause found regarding third-party AI training on downloaded content.
- General copyright protections apply to all content.

### 2.6 Games Workshop (Warhammer — included for industry context)

- January 2026: CEO Kevin Rountree announced comprehensive AI ban.
- "Does not allow AI-generated content or AI to be used in their design processes or its unauthorised use outside of GW."
- Explicitly bans AI use "including in any of their competitions."
- Investing in hiring more human creatives rather than AI.

---

## 3. BoardGameGeek (BGG) Policies

### 3.1 Terms of Service — AI Prohibition

BGG's Terms of Service contain an **explicit prohibition**:

> "You agree not to use the Geek Websites to train or otherwise use as data for an AI (Artificial Intelligence) or Large Language Model (LLM) system."

This is unambiguous and applies to all content on the platform, including uploaded rulebook PDFs.

**Source**: [BGG Terms of Service](https://boardgamegeek.com/terms)

### 3.2 File Upload & Copyright

- BGG's file upload policy states users "shall not upload User Submissions containing material that is copyrighted unless they are the owner of such rights or have permissions."
- In practice, publishers upload or authorize uploads of their rulebook PDFs to BGG.
- Community discussion indicates a gray area: some uploads are publisher-authorized, others are community-contributed.

**Source**: [BGG File Upload Discussion](https://boardgamegeek.com/thread/1349333/file-upload-copyright-bgg-terms-of-service)

### 3.3 XML API Terms

- Registration required for API access.
- **Scraping is prohibited**.
- Commercial use requires a separate commercial license from BGG.
- API provides game metadata but NOT file/PDF downloads.

**Source**: [BGG XML API Terms](https://boardgamegeek.com/wiki/page/XML_API_Terms_of_Use) | [BGG XML API Commercial Use](https://boardgamegeek.com/wiki/page/BGG_XML_API_Commercial_Use)

### 3.4 Community Rules on AI

BGG Community Rules include a section on "AI generated content," indicating platform-level governance of AI usage beyond just the TOS.

---

## 4. Open Gaming Licenses & Creative Commons

### 4.1 D&D System Reference Document (SRD 5.1) — CC-BY-4.0

The most significant open license in tabletop gaming:
- January 2023: WotC released SRD 5.1 under **irrevocable Creative Commons Attribution 4.0 (CC-BY-4.0)**.
- April 2025: SRD 5.2 also released under Creative Commons.
- Allows: sharing, adapting, commercial use — with attribution.
- This is specifically for the **rules mechanics** and system reference, NOT for full published books, artwork, or Product Identity.

### 4.2 Open Game License (OGL 1.0a)

- Original license from 2000, enables use of D&D-derived game mechanics.
- Distinguishes between "Open Game Content" (freely usable) and "Product Identity" (restricted).
- Hundreds of games published under OGL.
- After the 2023 controversy (WotC attempted OGL 1.1 revision), the original OGL 1.0a remains valid.

### 4.3 Open RPG Creative License (ORC)

- Developed by Paizo with 1,500+ publisher support, drafted by Azora Law.
- System-agnostic, perpetual, irrevocable open gaming license.
- Owned by a neutral legal entity (planned transfer to a nonprofit like Linux Foundation).
- Supported by: Paizo, Kobold Press, Chaosium, Green Ronin, Legendary Games, Rogue Genius Games.
- Final text submitted to Library of Congress.

**Source**: [ORC License](https://paizo.com/orclicense)

### 4.4 Creative Commons Board Games

- A GeekList on BGG catalogs [Creative Commons/Open Source Games](https://boardgamegeek.com/geeklist/33151/creative-commonsopen-source-games).
- Most CC-licensed games are indie/community projects (e.g., Sovereign under CC-BY-SA).
- FATE RPG system is under Creative Commons.
- **No major commercial board game publisher** (Asmodee, Ravensburger, CMON, etc.) releases their board game rulebooks under CC or open licenses.

### 4.5 Key Distinction: RPGs vs. Board Games

Open licensing is primarily an **RPG phenomenon**. Board games have fundamentally different IP structures:
- Board game **mechanics** are generally not copyrightable (per US law).
- Board game **rulebook text, artwork, and creative expression** ARE copyrighted.
- There is no board game equivalent of the OGL/ORC/CC-SRD ecosystem.

---

## 5. Industry Trends 2024-2026: AI and Board Game Content

### 5.1 Publisher Anti-AI Statements (Timeline)

| Date | Publisher | Action |
|------|-----------|--------|
| Mar 2024 | **Ravensburger** | Forced Awaken Realms to pull AI art from Puerto Rico 1897 |
| Apr 2024 | **Stonemaier Games** | Published "Generative AI? Not for Us!" blog post |
| 2024 | **Asmodee** | Confirmed no AI art policy for all productions |
| Summer 2024 | **Spiel Essen** | Stopped using AI images for marketing |
| Nov 2024 | **Wise Wizard Games** | Defended AI art use (Star Realms) — faced backlash |
| Jan 2026 | **Games Workshop** | CEO announced comprehensive AI ban across all processes |
| Ongoing | **WotC/Hasbro** | Contradictory: WotC anti-AI for published work; Hasbro CEO admits internal AI use |

### 5.2 AI Rulebook Assistant Apps

Several AI-powered rulebook assistants have emerged, raising legal and accuracy concerns:

- **RulesBot.ai**: AI chatbot for board game rules queries.
- **Boardside**: Claims to use ONLY official rulebooks, errata, and FAQs.
- **BoardGameAssistant.ai**: General AI board game assistant.
- **NotebookLM**: Google's tool being used by BGG community members for rulebook Q&A.

**Legal gray area**: These apps ingest copyrighted rulebook PDFs to build their knowledge bases. No publisher has explicitly authorized this use, and BGG's TOS would prohibit sourcing content from their platform for this purpose.

**Accuracy concern**: A February 2024 incident at a Root tournament in Portland highlighted risks — AI-paraphrased rules caused competitive disputes by subtly altering rule semantics.

### 5.3 Broader AI-Copyright Litigation Context

- 70+ copyright infringement lawsuits filed against AI companies as of 2025-2026.
- No board-game-specific lawsuits found, but the broader legal landscape is relevant.
- The EU Digital Services Act and French LCEN (referenced in Asmodee's TOS) may provide additional protections.
- US Copyright Office issued reports on AI and copyright in 2025.

### 5.4 Academic Research

- "Boardwalk: Towards a Framework for Creating Board Games with LLMs" (2025, arxiv.org) — academic paper on using LLMs for board game design, indicating growing research interest.

---

## 6. Implications for MeepleAI

### Risk Assessment

| Activity | Risk Level | Rationale |
|----------|-----------|-----------|
| Linking to publisher-hosted PDFs | LOW | Standard web linking; no content ingestion |
| Downloading PDFs for user's personal reference | LOW | Consistent with publisher intent |
| Ingesting PDFs into RAG/vector DB for AI assistant | HIGH | No publisher authorization; likely violates copyright |
| Scraping BGG for rulebook PDFs | VERY HIGH | Explicitly prohibited by BGG TOS |
| Using CC-licensed content (D&D SRD) | LOW | Explicitly permitted under CC-BY-4.0 with attribution |
| User-uploaded PDFs for personal AI assistant | MODERATE | User's personal use; platform liability questions |

### Recommended Approach

1. **User-uploaded content model**: Let users upload their own rulebook PDFs (which they obtained legitimately) for personal AI-assisted rules lookup. This follows the personal-use paradigm that publishers intend.

2. **Do NOT scrape or bulk-download** from BGG or publisher sites.

3. **Seek publisher partnerships** for authorized content ingestion — some publishers (via Rulepop/Boardside partnerships) are already exploring this space.

4. **Leverage CC-licensed content** (D&D SRD 5.1/5.2) as a demonstration use case where licensing is clear.

5. **Clear user attribution** and proper copyright notices per FFG/Asmodee IP policies.

6. **Monitor the legal landscape** — the AI-copyright space is evolving rapidly (2024-2026).

---

## 7. Key Gaps and Uncertainties

- **Specific TOS language on text mining**: Most publishers' TOS do not explicitly address AI/text mining. The prohibition is inferred from general copyright protection and anti-AI statements.
- **EU Text and Data Mining exception**: The EU DSA/Copyright Directive includes a text and data mining exception for research purposes; commercial applicability is debated.
- **Publisher partnership willingness**: No data found on whether publishers would license content for AI assistant use cases (as opposed to generative AI art).
- **Evolving policies**: The space is moving fast. Policies from 2024 may already be outdated.

---

## Sources

### Publisher Websites & Policies
- [Leder Games Resources](https://ledergames.com/pages/resources)
- [FFG Product Document Archive](https://www.fantasyflightgames.com/en/more/product-document-archive/)
- [FFG IP Policy (PDF)](https://images-cdn.fantasyflightgames.com/filer_public/fa/b1/fab15a15-94a6-404c-ab86-6a3b0e77a7a0/ip_policy_031419_final_v21.pdf)
- [Catan Game Rules](https://www.catan.com/understand-catan/game-rules)
- [Stonemaier Games Scythe Rules](https://stonemaiergames.com/games/scythe/rules-and-print-play/)
- [Czech Games Edition Downloads](https://czechgames.com/en/downloads/)
- [Devir Games](https://devirgames.com/)
- [Stonemaier "Generative AI? Not for Us!"](https://stonemaiergames.com/generative-ai-not-for-us/)

### BoardGameGeek
- [BGG Terms of Service](https://boardgamegeek.com/terms)
- [BGG Community Rules](https://boardgamegeek.com/community_rules)
- [BGG XML API Terms of Use](https://boardgamegeek.com/wiki/page/XML_API_Terms_of_Use)
- [BGG XML API Commercial Use](https://boardgamegeek.com/wiki/page/BGG_XML_API_Commercial_Use)
- [BGG File Upload / Copyright Discussion](https://boardgamegeek.com/thread/1349333/file-upload-copyright-bgg-terms-of-service)
- [BGG Legality of Posting Rulebooks](https://boardgamegeek.com/thread/3031595/legality-of-posting-a-rulebook)

### Open Licensing
- [ORC License (Paizo)](https://paizo.com/orclicense)
- [Open Game License (Wikipedia)](https://en.wikipedia.org/wiki/Open_Game_License)
- [WotC OGL/CC Announcement](https://www.enworld.org/threads/wotc-backs-down-original-ogl-to-be-left-untouched-whole-5e-rules-released-as-creative-commons.694850/)
- [Creative Commons / Open Source Games (BGG GeekList)](https://boardgamegeek.com/geeklist/33151/creative-commonsopen-source-games)
- [Copyright, Trademark, and OGL (Meeple Mountain)](https://www.meeplemountain.com/articles/copyright-trademark-and-open-game-licenses/)

### Industry News & AI Policy
- [Games Workshop Bans AI (Board Game Wire)](https://boardgamewire.com/index.php/2026/01/13/games-workshop-bans-ai-use-in-its-designs-celebrates-record-half-year-results/)
- [Ravensburger/Puerto Rico AI Art Incident](https://boardgamewire.com/index.php/2024/03/02/awaken-realms-pulls-ai-art-from-deluxe-puerto-rico-kickstarter-after-ravensburger-steps-in/)
- [Stonemaier Anti-AI (Board Game Wire)](https://boardgamewire.com/index.php/2024/04/24/no-ai-art-wingspan-scythe-maker-stonemaier-games-draws-hard-line-on-using-ai-in-creative-work/)
- [WotC AI Policy Controversy (GeekWire)](https://www.geekwire.com/2024/wizards-of-the-coast-will-adjust-generative-ai-policy-for-magic-following-controversy/)
- [Hasbro CEO AI Confirmation (Game Rant)](https://gamerant.com/hasbro-ceo-ai-use-confirmation-dungeons-and-dragons-magic-the-gathering/)
- [WotC Generative AI Art FAQ](https://dnd-support.wizards.com/hc/en-us/articles/26243094975252-Generative-AI-art-FAQ)
- [Board Games Copyright 2026 Legal Guide](https://legalmoveslawfirm.com/board-games-copyrighted/)
- [AI Copyright Lawsuit Developments 2025](https://copyrightalliance.org/ai-copyright-lawsuit-developments-2025/)

### AI Rulebook Apps
- [RulesBot.ai](https://www.rulesbot.ai/)
- [Boardside (BGG Thread)](https://boardgamegeek.com/thread/3631492/boardside-ai-app-for-board-game-rules)
- [BoardGameAssistant.ai](https://www.boardgameassistant.ai/)
- [AI Tool for Rulebooks (BGG Thread)](https://boardgamegeek.com/thread/3346052/ai-tool-to-readlearnrefresh-rulebooks)
