# RAG Systems and Copyrighted Content: Legal Analysis and Best Practices

**Research Date**: 2026-03-07
**Scope**: Copyright law implications for RAG systems processing copyrighted content (board game rulebooks)
**Jurisdiction Focus**: United States (primary), EU (secondary)
**Confidence Level**: High (based on 2024-2026 case law, Copyright Office reports, law firm analyses)

---

## Executive Summary

RAG systems occupy a legally distinct and more precarious position than general AI model training when it comes to copyright law. While courts have begun finding AI training itself to be fair use (Bartz v. Anthropic, Kadrey v. Meta), RAG-based retrieval and output of copyrighted content faces significantly higher legal risk because it more closely resembles traditional copying and market substitution. For MeepleAI's use case -- processing user-uploaded board game rulebooks -- the risk profile is moderate but manageable through proper legal architecture.

**Key Findings**:
1. The U.S. Copyright Office explicitly distinguishes RAG from training and views RAG as less likely to qualify as fair use
2. The first RAG-specific lawsuit (Advance Local Media v. Cohere) survived a motion to dismiss in 2025
3. User-uploaded content with proper TOS shifts significant liability, but does not eliminate it entirely
4. Technical measures (chunking strategy, no full-text storage) meaningfully reduce risk exposure
5. The "personal use by licensee" argument is the strongest defense for MeepleAI's model

---

## 1. AI Training vs. RAG: The Critical Legal Distinction

### 1.1 How Copyright Law Treats AI Training

Courts are converging on the view that AI model training on copyrighted works can constitute fair use, primarily because it is **transformative** -- converting individual works into statistical weights that generate novel outputs.

**Bartz v. Anthropic (N.D. Cal., June 2025)**: Judge Alsup ruled that using books to train Claude was fair use, calling AI training "quintessentially transformative" and "spectacularly so." However, he critically distinguished between:
- **Legally acquired works** used for training = fair use
- **Pirated works** (from shadow libraries like LibGen) used for training = NOT fair use

The case settled in August 2025 after class certification.

**Kadrey v. Meta (N.D. Cal., 2025)**: Similarly found AI training to be fair use on substantially similar reasoning.

### 1.2 How Copyright Law Treats RAG Systems

RAG faces a fundamentally different analysis because it does not merely learn from copyrighted works -- it **stores**, **retrieves**, and **outputs** portions of them.

**U.S. Copyright Office Part 3 Report (May 2025)**: The Office drew an explicit distinction:

> "RAG systems function in two steps: the system first copies the source materials into a retrieval database, and then, when prompted by a user query, outputs them again. While such an architecture improves accuracy, both the initial unauthorized reproduction and the later relaying of that material are potential copyright infringements which do not qualify as fair use."

> "The use of RAG is less likely to be transformative where the purpose is to generate outputs that summarize or provide abridged versions of retrieved copyrighted works."

This is a significant policy signal. The Copyright Office views RAG as:
1. Involving **two acts of copying** (ingestion into database + output to user)
2. **Less transformative** than training because it directly relays source content
3. **More likely to cause market harm** because outputs substitute for originals

### 1.3 Embedding Generation

The legal status of embeddings (vector representations) is unsettled but trending toward risk:

- Embeddings preserve semantic relationships from original content
- Technical research shows "extraction attacks" can recover significant portions of source text
- The Copyright Office notes that "tokenized datasets constitute reproductions" -- embeddings are a form of tokenization
- However, embeddings alone are not human-readable copies, which may provide some defense
- No court has directly ruled on whether generating embeddings alone constitutes infringement

**Risk Assessment**: Embeddings occupy a gray area. They are clearly derived from copyrighted works, but whether the derivation constitutes "copying" under copyright law remains unresolved. Storing only embeddings (without source text chunks) would reduce but not eliminate risk.

### 1.4 Why the Distinction Matters

| Factor | AI Training | RAG System |
|--------|------------|------------|
| Transformativeness | High (statistical model) | Low-Medium (retrieves/summarizes) |
| Copying | Intermediate (into weights) | Direct (into database + output) |
| Market substitution | Low (generates novel content) | High (can substitute for original) |
| Copyright Office view | "May be fair use" | "Less likely fair use" |
| Court rulings | Trending toward fair use | First case survived MTD (Cohere) |

