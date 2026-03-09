# Copyright Analysis: Board Game Rulebooks in RAG Systems

**Date**: 2026-03-07
**Scope**: US, EU, and Italian copyright law as applied to processing board game rulebook PDFs in a Retrieval Augmented Generation (RAG) system
**Confidence Level**: High (based on published case law, statutory text, and authoritative guidance from the U.S. Copyright Office, EU Directive text, and Italian Law No. 132/2025)
**Disclaimer**: This is a legal research summary, not legal advice. Consult qualified legal counsel for specific situations.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Copyright Fundamentals: Game Rules vs. Rulebooks](#2-copyright-fundamentals-game-rules-vs-rulebooks)
3. [US Copyright Law](#3-us-copyright-law)
4. [EU Copyright Law](#4-eu-copyright-law)
5. [Italian Copyright Law](#5-italian-copyright-law)
6. [Fair Use / Fair Dealing Analysis for RAG](#6-fair-use--fair-dealing-analysis-for-rag)
7. [Key Legal Risks by Scenario](#7-key-legal-risks-by-scenario)
8. [Risk Mitigation Strategies](#8-risk-mitigation-strategies)
9. [Conclusions and Recommendations](#9-conclusions-and-recommendations)
10. [Sources](#10-sources)

---

## 1. Executive Summary

The legal landscape for using board game rulebook PDFs in a RAG system is nuanced and jurisdiction-dependent. The core distinction is:

- **Game mechanics/rules** (the systems and processes): **NOT copyrightable** in any jurisdiction studied
- **Rulebook text** (the specific written expression, layout, illustrations, flavor text): **IS copyrightable**

This means that while anyone can freely describe how to play a game in their own words, the specific text of a publisher's rulebook is protected expression. Using that protected text in a RAG system involves reproduction and potentially derivative work creation, triggering copyright analysis.

**Key findings by jurisdiction**:

| Jurisdiction | Framework | RAG Risk Level | Key Exception |
|---|---|---|---|
| **US** | Fair use (case-by-case) | MODERATE-HIGH | Transformative use defense, but US Copyright Office skeptical of RAG |
| **EU** | TDM exceptions (Arts. 3-4, Dir. 2019/790) | LOW-MODERATE | Article 4 permits commercial TDM unless rightholder opts out |
| **Italy** | Art. 70-septies (L. 633/1941, as amended) | LOW-MODERATE | Follows EU TDM framework; criminal penalties for violations |

**Bottom line for MeepleAI**: A RAG system where users upload their own PDFs for personal Q&A assistance carries the lowest legal risk. A system that centrally downloads and processes publisher PDFs for all users carries the highest risk and requires careful compliance with TDM opt-out mechanisms (EU/Italy) or a strong fair use argument (US).

---

## 2. Copyright Fundamentals: Game Rules vs. Rulebooks

### 2.1 The Idea-Expression Dichotomy

Copyright law across all studied jurisdictions draws a fundamental line between **ideas** (unprotectable) and **expression** (protectable). For board games:

**NOT copyrightable** (ideas, systems, methods of operation):
- Game mechanics (turn structure, victory conditions, resource management systems)
- Rules as abstract concepts ("roll dice, move that many spaces")
- Mathematical formulas and scoring systems
- Game strategies and optimal play patterns

**IS copyrightable** (expression):
- The specific written text of a rulebook (word choice, sentence structure, explanatory prose)
- Illustrations, diagrams, and graphic design of the rulebook
- Flavor text, thematic narrative, and world-building content
- Layout and typographic arrangement (in some jurisdictions)
- Example scenarios and walkthroughs (as literary expression)

This distinction is codified in 17 U.S.C. Section 102(b):

> "In no case does copyright protection for an original work of authorship extend to any idea, procedure, process, system, method of operation, concept, principle, or discovery, regardless of the form in which it is described, explained, illustrated, or embodied in such work."

### 2.2 Practical Implications

A board game rulebook is a mixed work. When a RAG system chunks and embeds a rulebook PDF, it captures both:
- Unprotectable mechanical descriptions (the rules themselves)
- Protectable expression (how those rules are explained, the creative choices in presentation)

The legal risk is concentrated in the **reproduction and storage of the expressive elements**, not the mechanical content.

---

## 3. US Copyright Law

### 3.1 Key Case Law

#### Baker v. Selden, 101 U.S. 99 (1879)

The foundational case establishing that copyright in a book describing a system (bookkeeping) does not grant exclusive rights to the system itself. The Supreme Court held that the **description** of a method is copyrightable, but the **method itself** requires patent protection. This case is directly analogous to game rulebooks: the text describing how to play is protectable expression, but the gameplay system is not.

**Relevance to MeepleAI**: A RAG system can freely answer "how do you play Catan?" by synthesizing game mechanics. It cannot reproduce substantial portions of the Catan rulebook verbatim.

#### Lotus Development Corp. v. Borland International, Inc., 49 F.3d 807 (1st Cir. 1995)

Extended Baker v. Selden to hold that a software menu command hierarchy is an uncopyrightable "method of operation." Affirmed by an equally divided Supreme Court (no precedential value nationally, but influential). The First Circuit held that functional elements serving as the means to operate something are excluded from copyright protection.

**Relevance to MeepleAI**: Reinforces that game mechanics, as the "method of operation" of a game, are not copyrightable. The specific way those mechanics are presented in a rulebook remains protected.

#### ABA Article: "It's How You Play the Game" (2024)

A 2024 American Bar Association article specifically argues that video game rules are not expression protected by copyright law, extending the Baker/Lotus line of reasoning to the gaming context. Courts analyzing copyright infringement of games must filter out similarities between rules and mechanics, giving them no weight in the infringement analysis.

### 3.2 US Copyright Office AI Reports (2024-2025)

The US Copyright Office released a three-part study on AI and copyright:

- **Part 1** (July 2024): Digital replicas/deepfakes
- **Part 2** (January 2025): Copyrightability of AI-generated outputs
- **Part 3** (May 2025): Generative AI training and fair use

**Part 3 is directly relevant to RAG systems.** Key findings:

1. **RAG involves reproduction**: The Office found that RAG operates in two steps: (a) copying source materials into a retrieval database, and (b) outputting retrieved content in response to queries. Both steps involve reproduction of copyrighted works.

2. **RAG is less likely to be transformative**: The Office stated that "use of RAG is less likely to be transformative where the purpose is to generate outputs that summarize or provide abridged versions of retrieved copyrighted works, such as news articles, as opposed to hyperlinks."

3. **Market substitution risk**: The Office noted that "retrieval of copyrighted works by RAG can also result in market substitution" -- meaning the RAG output could substitute for reading the original work.

4. **No blanket fair use**: The Office rejected the idea that all AI uses of copyrighted material are fair use, calling it a "highly fact-specific inquiry."

### 3.3 The Four-Factor Fair Use Test (17 U.S.C. Section 107)

Applied to a rulebook RAG system:

| Factor | Analysis | Favors |
|---|---|---|
| **1. Purpose and character of use** | Q&A assistance is arguably transformative (new purpose: interactive help vs. passive reading). But the Copyright Office is skeptical of RAG summarization as transformative. Commercial use weighs against. | Mixed |
| **2. Nature of the copyrighted work** | Rulebooks are factual/instructional (favors fair use) but contain creative expression (illustrations, flavor text). | Slightly favors fair use |
| **3. Amount and substantiality** | RAG systems typically chunk and store the entire work. Even if only portions are retrieved, the entire work was copied into the database. | Weighs against fair use |
| **4. Market effect** | A RAG system answering rules questions could reduce need to consult the original rulebook. However, publishers often provide free PDFs, suggesting limited commercial market harm. | Mixed to slightly against |

**Overall US fair use assessment**: UNCERTAIN. The case is defensible but not clearly favorable, especially after the Copyright Office's skeptical stance on RAG in Part 3 of its AI report.

### 3.4 Dow Jones v. Perplexity AI (S.D.N.Y., filed Oct. 2024)

The first major US lawsuit specifically targeting RAG technology. Dow Jones and NY Post alleged that Perplexity AI:
- Scraped copyrighted news articles into a RAG database
- Generated summaries that substituted for reading the originals
- Allowed users to "skip the links" to original content

As of August 2025, the court denied Perplexity's motion to dismiss. The case is ongoing. While this involves news content (not game rulebooks), the legal theories are directly applicable to any RAG system that stores and retrieves copyrighted text.

**Relevance to MeepleAI**: This case could set significant precedent for RAG copyright liability in the US. The key risk factor is **market substitution** -- does the RAG answer replace the need to read the original?

---

## 4. EU Copyright Law

### 4.1 Directive 2019/790 (Copyright in the Digital Single Market)

The EU takes a fundamentally different approach from the US. Rather than relying on a general fair use defense, the EU provides **specific statutory exceptions** for text and data mining (TDM).

#### Article 2(2) -- Definition of TDM

> "Text and data mining means any automated analytical technique aimed at analysing text and data in digital form in order to generate information which includes but is not limited to patterns, trends and correlations."

RAG processing (chunking, embedding, indexing, retrieval) clearly falls within this definition.

#### Article 3 -- Scientific Research Exception

Permits TDM by research organizations and cultural heritage institutions for scientific purposes, without any opt-out possibility for rightholders. **Not applicable** to a commercial product like MeepleAI.

#### Article 4 -- General TDM Exception (Commercial Use)

This is the key provision for MeepleAI:

> Permits TDM for **any purpose** (including commercial) on works to which the user has **lawful access**, **unless** the rightholder has **expressly reserved their rights** in an "appropriate manner, such as machine-readable means."

**Key elements**:

1. **Lawful access**: The person performing TDM must have legitimate access to the content. Freely available publisher PDFs satisfy this. User-purchased/downloaded PDFs also satisfy this.

2. **Opt-out mechanism**: Rightholders can opt out of Article 4 TDM by:
   - robots.txt directives (most common for online content)
   - TDMRep protocol headers
   - Terms of service/use restrictions (a German court ruled that natural-language ToS can constitute machine-readable opt-out)
   - AI-specific metadata tags

3. **No opt-out = TDM permitted**: If a publisher provides a free PDF without any TDM reservation, processing it in a RAG system is **legally permitted under EU law**.

#### AI Act Interaction

The EU AI Act (2024) explicitly references Article 4 of the DSM Directive, confirming that TDM exceptions apply to AI training, including generative AI and RAG systems. This legislative intent is clear and documented in recitals and cross-references.

### 4.2 Practical Application to Board Game Rulebooks

Most board game publishers:
- Provide rulebook PDFs freely on their websites
- Do not include TDM reservation metadata
- Do not have robots.txt blocking AI crawlers on PDF download pages
- Do not include opt-out clauses in terms of service regarding TDM

**Under current EU law, processing freely available rulebook PDFs in a RAG system is likely permitted under Article 4, absent explicit opt-out by the publisher.**

However, this could change if publishers begin implementing opt-out mechanisms.

---

## 5. Italian Copyright Law

### 5.1 Legge 633/1941 (as amended by Law No. 132/2025)

Italy became the **first EU Member State** to enact comprehensive national AI legislation (Law No. 132 of September 23, 2025, effective October 10, 2025). This law modifies the Italian Copyright Act in significant ways:

#### Article 70-septies (New)

Permits reproduction and extraction of text/data from works lawfully available online or in databases through AI models and systems (including generative AI), provided compliance with:

- **Article 70-ter**: TDM for scientific research by research organizations/cultural heritage institutions (mirrors EU Art. 3)
- **Article 70-quater**: TDM for any purpose including commercial, unless the rightholder has exercised the opt-out (mirrors EU Art. 4)

#### Key Italian Specifics

1. **Explicit AI coverage**: Unlike the EU Directive which left some ambiguity about AI applicability, Italy's Art. 70-septies explicitly states the exception covers "AI models and systems, including generative AI." This provides strong legal clarity.

2. **Criminal penalties**: Italy introduced **criminal sanctions** for TDM violations. Article 171, paragraph 1, now includes a new letter a-ter establishing criminal offenses for unauthorized TDM. This is a significant escalation beyond the EU's civil enforcement framework.

3. **Human authorship reinforcement**: Article 1 of L. 633/1941 was modified to specify that works created with AI aid are protected "provided they constitute the result of the author's intellectual work." This is relevant to whether RAG outputs could themselves be copyrightable.

4. **Opt-out requirement**: Consistent with EU law, Italian TDM is only permitted if the rightholder has NOT opted out. The criminal penalties make compliance with opt-out mechanisms particularly critical in Italy.

### 5.2 "Libero Utilizzo" (Free Use) -- Articles 65-71

Italy does not have a general "fair use" doctrine like the US. Instead, it has a closed list of specific exceptions ("eccezioni e limitazioni"):

- **Art. 65**: Free reproduction of articles on current events in periodicals
- **Art. 68**: Personal reproduction for personal use (single copy)
- **Art. 70**: Quotation for criticism, discussion, teaching (with attribution, within limits)

**Art. 68 (personal reproduction)** could support the scenario where a user uploads their own PDF for personal RAG-assisted Q&A, as this is analogous to making a personal copy for private study. However, the systematic commercial processing of that copy by a platform operator may not fall under this exception.

### 5.3 Practical Implications for MeepleAI in Italy

- Processing freely available rulebook PDFs: **Permitted** under Art. 70-septies/70-quater, absent publisher opt-out
- Must implement **opt-out checking** before processing any content
- **Criminal liability risk** if TDM is performed on opted-out content
- User-uploaded PDFs for personal use: Lower risk under Art. 68, but the platform's systematic processing may not be covered

---

## 6. Fair Use / Fair Dealing Analysis for RAG

### 6.1 Is Chunking and Embedding Transformative?

**Arguments FOR transformativeness**:
- The purpose changes from "reading a manual" to "interactive AI-assisted Q&A"
- Embeddings are mathematical vectors, not human-readable text -- they represent semantic meaning, not literal expression
- The system generates new, synthesized answers rather than reproducing the original
- The use is functional/utilitarian (helping users understand rules during gameplay)

**Arguments AGAINST transformativeness**:
- The US Copyright Office (Part 3, 2025) explicitly stated RAG summarization is "less likely to be transformative"
- Chunks stored in the retrieval database ARE literal copies of the copyrighted text
- Retrieved chunks may be reproduced near-verbatim in system outputs
- The purpose (explaining rules) closely mirrors the original purpose of the rulebook

### 6.2 Does RAG Compete with the Original?

**Arguments that it does NOT compete**:
- Many publishers provide rulebook PDFs for free (no lost sales)
- Players still need the physical game (the rulebook supports the product, not the reverse)
- RAG provides contextual Q&A during gameplay, a different use case from reading a manual cover-to-cover
- The RAG system enhances the gameplay experience, potentially increasing game sales

**Arguments that it DOES compete**:
- Some publishers sell premium digital rulebooks or companion apps
- The RAG system could replace the need to consult the rulebook entirely
- Publisher-specific FAQ pages and BGG forums serve a similar Q&A function
- Third-party tutorial content creators (YouTube, blogs) could claim market harm

### 6.3 Embeddings vs. Full Text Storage

This is a critical technical-legal distinction:

| Storage Type | Copyright Risk | Reasoning |
|---|---|---|
| **Vector embeddings only** | LOWER | Embeddings are mathematical representations; they cannot be reversed to reconstruct the original text. Similar to the "intermediate copy" doctrine. |
| **Chunks of original text** | HIGHER | Literal copies of copyrighted expression stored in the retrieval database. This is straightforward reproduction. |
| **Full original PDF stored** | HIGHEST | Complete reproduction of the copyrighted work, even if only portions are retrieved. |

**For MeepleAI's RAG system**: The system stores text chunks for retrieval, which constitutes reproduction of copyrighted expression. This is the most legally sensitive component.

**Potential mitigation**: If the system could answer questions using ONLY embeddings (without retrieving literal text chunks), the copyright argument becomes significantly weaker. However, current RAG architectures require text chunk retrieval for quality answers.

---

## 7. Key Legal Risks by Scenario

### Scenario A: Downloading publisher-provided free PDFs and processing in RAG

| Factor | Risk Assessment |
|---|---|
| **US Law** | MODERATE-HIGH. Fair use is uncertain. The Copyright Office's RAG skepticism, combined with Dow Jones v. Perplexity precedent risk, creates significant exposure. The "free PDF" factor helps (no market harm to sales) but the systematic copying into a commercial database weighs against. |
| **EU Law** | LOW-MODERATE. Article 4 TDM exception likely applies if publishers have not opted out. Must verify no opt-out exists (robots.txt, ToS, metadata). If no opt-out, this is legally permitted. |
| **Italian Law** | LOW-MODERATE. Art. 70-septies/70-quater mirrors EU framework. Criminal penalties for violations elevate the consequences of getting it wrong. Must implement robust opt-out checking. |

**Overall risk**: MODERATE. Defensible in the EU/Italy under TDM exceptions if opt-out compliance is rigorous. Higher risk in the US without clear fair use protection.

### Scenario B: Users uploading their own purchased/downloaded PDFs for personal RAG use

| Factor | Risk Assessment |
|---|---|
| **US Law** | LOW-MODERATE. Strongest fair use argument: personal/educational purpose, user already has lawful copy, transformative use for personal Q&A. But the platform operator (MeepleAI) is still making copies. |
| **EU Law** | LOW. User has lawful access. Processing for personal use. Platform acts as a tool/service. Art. 4 TDM exception applies. Private copying exceptions (Art. 5(2)(b) InfoSoc Directive) may also apply. |
| **Italian Law** | LOW. Art. 68 (personal reproduction) + Art. 70-septies coverage. User-initiated, personal purpose, lawful access. |

**Overall risk**: LOW. This is the safest scenario. The user has lawful access, the purpose is personal, and the platform acts as a processing tool rather than a content distributor.

### Scenario C: Caching/storing chunks of rulebook text for retrieval

| Factor | Risk Assessment |
|---|---|
| **US Law** | MODERATE-HIGH. Literal copying into a database is reproduction. Duration of storage matters -- temporary/cache copies may be treated differently than permanent storage. Amount stored (full work vs. selective chunks) matters. |
| **EU Law** | LOW-MODERATE. TDM exception explicitly permits "acts of reproduction and extraction." Storage of extracted data is covered as necessary for TDM activities, subject to opt-out compliance. Art. 4 requires lawful access. |
| **Italian Law** | LOW-MODERATE. Art. 70-septies covers "reproduction and extraction." Same framework as EU. Criminal penalties for violations. |

**Overall risk**: MODERATE. Storage of text chunks is the most legally exposed technical component. EU/Italy provide clearer statutory authorization than the US.

---

## 8. Risk Mitigation Strategies

### 8.1 Technical Mitigations

1. **User-upload model preferred**: Design the primary flow around users uploading their own PDFs rather than centrally downloading publisher PDFs. This shifts the copyright analysis toward personal use.

2. **Opt-out compliance system**: Before processing any publisher PDF, check:
   - robots.txt for TDM/AI-related directives
   - Publisher terms of service for TDM reservations
   - File metadata for TDM opt-out tags
   - Maintain a blocklist of publishers who have opted out

3. **Minimize stored text**: Store the smallest chunks necessary for quality retrieval. Consider whether embedding-only retrieval is technically feasible for any use cases.

4. **Output controls**: Implement guardrails preventing the system from reproducing large verbatim passages from rulebooks. Paraphrase and synthesize rather than quote.

5. **Attribution**: Always cite the source rulebook and publisher in RAG outputs. Include links to the original PDF where available.

6. **Ephemeral processing**: For user-uploaded PDFs, consider processing and generating embeddings in-session without permanent storage of text chunks. Delete source text after embedding generation.

### 8.2 Legal/Business Mitigations

1. **Terms of Service**: Clearly state that users are responsible for having lawful access to uploaded PDFs. Include appropriate DMCA/notice-and-takedown procedures.

2. **Publisher partnerships**: Proactively reach out to major board game publishers to obtain explicit permission or licensing agreements for rulebook processing. Frame it as a value-add that drives game engagement.

3. **Licensing framework**: Consider implementing a licensing system where publishers can opt-in to have their rulebooks included, potentially with revenue sharing or promotional benefits.

4. **Geographic compliance**: Implement jurisdiction-aware processing. EU/Italy TDM compliance may differ from US requirements.

5. **Documentation**: Maintain records of:
   - When and how each PDF was obtained
   - Publisher opt-out status at time of processing
   - User consent and upload records
   - Processing logs for compliance auditing

### 8.3 Architecture Recommendations

```
LOWEST RISK ARCHITECTURE:
User uploads own PDF
  -> Process in user's session
  -> Generate embeddings (stored)
  -> Store minimal text chunks (user-scoped, not shared)
  -> Answer questions using RAG
  -> Text chunks deletable by user
  -> No cross-user sharing of chunked text

MODERATE RISK ARCHITECTURE:
Platform downloads free publisher PDFs
  -> Check opt-out status FIRST
  -> Process only non-opted-out content
  -> Store embeddings and chunks (platform-scoped)
  -> All users can query
  -> Attribution and source links in all outputs
  -> Publisher takedown mechanism

HIGHEST RISK ARCHITECTURE:
Platform stores complete PDFs centrally
  -> No opt-out checking
  -> Verbatim retrieval enabled
  -> No attribution
  -> Cross-user content sharing
  -> No takedown mechanism
```

---

## 9. Conclusions and Recommendations

### 9.1 Summary of Legal Position

1. **Game mechanics are free to use** -- no copyright protection in any jurisdiction. A RAG system can describe how to play any game without copyright concern, provided it uses its own words.

2. **Rulebook text is copyrighted** -- the specific expression in a publisher's rulebook is protected. Copying it into a RAG database is reproduction. Retrieving and displaying it is potentially creating derivative works.

3. **EU/Italy offer clearer legal footing** than the US -- the TDM exceptions (Art. 4 DSM Directive / Art. 70-septies L. 633/1941) provide explicit statutory permission for commercial TDM, subject to opt-out compliance. The US relies on uncertain fair use analysis, and the Copyright Office has expressed skepticism about RAG's transformative nature.

4. **User-upload model is safest** across all jurisdictions -- personal use, lawful access, and user-initiated processing provide the strongest legal position.

5. **Opt-out compliance is mandatory in the EU/Italy** -- and carries criminal penalties in Italy for violations.

### 9.2 Specific Recommendations for MeepleAI

| Priority | Recommendation |
|---|---|
| **CRITICAL** | Implement user-upload as the primary PDF ingestion path |
| **CRITICAL** | Build opt-out checking system before any centralized PDF processing |
| **CRITICAL** | Implement output guardrails to prevent verbatim reproduction |
| **HIGH** | Add attribution and source links to all RAG responses |
| **HIGH** | Include DMCA/takedown mechanism in platform ToS |
| **HIGH** | Scope text chunk storage to individual users (not shared) |
| **MEDIUM** | Explore publisher partnership/licensing program |
| **MEDIUM** | Implement ephemeral processing option (delete source text after embedding) |
| **MEDIUM** | Document compliance procedures for audit trail |
| **LOW** | Monitor Dow Jones v. Perplexity AI outcome for US precedent |
| **LOW** | Track EU TDM opt-out standardization developments |

### 9.3 Confidence Assessment

| Area | Confidence | Basis |
|---|---|---|
| Game mechanics not copyrightable | 95% | Settled law across all jurisdictions |
| Rulebook text is copyrightable | 95% | Standard copyright doctrine |
| EU TDM exception covers RAG | 85% | Statutory text + AI Act cross-reference, but limited case law |
| Italian criminal penalties apply | 90% | Explicit statutory text (Law No. 132/2025) |
| US fair use is uncertain for RAG | 90% | Copyright Office Part 3 + pending litigation |
| User-upload model is lowest risk | 85% | Consistent across jurisdictions but not tested in court |

---

## 10. Sources

### Case Law
- [Baker v. Selden, 101 U.S. 99 (1879)](https://en.wikipedia.org/wiki/Baker_v._Selden)
- [Lotus Development Corp. v. Borland International, Inc., 49 F.3d 807 (1st Cir. 1995)](https://www.bitlaw.com/source/cases/copyright/Lotus.html)
- [Dow Jones & Co. v. Perplexity AI, Inc. (S.D.N.Y. 2024)](https://law.justia.com/cases/federal/district-courts/new-york/nysdce/1:2024cv07984/630270/65/)

### US Copyright Office Reports
- [Copyright and Artificial Intelligence -- Official Page](https://www.copyright.gov/ai/)
- [Part 3: Generative AI Training (Pre-Publication, May 2025)](https://www.copyright.gov/ai/Copyright-and-Artificial-Intelligence-Part-3-Generative-AI-Training-Report-Pre-Publication-Version.pdf)
- [US Copyright Office AI Study -- All Parts](https://www.copyright.gov/policy/artificial-intelligence/)

### US Copyright Office Analysis (Law Firm Summaries)
- [Mayer Brown: Copyright Office Weighs in on Fair Use for Generative AI Training](https://www.mayerbrown.com/en/insights/publications/2025/05/united-states-copyright-office-weighs-in-on-fair-use-defense-for-generative-ai-training)
- [Sidley Austin: Generative AI Meets Copyright Scrutiny (Part III Highlights)](https://www.sidley.com/en/insights/newsupdates/2025/05/generative-ai-meets-copyright-scrutiny)
- [Skadden: Copyright Office Report on AI Training and Fair Use](https://www.skadden.com/insights/publications/2025/05/copyright-office-report)
- [McDermott: US Copyright Office Report on Copyrighted Material in AI Training](https://www.mwe.com/insights/us-copyright-office-issues-report-addressing-use-of-copyrighted-material-to-train-generative-ai-systems/)
- [Authors Guild: What Authors Should Know About Part 3](https://authorsguild.org/news/us-copyright-office-ai-report-part-3-what-authors-should-know/)

### EU Copyright Directive
- [Text and Data Mining: Articles 3 and 4 (Geiger, Frosio, Bulayenko -- SSRN)](https://papers.ssrn.com/sol3/papers.cfm?abstract_id=3470653)
- [Knowledge Rights 21: Why EU TDM Exceptions Apply to AI](https://knowledgerights21.org/news-story/eu-tdm-exceptions-can-be-used-for-ai/)
- [Kluwer Copyright Blog: The New Copyright Directive TDM Articles](https://legalblogs.wolterskluwer.com/copyright-blog/the-new-copyright-directive-text-and-data-mining-articles-3-and-4/)
- [Oxford Academic: TDM Opt-Out in Article 4(3) CDSMD](https://academic.oup.com/jiplp/article/19/5/453/7614898)
- [Kluwer Copyright Blog: TDM Opt-Out -- Five Problems, One Solution](https://legalblogs.wolterskluwer.com/copyright-blog/the-tdm-opt-out-in-the-eu-five-problems-one-solution/)
- [Orrick: First Significant EU Decision on Data Mining for AI Training](https://www.orrick.com/en/Insights/2024/10/Significant-EU-Decision-Concerning-Data-Mining-and-Dataset-Creation-to-Train-AI)
- [TechnoLlama: EU TDM Exception and AI Training](https://www.technollama.co.uk/we-need-to-talk-about-the-eu-tdm-exception-and-ai-training)
- [EU Parliament: AI and Copyright -- Training of GPAI (2025)](https://www.europarl.europa.eu/RegData/etudes/ATAG/2025/769585/EPRS_ATA(2025)769585_EN.pdf)

### Italian Copyright Law
- [Cleary Gottlieb: Italy Adopts First National AI Law](https://www.clearygottlieb.com/news-and-insights/publication-listing/italy-adopts-the-first-national-ai-law-in-europe-complementing-the-eu-ai-act)
- [Hogan Lovells: Copyright Provisions in the New Italian AI Law](https://www.hoganlovells.com/en/publications/copyright-provisions-in-the-new-italian-ailaw-reinforcing-human-authorship-and-text-and-data-mining)
- [Trademark Lawyer Magazine: Italy's New Copyright Rules in AI Law](https://trademarklawyermagazine.com/italys-new-copyright-rules-in-the-first-national-ai-law-by-an-eu-member-state/)
- [Kluwer Copyright Blog: New Italian Law on AI -- A General Framework](https://legalblogs.wolterskluwer.com/copyright-blog/new-italian-law-on-ai-a-general-framework/)
- [Communia: Italy Updates Copyright Law for AI](https://communia-association.org/2025/10/01/italy-updates-its-copyright-law-to-address-ai/)
- [Norton Rose Fulbright: Italy Enacts Law No. 132/2025](https://www.nortonrosefulbright.com/en/knowledge/publications/9bfedfea/italy-enacts-law-no-132-2025-on-artificial-intelligence-sector-rules-and-next-steps)
- [WIPO Lex: Law No. 633/1941 (consolidated text)](https://www.wipo.int/wipolex/en/legislation/details/21564)

### Board Game Copyright
- [ABA: Not Playing Around -- Board Games and IP Law](https://www.americanbar.org/groups/intellectual_property_law/resources/landslide/archive/not-playing-around-board-games-intellectual-property-law/)
- [ABA: Why Videogame Rules Are Not Expression Protected by Copyright](https://www.americanbar.org/groups/intellectual_property_law/resources/landslide/archive/why-videogame-rules-are-not-expression-protected-copyright-law/)
- [Legal Moves Law Firm: Are Board Games Copyrighted? (2025)](https://legalmoveslawfirm.com/board-games-copyrighted/)
- [Meeple Mountain: Board Game Designer's Guide to IP Law](https://www.meeplemountain.com/articles/the-board-game-designers-guide-to-intellectual-property-law/)
- [Vanderbilt Journal of Entertainment & Technology Law: Patenting Games -- Baker v. Selden Revisited](https://scholarship.law.vanderbilt.edu/cgi/viewcontent.cgi?article=1337&context=jetlaw)

### RAG and AI Copyright
- [36kr: New Copyright Concerns in RAG -- What You Need to Know](https://eu.36kr.com/en/p/3422429684387205)
- [Asia IP Law: The Latest Rage Called RAG](https://www.asiaiplaw.com/section/in-depth/the-latest-rage-called-rag)
- [Norton Rose Fulbright: Practical Commentary on Copyright and Generative AI Training](https://www.nortonrosefulbright.com/en/knowledge/publications/87200379/practical-commentary-regarding-copyright-and-generative-ai-training)
- [Arxiv: Incorporating Legal Structure in RAG for Copyright Fair Use](https://arxiv.org/abs/2505.02164)
- [News/Media Alliance: Copyright Office AI Report Press Release](https://www.newsmediaalliance.org/copyright-office-ai-report-press-release/)
- [Loeb & Loeb: Dow Jones v. Perplexity AI Analysis](https://www.loeb.com/en/insights/publications/2025/08/dow-jones-and-company-inc-v-perplexity-ai-inc)
- [Copyright Alliance: AI Copyright Lawsuit Developments in 2025](https://copyrightalliance.org/ai-copyright-lawsuit-developments-2025/)
- [ArentFox Schiff: News Corp Continues Battle Against Perplexity AI](https://www.afslaw.com/perspectives/ai-law-blog/generative-ai-meets-generative-litigation-news-corp-continues-its-battle)

### Embeddings and Vector Storage
- [Kluwer Copyright Blog: Memorisation in Generative Models and EU Copyright Law](https://legalblogs.wolterskluwer.com/copyright-blog/memorisation-in-generative-models-and-eu-copyright-law-an-interdisciplinary-view/)
- [IPWatchdog: The Licensing Vector -- A Fair Approach to Content Use in LLMs](https://ipwatchdog.com/2024/04/10/licensing-vector-fair-approach-content-use-llms-2/id=175202/)
- [EFF: Don't Make Embedding Illegal (March 2026)](https://www.eff.org/deeplinks/2026/03/eff-court-dont-make-embedding-illegal)

---

*Research conducted: 2026-03-07*
*Methodology: Multi-source web research using legal databases, law firm publications, government reports, and academic papers. All findings cross-referenced across multiple sources.*