---

## 2. Key AI Copyright Cases (2024-2026)

### 2.1 NYT v. OpenAI (S.D.N.Y., filed Dec 2023)

**Status**: In discovery phase as of January 2026.

**Key Developments**:
- April 2025: Court narrowed claims, dismissed several, focusing case primarily on fair use
- November 2025: Court ordered OpenAI to produce 20 million ChatGPT logs (not just cherry-picked conversations)
- January 2026: Judge Stein affirmed the discovery order over OpenAI's privacy objections
- No fair use ruling expected until summer 2026 at the earliest

**Implications for RAG**: The case primarily addresses training, but discovery of output logs showing verbatim reproduction is directly relevant to RAG systems that retrieve and display copyrighted text.

### 2.2 Authors Guild v. OpenAI (S.D.N.Y., filed Sept 2023)

**Status**: Consolidated with other cases; in discovery phase.

**Key Developments**:
- April 2025: Transferred and consolidated in S.D.N.Y.
- October 2025: Judge Stein denied OpenAI's motion to dismiss output infringement claims, finding authors may be able to prove ChatGPT outputs are "similar enough" to violate copyrights
- Court explicitly declined to opine on fair use at this stage
- Discovery conferences ongoing (January-February 2026)

**Implications for RAG**: The court's willingness to allow output-based infringement claims to proceed is directly relevant. RAG outputs that closely track source material face similar exposure.

### 2.3 Thomson Reuters v. Ross Intelligence (D. Del., filed Dec 2020)

**Status**: Partial summary judgment granted February 2025; trial on remaining issues scheduled May 2025.

**Key Ruling**: First court to **reject** fair use for AI training in the context of a competitive product.

**Critical Findings**:
- Ross used Westlaw headnotes (via a third party) to build a competing legal search AI
- Court found the use was **commercial** and lacked a "further purpose or different character" because Ross's product directly competed with Westlaw
- Court recognized an **obvious potential market for licensing copyrighted material for AI training**
- The competitive relationship between the original and the AI product was decisive

**Implications for RAG**: This is the most directly relevant precedent for RAG systems. Ross built what was essentially a RAG-like search tool that competed with the source. The court's emphasis on competitive relationship and the existence of licensing markets is critical. MeepleAI's position differs because it does not compete with rulebook publishers, but the case shows courts will scrutinize whether the AI product substitutes for purchasing/accessing the original.

### 2.4 Getty Images v. Stability AI (UK High Court, Nov 2025)

**Status**: Judgment issued November 4, 2025.

**Key Ruling**: Getty largely lost -- the court found:
- AI model weights are **not a "copy"** of training images
- Models contain "statistically trained parameters, not stored copies"
- Getty abandoned its primary copyright claim because training did not occur in the UK
- Limited trademark infringement found

**Implications for RAG**: The UK ruling that model weights are not copies supports the argument that embeddings may not constitute copies. However, RAG systems store more than just weights -- they store retrievable text chunks, which is a different (and riskier) proposition.

### 2.5 Advance Local Media v. Cohere -- The First RAG Case (S.D.N.Y., filed Feb 2025)

**Status**: Motion to dismiss denied November 2025; proceeding to discovery.

**This is the most important case for RAG systems.** Fourteen publishers (Forbes, Conde Nast, LA Times, The Atlantic, etc.) sued Cohere alleging its RAG system:
- Accesses publishers' sites and incorporates content into responses
- Produces "verbatim copies, substantial excerpts, and substitutive summaries"
- Bypasses paywalls
- Over 4,000 allegedly infringed works identified

**Court's Ruling on MTD**:
- Denied dismissal on direct infringement, secondary infringement, and Lanham Act claims
- Found that "in some of the examples, the outputs were nearly identical to the underlying work, lifting several paragraphs in their entirety"
- Rejected Cohere's argument that summaries differed in "tone, style, length and sentence structure"

**Implications for RAG**: This case establishes that RAG systems face viable copyright claims when they retrieve and output substantial portions of copyrighted works. The critical distinction for MeepleAI is that Cohere scraped publishers' content without permission, while MeepleAI processes user-uploaded content.

---

## 3. RAG-Specific Legal Analysis

### 3.1 Does Storing Text Chunks in a Vector Database Constitute "Copying"?

**Answer: Almost certainly yes, in the traditional copyright sense.**

Under 17 U.S.C. 106, copyright holders have the exclusive right to "reproduce the copyrighted work in copies." Storing text chunks -- even broken into segments -- creates reproductions. The Copyright Office's Part 3 report explicitly identifies "copying source materials into a retrieval database" as the first act of potential infringement in RAG systems.

**Mitigating factors**:
- If chunks are small enough, they may fall below the threshold of substantial similarity
- If chunks are stored only temporarily (caching), they may qualify for the ephemeral copy exception
- If the user uploaded the content, the platform may argue it is the user who made the copy

### 3.2 Is Retrieving and Displaying Snippets Fair Use?

**Analysis under the four fair use factors (17 U.S.C. 107):**

| Factor | Analysis for MeepleAI | Risk Level |
|--------|----------------------|------------|
| **Purpose and character** | Commercial service, but does not compete with rulebook sales. Purpose is to help users understand games they already own. Some transformative element (answering questions vs. reading rules). | Medium |
| **Nature of the work** | Rulebooks are factual/instructional, not highly creative. Factual works receive thinner protection. | Low |
| **Amount used** | RAG retrieves chunks, not full works. But cumulative retrieval could reconstruct substantial portions. | Medium |
| **Market effect** | Users have already purchased the game. The service does not substitute for buying the rulebook. | Low |

**Overall Fair Use Assessment**: Moderate-to-favorable. The factual nature of rulebooks and the fact that users have already purchased the game are strong factors. The key risk is if the system can output large portions of verbatim text.

### 3.3 Does Purpose Affect the Analysis?

**Yes, significantly.** MeepleAI's purpose -- helping users understand games they already own -- is distinct from cases like Cohere (substituting for news articles) or Ross (competing with Westlaw). Courts consider:

- **Non-competitive purpose**: MeepleAI does not compete with game publishers
- **Complementary use**: The service adds value to the purchased product
- **User benefit**: Helps users get more value from games they already bought
- **No market substitution**: Nobody buys a rulebook to read it like a book; the RAG system does not replace the product

### 3.4 User-Uploaded Content: The "User is the Licensee" Argument

**This is MeepleAI's strongest legal position.**

When users upload their own rulebooks:
1. They typically own a physical copy (implied license to personal use)
2. They are making the copy, not MeepleAI
3. The platform is providing a tool, similar to a scanner or note-taking app
4. The user's personal use purpose is stronger than the platform's commercial purpose

**Analogies**:
- Google Books: Court found that scanning books for search indexing was fair use (Authors Guild v. Google, 2d Cir. 2015)
- Personal cloud storage: Users storing their own purchased content is generally accepted
- Note-taking apps: Evernote, OneNote process user-uploaded documents without copyright issues

**Limitations of this argument**:
- It weakens if MeepleAI uses uploaded content across users (one user's upload benefits all users)
- It weakens if MeepleAI retains content after the user leaves
- It weakens if uploaded content is used to train models

---

## 4. Safe Harbor and Platform Liability

### 4.1 DMCA Safe Harbor (17 U.S.C. 512)

DMCA safe harbor protects platforms from liability for user-uploaded infringing content, provided they:

1. **Designate a DMCA agent** with the Copyright Office
2. **Implement a repeat infringer policy**
3. **Respond expeditiously** to takedown notices
4. **Do not have actual knowledge** of infringement
5. **Do not receive financial benefit** directly attributable to infringing activity they could control

**Application to MeepleAI**:
- Safe harbor likely applies to user-uploaded PDFs (users upload, platform hosts)
- The platform must not actively encourage uploading copyrighted content
- Processing content with AI may complicate the "passive intermediary" assumption
- As of 2025, courts and scholars note that "generative AI overturns the passive-intermediary assumptions that underlie the DMCA safe harbour"

**Proposed "AI Harbour" Framework** (academic, 2025): Scholars have proposed tiered duties:
- Data suppliers: provenance disclosure and transparency
- Developers: dataset curation, memorization-mitigation, watermarking
- Deployers: dynamic filtering, complaint handling, repeat-infringer policies

**Key Risk**: If MeepleAI processes uploaded content to generate responses (not merely hosting it), a court might find the platform is doing more than passively hosting, potentially weakening the safe harbor defense. The tighter compliance timelines being demanded in 2025 also increase operational burden.

### 4.2 EU Digital Services Act (DSA)

The DSA (fully applicable since February 2024) imposes obligations on platforms hosting user content:

- **Notice and action mechanisms**: Must provide easy-to-use systems for reporting illegal content
- **Transparency reporting**: Must publish regular reports on content moderation
- **Algorithmic accountability**: Must explain how AI systems recommend or process content
- **Due diligence obligations**: Scale with platform size

**EU AI Act Copyright Provisions (effective August 2025)**:
- GPAI providers must comply with copyright law, including EU text and data mining exceptions
- Must respect "opt-out" reservations by rights holders against TDM
- Must produce sufficiently detailed summaries of training data used
- The EU approach is more prescriptive than the U.S. fair use framework

**Implications for MeepleAI**: If operating in the EU, MeepleAI would need to respect TDM opt-outs from publishers and provide transparency about how uploaded content is processed. The DSA's notice-and-action requirements would apply.

### 4.3 Platform vs. User Responsibility

| Scenario | Likely Liability Bearer | Legal Basis |
|----------|------------------------|-------------|
| User uploads own purchased rulebook | User (personal copy) | Fair use / implied license |
| User uploads pirated PDF | User (primary); Platform (if knew/should have known) | DMCA 512 |
| Platform uses upload across all users | Platform (beyond user's personal use) | Direct infringement |
| Platform trains models on uploads | Platform (new use beyond user authorization) | Reproduction right |
| Platform stores only embeddings from upload | Unclear (gray area) | No direct precedent |

---

## 5. Best Practices from Similar Services

### 5.1 Google NotebookLM

**Model**: Users upload their own documents; AI processes them for Q&A and summarization.

**Key TOS provisions**:
- Users must have "necessary rights to upload or share content" and content must be "lawful"
- Google does not claim ownership of generated content
- User data is not used to train models (for Workspace/Education accounts)
- Feedback may be reviewed
- Users indemnify Google for claims arising from their use
- DMCA compliance with notice-and-takedown

**Relevance**: NotebookLM is the closest comparable to MeepleAI's model. Google's approach relies heavily on (a) user certification of rights and (b) not using content for training.

### 5.2 ChatGPT (OpenAI)

**Model**: Users can upload documents for analysis; also scrapes web for training.

**Key provisions**:
- Users retain ownership of inputs and outputs (subject to applicable law)
- Users represent they have rights to input content
- OpenAI may use inputs for model improvement (can opt out with API/Enterprise)
- Indemnification clause for user-provided content

### 5.3 Legal Research AI (Westlaw AI, Lexis+ AI Protege)

**Model**: These services process exclusively **licensed** content.

**Key approach**:
- Thomson Reuters and LexisNexis own or license all content their AI processes
- Westlaw AI is "grounded in trusted, authoritative content" that TR has rights to
- Lexis+ AI Protege is trained on "primary law and limited secondary sources owned by Lexis"
- Neither processes user-uploaded third-party copyrighted content

**Lesson**: The legally safest approach is to process only content you own or license. This is why TR sued Ross -- Ross tried to build a competing product without licensing.

### 5.4 Common TOS Patterns Across Services

1. **User certification**: Users affirm they have rights to uploaded content
2. **Indemnification**: Users agree to hold the platform harmless for IP claims
3. **No training pledge**: Content not used for model training without consent
4. **DMCA compliance**: Designated agent, takedown procedures, repeat infringer policy
5. **Content restrictions**: Prohibited from uploading content without rights
6. **Limitation of liability**: Platform liability capped, typically at subscription fees paid

---

## 6. Practical Risk Mitigation Strategies for MeepleAI

### 6.1 Terms of Service Provisions (HIGH PRIORITY)

**Required clauses**:

1. **User Certification of Rights**:
   > "By uploading content, you represent and warrant that you own or have the legal right to use and process such content, including but not limited to copyright rights. You represent that you have lawfully obtained any rulebooks, manuals, or game documents you upload."

2. **Indemnification**:
   > "You agree to indemnify, defend, and hold harmless MeepleAI from any claims, damages, losses, or expenses arising from your upload of content to which you do not have the requisite rights."

3. **No Training Pledge**:
   > "User-uploaded content is processed solely to provide you with AI-assisted game guidance. Your uploaded content is not used to train our AI models and is not shared with other users."

4. **Content Scope Limitation**:
   > "This service is designed to process board game rulebooks and related game materials that you have lawfully purchased or obtained."

5. **DMCA Notice Procedure**:
   > Full DMCA takedown procedure, designated agent registration, repeat infringer policy

6. **Acceptable Use**:
   > "You may not upload content that you do not have the right to reproduce, including pirated, unlicensed, or unauthorized copies of copyrighted materials."

### 6.2 Technical Measures (HIGH PRIORITY)

1. **No full-text storage**: Store only embeddings and minimal text chunks necessary for retrieval. Do not retain complete document text after processing.

2. **Chunk size limits**: Keep retrieved chunks small (e.g., 200-500 tokens). Smaller chunks reduce the risk of reproducing "substantial" portions.

3. **Output guardrails**: Limit the amount of verbatim text that can appear in a single response. Paraphrase rather than quote when possible.

4. **Per-user isolation**: Each user's uploaded content should be isolated. One user's upload must never augment another user's queries.

5. **Retention limits**: Auto-delete uploaded content and derived data after a reasonable period (e.g., 90 days of inactivity).

6. **No cross-training**: Never use user-uploaded content to fine-tune or train models.

7. **Watermark/source detection**: Optionally detect and refuse clearly pirated content (e.g., PDFs with piracy watermarks or from known piracy sources).

### 6.3 DMCA Compliance (HIGH PRIORITY)

1. **Register a DMCA agent** with the U.S. Copyright Office
2. **Publish a DMCA policy** on the website
3. **Implement takedown procedures**: Respond to valid takedown notices within 24-48 hours
4. **Repeat infringer policy**: Terminate accounts of users who repeatedly upload infringing content
5. **Counter-notification process**: Allow users to contest takedowns
6. **Logging**: Maintain records of takedown notices and responses

### 6.4 Content Licensing (MEDIUM PRIORITY)

1. **Publisher partnerships**: Approach board game publishers for explicit licenses to process their rulebooks. Many publishers (especially indie publishers) may welcome this as it increases game adoption.

2. **Open/CC-licensed content**: Prioritize processing content under Creative Commons or similar open licenses.

3. **Publisher opt-out mechanism**: Allow publishers to register their works and opt out of processing.

4. **Revenue sharing**: Consider models where publishers receive compensation for licensed content processing.

### 6.5 User Communication (MEDIUM PRIORITY)

1. **Clear upload prompts**: When users upload, display: "Please only upload rulebooks for games you own."

2. **FAQ**: Publish a clear FAQ explaining what happens to uploaded content, how it's processed, and when it's deleted.

3. **Privacy policy**: Explain data handling, retention, and deletion in the privacy policy.

### 6.6 Risk Tier Assessment for MeepleAI

| Risk Factor | MeepleAI's Position | Risk Level |
|-------------|---------------------|------------|
| Content type | Factual/instructional (rulebooks) | LOW |
| Market substitution | None (complements game purchase) | LOW |
| Who uploads | Users upload their own purchased content | LOW |
| Verbatim reproduction | RAG chunks may contain verbatim text | MEDIUM |
| Cross-user sharing | If one upload serves all users: HIGH | VARIES |
| Content retention | If stored indefinitely: MEDIUM | VARIES |
| Publisher relationship | Non-competitive, complementary | LOW |
| Scale | Small platform vs. major publisher catalogs | LOW |

**Overall Risk**: LOW-MEDIUM with proper safeguards implemented.

---

## 7. Conclusions and Recommendations

### 7.1 Legal Landscape Summary

The legal landscape for RAG and copyright is evolving rapidly but trending toward:
- **Training = generally fair use** (Bartz, Kadrey)
- **RAG = higher risk** than training (Copyright Office Part 3, Cohere lawsuit)
- **User-uploaded content = significant protection** if properly structured (DMCA safe harbor, TOS)
- **Competitive products = highest risk** (Thomson Reuters v. Ross)
- **Complementary products = lower risk** (MeepleAI's position)

### 7.2 Priority Actions for MeepleAI

**Immediate (before launch)**:
1. Implement comprehensive TOS with user certification, indemnification, and acceptable use provisions
2. Register a DMCA agent with the Copyright Office
3. Implement per-user content isolation (no cross-user sharing of uploaded content)
4. Implement output guardrails limiting verbatim reproduction
5. Publish DMCA policy and takedown procedures

**Short-term (within 3 months of launch)**:
1. Implement automated content retention limits
2. Begin outreach to board game publishers for partnership/licensing
3. Implement publisher opt-out mechanism
4. Review and optimize chunk sizes for legal risk minimization

**Medium-term (6-12 months)**:
1. Monitor Cohere case and NYT v. OpenAI for new precedents
2. Consider EU compliance if serving European users (DSA, AI Act)
3. Explore content licensing agreements with major publishers
4. Regular legal review of RAG output patterns for infringement risk

### 7.3 The Strongest Legal Argument for MeepleAI

MeepleAI's best legal position rests on the combination of:

1. **User agency**: Users upload content they own/purchased
2. **Factual content**: Rulebooks are factual/instructional, receiving thin copyright protection
3. **Non-competitive purpose**: Service complements rather than substitutes for game purchases
4. **Personal use**: Processing serves the user's personal understanding of their own games
5. **Platform neutrality**: MeepleAI provides the tool; users choose what to upload
6. **DMCA compliance**: Full safe harbor procedures in place

This combination -- lawful user + factual content + non-competitive purpose + DMCA compliance -- provides a strong multilayered defense.

---

## Sources

### Case Law
- [Bartz v. Anthropic - Landmark Fair Use Ruling](https://www.afslaw.com/perspectives/alerts/landmark-ruling-ai-copyright-fair-use-vs-infringement-bartz-v-anthropic)
- [Bartz v. Anthropic - Settlement](https://www.insidetechlaw.com/blog/2025/09/bartz-v-anthropic-settlement-reached-after-landmark-summary-judgment-and-class-certification)
- [Thomson Reuters v. Ross Intelligence](https://www.dwt.com/blogs/artificial-intelligence-law-advisor/2025/02/reuters-ross-court-ruling-ai-copyright-fair-use)
- [Thomson Reuters v. Ross - Reed Smith Analysis](https://www.reedsmith.com/en/perspectives/2025/03/court-ai-fair-use-thomson-reuters-enterprise-gmbh-ross-intelligence)
- [Advance Local Media v. Cohere - RAG Lawsuit](https://copyrightlately.com/court-rules-ai-news-summaries-may-infringe-copyright/)
- [Advance Local Media v. Cohere - MTD Denied](https://www.newsmediaalliance.org/judge-denies-cohere-motion-to-dismiss/)
- [Getty Images v. Stability AI - UK Ruling](https://www.mayerbrown.com/en/insights/publications/2025/11/getty-images-v-stability-ai-what-the-high-courts-decision-means-for-rights-holders-and-ai-developers)
- [Authors Guild v. OpenAI - MTD Denied](https://www.cullenllp.com/blog/ai-lawsuits-are-coming-court-denies-openais-motion-to-dismiss-claims-that-chatgpt-infringed-game-of-thrones-authors-rights/)
- [OpenAI 20M Logs Discovery Order](https://natlawreview.com/article/openai-loses-privacy-gambit-20-million-chatgpt-logs-likely-headed-copyright-plaintiffs)
- [Status of All 51 AI Copyright Lawsuits](https://chatgptiseatingtheworld.com/2025/10/08/status-of-all-51-copyright-lawsuits-v-ai-oct-8-2025-no-more-decisions-on-fair-use-in-2025/)

### Government and Policy
- [U.S. Copyright Office Part 3 Report - AI Training (May 2025)](https://www.copyright.gov/ai/Copyright-and-Artificial-Intelligence-Part-3-Generative-AI-Training-Report-Pre-Publication-Version.pdf)
- [Copyright Office AI Study Portal](https://www.copyright.gov/policy/artificial-intelligence/)
- [Sidley Austin - Copyright Office Part 3 Analysis](https://www.sidley.com/en/insights/newsupdates/2025/05/generative-ai-meets-copyright-scrutiny)
- [Authors Guild - Copyright Office Report Analysis](https://authorsguild.org/news/us-copyright-office-ai-report-part-3-what-authors-should-know/)
- [Perkins Coie - Copyright Office Analysis](https://perkinscoie.com/insights/update/copyright-office-stakes-out-position-use-works-ai-training)
- [EU AI Act Copyright Consultations](https://www.insideprivacy.com/artificial-intelligence/european-commission-launches-consultations-on-the-eu-ai-acts-copyright-provisions-and-ai-regulatory-sandboxes/)
- [EU Digital Services Act](https://digital-strategy.ec.europa.eu/en/policies/digital-services-act)
- [EU Parliament Copyright Proposals](https://www.globalpolicywatch.com/2026/02/european-parliament-proposes-changes-to-copyright-protection-in-the-age-of-generative-ai/)

### Law Firm Analysis and Commentary
- [Norton Rose Fulbright - Copyright and AI Training](https://www.nortonrosefulbright.com/en/knowledge/publications/87200379/practical-commentary-regarding-copyright-and-generative-ai-training)
- [DMCA Safe Harbor and AI (2025)](https://patentpc.com/blog/dmca-safe-harbor-and-the-rise-of-ai-content-whats-changing-in-2025)
- [DMCA Safe Harbor for AI Platforms](https://patentpc.com/blog/dmca-safe-harbor-rules-and-their-application-to-ai-platforms)
- [AI Harbour Proposal (Oxford Academic)](https://academic.oup.com/jiplp/article/20/9/605/8221820)
- [RAG Copyright Concerns (36Kr)](https://eu.36kr.com/en/p/3422429684387205)
- [RAG Legal Considerations (Legal Foundations UK)](https://legalfoundations.org.uk/blog/legal-considerations-with-retrieval-augmented-generation-rag/)
- [Asia IP - RAG Copyright Analysis](https://www.asiaiplaw.com/section/in-depth/the-latest-rage-called-rag)
- [Perplexity RAG Copyright Paths](https://themediabrain.substack.com/p/perplexitys-use-of-rag-opens-up-3)
- [Fair Use in AI Training - Skadden Analysis](https://www.skadden.com/insights/publications/2025/07/fair-use-and-ai-training)
- [AI Copyright 2025 Year in Review](https://copyrightalliance.org/ai-copyright-lawsuit-developments-2025/)
- [TechPolicy - Missing Fair Use Argument for AI Summaries](https://www.techpolicy.press/the-missing-fair-use-argument-in-the-copyright-battle-over-ai-summaries/)

### Platform Terms and Industry
- [Google NotebookLM Terms](https://support.google.com/notebooklm/answer/16164461)
- [NotebookLM Additional Terms (via U. Oslo)](https://www.uio.no/english/services/it/ai/notebooklm/help/terms-and-conditions.html)
- [AI Platform Ownership Rules Comparison](https://terms.law/2025/04/09/navigating-ai-platform-policies-who-owns-ai-generated-content/)
- [AI TOS Fine Print Analysis](https://www.termsfeed.com/blog/ai-terms-service-fine-print/)
- [Westlaw and Lexis AI Updates](https://usfblogs.usfca.edu/ziefbrief/2026/02/02/legal-research-ai-update-latest-versions-from-westlaw-and-lexis/)
- [IPWatchdog - Licensing Vector for AI](https://ipwatchdog.com/2024/04/10/licensing-vector-fair-approach-content-use-llms-2/id=175202/)
- [AI Copyright Litigation to Licensing](https://ipwatchdog.com/2026/02/15/ai-copyright-how-lessons-litigation-pave-way-licensing/)

### Academic
- [Harvard JOLT - RAG for Legal Work](https://jolt.law.harvard.edu/digest/retrieval-augmented-generation-rag-towards-a-promising-llm-architecture-for-legal-work)
- [Copyright Safety for Generative AI (Houston Law Review)](https://houstonlawreview.org/article/92126-copyright-safety-for-generative-ai)
- [Penn State Law Review - AI and Copyright Fine Print](https://www.pennstatelawreview.org/wp-content/uploads/2025/05/1.-Kim_577-605.pdf)
- [Springer - Copyright and ML Model Lifecycle](https://link.springer.com/article/10.1007/s40319-023-01419-3)
- [CRS Report - Generative AI and Copyright Law](https://www.congress.gov/crs-product/LSB10922)

---

*Disclaimer: This research document is for informational purposes only and does not constitute legal advice. MeepleAI should consult with qualified intellectual property counsel before implementing any legal strategy.*
