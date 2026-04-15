# Batch Embedding + RAG Test + Snapshot — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Embeddare tutti i 126 PDF rulebook locali, testare il RAG manualmente per gruppo, creare snapshot DB per seeding.

**Architecture:** Lo script `import-games.sh` gestisce la pipeline completa (BGG import -> download -> upload -> poll). Serve prima popolare il manifest con i 126 PDF locali, poi processare in 8 gruppi da ~16. Dopo ogni gruppo, test RAG manuale nel browser. Alla fine, `seed-index-dump.sh` produce lo snapshot.

**Tech Stack:** Bash (import-games.sh), jq, curl, PostgreSQL (pgvector), .NET API, Python embedding-service, SmolDocling

**Spec:** `docs/superpowers/specs/2026-04-14-batch-embedding-rag-test-snapshot-design.md`

---

## Prerequisiti

- Stack avviato con `make dev` (da `infra/`)
- Credenziali admin: `ADMIN_EMAIL` e `ADMIN_PASSWORD` impostati
- Servizi attivi: postgres, redis, api, embedding-service, smoldocling-service
- 126 PDF presenti in `data/rulebook/`

---

## Task 0: Diagnosi fallimenti baseline

**Files:**
- Read: `data/rulebook/manifest.json`
- Read: database `processing_jobs` table

**Obiettivo:** Capire perché 122/136 PDF fallivano nel baseline snapshot e verificare lo stato attuale.

- [ ] **Step 0.1: Verificare lo stack e' attivo**

```bash
cd infra && make dev
# Attendere che tutti i servizi siano healthy
curl -s http://localhost:8080/api/v1/health | jq .
```

Expected: `{"status":"Healthy"}`

- [ ] **Step 0.2: Query errori processing_jobs**

```bash
pwsh -c "docker exec meepleai-postgres psql -U meepleai -d meepleai_dev -c \"
  SELECT status, COUNT(*) as cnt
  FROM processing_jobs
  GROUP BY status
  ORDER BY cnt DESC;
\""
```

- [ ] **Step 0.3: Dettaglio errori per categoria**

```bash
pwsh -c "docker exec meepleai-postgres psql -U meepleai -d meepleai_dev -c \"
  SELECT pj.status, LEFT(pj.error_message, 120) as error_preview, COUNT(*) as cnt
  FROM processing_jobs pj
  WHERE pj.status = 'Failed'
  GROUP BY pj.status, LEFT(pj.error_message, 120)
  ORDER BY cnt DESC
  LIMIT 20;
\""
```

- [ ] **Step 0.4: Verificare servizi Python**

```bash
curl -s http://localhost:8000/health | jq .   # embedding-service
curl -s http://localhost:8001/health | jq .   # smoldocling-service
```

- [ ] **Step 0.5: Annotare cause e decidere fix**

Categorie probabili:
- Embedding service down -> verificare docker logs
- SmolDocling timeout -> aumentare timeout o riavviare
- PDF corrotto -> escludere dal manifest
- OOM -> aumentare RAM Docker Desktop

```bash
pwsh -c "docker logs meepleai-embedding-service --tail=50"
pwsh -c "docker logs meepleai-smoldocling-service --tail=50"
```

---

## Task 1: Popolare manifest.json con i 126 PDF locali

**Files:**
- Create: `data/rulebook/populate-manifest.sh`
- Modify: `data/rulebook/manifest.json`

**Obiettivo:** Aggiungere al manifest tutti i 126 PDF locali che non hanno ancora un'entry.

- [ ] **Step 1.1: Creare script di popolamento**

```bash
cat > data/rulebook/populate-manifest.sh << 'SCRIPT'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MANIFEST="${SCRIPT_DIR}/manifest.json"

# Mappa BGG ID per i giochi noti (slug -> bggId)
# Fonte: boardgamegeek.com
declare -A BGG_IDS=(
  ["7-wonders"]=68448
  ["7-wonders-duel"]=173346
  ["aeons-end"]=191189
  ["a-feast-for-odin"]=177736
  ["age-of-innovation"]=363369
  ["agricola"]=31260
  ["agricola-revised"]=200680
  ["android-netrunner"]=124742
  ["arkham-horror-tcg"]=205637
  ["ark-nova"]=342942
  ["azul"]=230802
  ["betrayal-at-house-on-the-hill"]=10547
  ["brass-birmingham"]=224517
  ["brass-lancashire"]=28720
  ["carcassone"]=822
  ["cascadia"]=295947
  ["castles-of-burgundy"]=84876
  ["catan_en"]=13
  ["century-spice-road"]=209685
  ["clank"]=201808
  ["clank-legacy"]=266507
  ["codenames"]=178900
  ["concordia"]=124361
  ["coup"]=131357
  ["cthulhu-death-may-die"]=253344
  ["darwins-journey"]=307890
  ["descent"]=104162
  ["dixit"]=39856
  ["dixit-odyssey"]=92828
  ["dominion-prosperity"]=66690
  ["dune-imperium"]=316554
  ["dune-imperium-uprising"]=397598
  ["eclipse"]=72125
  ["eclipse-second-dawn"]=246900
  ["el-grande"]=93
  ["everdell"]=199792
  ["exploding-kittens"]=172225
  ["fields-of-arle"]=159675
  ["final-girl"]=277659
  ["five-tribes"]=157354
  ["food-chain-magnate"]=175914
  ["forbidden-island"]=65244
  ["frosthaven"]=295770
  ["gaia-project"]=220308
  ["gloom"]=12692
  ["gloomhaven"]=174430
  ["gloomhaven-jaws-of-the-lion"]=291457
  ["great-western-trail"]=193738
  ["great-western-trail-argentina"]=363144
  ["guillotine"]=1123
  ["hanabi"]=98778
  ["hanamikoji"]=158600
  ["harmonies"]=405317
  ["heat"]=366013
  ["hegemony"]=321608
  ["hero-realms"]=198994
  ["hive-pocket"]=154597
  ["imperial-settlers"]=154203
  ["jaipur"]=54043
  ["kanban-ev"]=284378
  ["king-of-tokyo"]=70323
  ["kingdomino"]=204583
  ["le-havre"]=35677
  ["lost-ruins-of-arnak"]=312484
  ["lotr-card-game"]=77423
  ["love-letter"]=129622
  ["maracaibo"]=276025
  ["mechs-vs-minions"]=209010
  ["munchkin"]=1927
  ["mysterium"]=181304
  ["nemesis"]=167355
  ["orleans"]=164928
  ["paladins-of-the-west-kingdom"]=266810
  ["pandemic"]=30549
  ["pandemic-legacy-s1"]=161936
  ["pandemic-legacy-s2"]=221107
  ["patchwork"]=163412
  ["pax-pamir"]=256960
  ["photosynthesis"]=218603
  ["power-grid"]=2651
  ["puerto-rico"]=3076
  ["quacks-of-quedlinburg"]=244521
  ["race-for-the-galaxy"]=28143
  ["raiders-of-the-north-sea"]=170042
  ["res-arcana"]=262712
  ["revive"]=332772
  ["roll-player"]=169426
  ["root"]=237182
  ["sagrada"]=199561
  ["santorini"]=194655
  ["scacchi-fide_2017"]=171
  ["scythe"]=169786
  ["seti"]=301784
  ["skytear"]=262377
  ["slay-the-spire"]=338960
  ["sleeping-gods"]=255984
  ["small-world"]=40692
  ["spirit-island"]=162886
  ["splendor"]=148228
  ["spot-it"]=63268
  ["star-wars-imperial-assault"]=164153
  ["star-wars-rebellion"]=187645
  ["survive-escape-from-atlantis"]=2653
  ["sushi-go"]=133473
  ["terra-mystica"]=120677
  ["terraforming-mars"]=167791
  ["the-crew"]=284083
  ["the-gallerist"]=125153
  ["the-white-castle"]=371942
  ["through-the-ages"]=182028
  ["ticket-to-ride"]=9209
  ["ticket-to-ride-europe"]=14996
  ["too-many-bones"]=192135
  ["twilight-imperium-4e"]=233078
  ["twilight-struggle"]=12333
  ["underwater-cities"]=247763
  ["villainous"]=256382
  ["viticulture"]=183394
  ["voidfall"]=337627
  ["war-of-the-ring"]=115746
  ["wingspan_en"]=266192
  ["wingspan-asia"]=366161
)

# [FIX I2] Mappa nomi corretti per slug che producono nomi errati con auto-capitalize
declare -A DISPLAY_NAMES=(
  ["7-wonders"]="7 Wonders"
  ["7-wonders-duel"]="7 Wonders Duel"
  ["a-feast-for-odin"]="A Feast for Odin"
  ["android-netrunner"]="Android: Netrunner"
  ["arkham-horror-tcg"]="Arkham Horror: The Card Game"
  ["ark-nova"]="Ark Nova"
  ["betrayal-at-house-on-the-hill"]="Betrayal at House on the Hill"
  ["brass-birmingham"]="Brass: Birmingham"
  ["brass-lancashire"]="Brass: Lancashire"
  ["castles-of-burgundy"]="The Castles of Burgundy"
  ["catan_en"]="Catan"
  ["century-spice-road"]="Century: Spice Road"
  ["clank-legacy"]="Clank! Legacy"
  ["cthulhu-death-may-die"]="Cthulhu: Death May Die"
  ["darwins-journey"]="Darwin's Journey"
  ["dixit-odyssey"]="Dixit Odyssey"
  ["dominion-prosperity"]="Dominion: Prosperity"
  ["dune-imperium"]="Dune: Imperium"
  ["dune-imperium-uprising"]="Dune: Imperium - Uprising"
  ["eclipse-second-dawn"]="Eclipse: Second Dawn for the Galaxy"
  ["el-grande"]="El Grande"
  ["fields-of-arle"]="Fields of Arle"
  ["food-chain-magnate"]="Food Chain Magnate"
  ["gaia-project"]="Gaia Project"
  ["gloomhaven-jaws-of-the-lion"]="Gloomhaven: Jaws of the Lion"
  ["great-western-trail"]="Great Western Trail"
  ["great-western-trail-argentina"]="Great Western Trail: Argentina"
  ["hero-realms"]="Hero Realms"
  ["hive-pocket"]="Hive Pocket"
  ["imperial-settlers"]="Imperial Settlers"
  ["kanban-ev"]="Kanban EV"
  ["king-of-tokyo"]="King of Tokyo"
  ["le-havre"]="Le Havre"
  ["lost-ruins-of-arnak"]="Lost Ruins of Arnak"
  ["lotr-card-game"]="The Lord of the Rings: The Card Game"
  ["love-letter"]="Love Letter"
  ["marvel-champions"]="Marvel Champions: The Card Game"
  ["mechs-vs-minions"]="Mechs vs. Minions"
  ["paladins-of-the-west-kingdom"]="Paladins of the West Kingdom"
  ["pandemic-legacy-s1"]="Pandemic Legacy: Season 1"
  ["pandemic-legacy-s2"]="Pandemic Legacy: Season 2"
  ["pax-pamir"]="Pax Pamir (Second Edition)"
  ["power-grid"]="Power Grid"
  ["quacks-of-quedlinburg"]="The Quacks of Quedlinburg"
  ["race-for-the-galaxy"]="Race for the Galaxy"
  ["raiders-of-the-north-sea"]="Raiders of the North Sea"
  ["res-arcana"]="Res Arcana"
  ["roll-player"]="Roll Player"
  ["scacchi-fide_2017"]="Chess (FIDE 2017 Rules)"
  ["slay-the-spire"]="Slay the Spire: The Board Game"
  ["sleeping-gods"]="Sleeping Gods"
  ["small-world"]="Small World"
  ["spirit-island"]="Spirit Island"
  ["spot-it"]="Spot It!"
  ["star-wars-imperial-assault"]="Star Wars: Imperial Assault"
  ["star-wars-rebellion"]="Star Wars: Rebellion"
  ["survive-escape-from-atlantis"]="Survive: Escape from Atlantis!"
  ["sushi-go"]="Sushi Go!"
  ["terra-mystica"]="Terra Mystica"
  ["terraforming-mars"]="Terraforming Mars"
  ["the-crew"]="The Crew: The Quest for Planet Nine"
  ["the-gallerist"]="The Gallerist"
  ["the-white-castle"]="The White Castle"
  ["through-the-ages"]="Through the Ages: A New Story of Civilization"
  ["ticket-to-ride"]="Ticket to Ride"
  ["ticket-to-ride-europe"]="Ticket to Ride: Europe"
  ["too-many-bones"]="Too Many Bones"
  ["twilight-imperium-4e"]="Twilight Imperium (Fourth Edition)"
  ["twilight-struggle"]="Twilight Struggle"
  ["underwater-cities"]="Underwater Cities"
  ["war-of-the-ring"]="War of the Ring (Second Edition)"
  ["wingspan_en"]="Wingspan"
  ["wingspan-asia"]="Wingspan: Asia"
)

# Leggi il prossimo ID disponibile
NEXT_ID=$(jq '[.games[].id] | max + 1' "$MANIFEST")

# Ottieni slugs gia' nel manifest
EXISTING_SLUGS=$(jq -r '.games[].slug' "$MANIFEST")

# [FIX M1] Costruisci array JSON di nuovi giochi in memoria, poi merge una volta sola
NEW_GAMES="[]"
ADDED=0

for pdf in "${SCRIPT_DIR}"/*_rulebook.pdf; do
  slug=$(basename "$pdf" | sed 's/_rulebook\.pdf$//')

  # Skip se gia' nel manifest
  if echo "$EXISTING_SLUGS" | grep -qx "$slug"; then
    continue
  fi

  # [FIX I2] Nome leggibile: usa DISPLAY_NAMES se disponibile, altrimenti auto-capitalize
  if [[ -n "${DISPLAY_NAMES[$slug]:-}" ]]; then
    name="${DISPLAY_NAMES[$slug]}"
  else
    name=$(echo "$slug" | sed 's/-/ /g; s/_/ /g' | sed 's/\b\(.\)/\u\1/g')
  fi

  # BGG ID
  bgg_id="${BGG_IDS[$slug]:-0}"
  bgg_found="true"
  if [[ "$bgg_id" -eq 0 ]]; then
    bgg_found="false"
  fi

  lang="en"
  echo "Adding: $slug (id=$NEXT_ID, bggId=$bgg_id, name=$name)"

  # [FIX C1] No 'local' keyword — siamo fuori da una funzione
  NEW_GAMES=$(echo "$NEW_GAMES" | jq \
     --argjson id "$NEXT_ID" \
     --arg name "$name" \
     --arg slug "$slug" \
     --argjson bggId "$bgg_id" \
     --argjson bggFound "$bgg_found" \
     --arg lang "$lang" \
     --arg pdfLocal "data/rulebook/${slug}_rulebook.pdf" \
     '. += [{
       "id": $id,
       "name": $name,
       "slug": $slug,
       "bggId": $bggId,
       "bggFound": $bggFound,
       "language": $lang,
       "pdfUrl": null,
       "pdfLocal": $pdfLocal,
       "pdfStatus": "local",
       "isExpansion": false,
       "baseGameSlug": null,
       "sharedGameId": null,
       "documentId": null,
       "indexingStatus": null,
       "metadata": {
         "yearPublished": 2020,
         "minPlayers": 1,
         "maxPlayers": 4,
         "playingTimeMinutes": 60,
         "minAge": 14,
         "designers": [],
         "publishers": [],
         "categories": [],
         "mechanics": []
       }
     }]')

  NEXT_ID=$((NEXT_ID + 1))
  ADDED=$((ADDED + 1))
done

# [FIX M1] Merge una sola volta nel manifest
if [[ "$ADDED" -gt 0 ]]; then
  tmp="${MANIFEST}.tmp"
  jq --argjson new "$NEW_GAMES" '.games += $new' "$MANIFEST" > "$tmp" && mv "$tmp" "$MANIFEST"
fi

echo "Done. Added $ADDED new games to manifest."
echo "Total games: $(jq '.games | length' "$MANIFEST")"
SCRIPT
chmod +x data/rulebook/populate-manifest.sh
```

- [ ] **Step 1.2: Eseguire lo script**

```bash
cd data/rulebook && bash populate-manifest.sh
```

Expected: `Done. Added ~107 new games to manifest. Total games: ~126`

- [ ] **Step 1.3: Fix 4 PDF sovrapposti (barrage, dominion, mage-knight, marvel-champions)**

Questi 4 slug esistono gia' nel manifest con `pdfStatus: "to_download"` ma hanno PDF locale.
Aggiornare il manifest per usare i file locali:

```bash
cd data/rulebook
for slug in barrage dominion mage-knight marvel-champions; do
  if [[ -f "${slug}_rulebook.pdf" ]]; then
    tmp="manifest.json.tmp"
    jq --arg s "$slug" --arg p "data/rulebook/${slug}_rulebook.pdf" \
      '(.games[] | select(.slug == $s)) |= (.pdfStatus = "local" | .pdfLocal = $p)' \
      manifest.json > "$tmp" && mv "$tmp" manifest.json
    echo "Fixed: $slug -> local"
  fi
done
```

- [ ] **Step 1.4: Verificare il manifest aggiornato**

```bash
jq '.games | length' data/rulebook/manifest.json
jq '[.games[] | select(.pdfStatus == "local")] | length' data/rulebook/manifest.json
jq '[.games[] | select(.pdfStatus == "to_download")] | length' data/rulebook/manifest.json
```

Expected: ~126 total, ~126 local, 0 (o pochi) to_download

- [ ] **Step 1.5: Commit**

```bash
git add data/rulebook/populate-manifest.sh data/rulebook/manifest.json
git commit -m "chore(rulebook): populate manifest with 126 local PDF entries"
```

---

## Task 2: Creare script di import per gruppo

**Files:**
- Create: `data/rulebook/import-group.sh`
- Create: `data/rulebook/rag-test-results.md`

**Obiettivo:** Script wrapper che esegue `import-games.sh` per un sottoinsieme di giochi, filtrando per ID range.

- [ ] **Step 2.1: Creare lo script di import per gruppo**

```bash
cat > data/rulebook/import-group.sh << 'SCRIPT'
#!/usr/bin/env bash
set -euo pipefail

# Usage: ./import-group.sh <from_id> <to_id>
# Example: ./import-group.sh 20 35   (processes games with id 20-35)

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <from_id> <to_id>"
  echo "Example: $0 20 35"
  exit 1
fi

FROM_ID=$1
TO_ID=$2

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MANIFEST="${SCRIPT_DIR}/manifest.json"
BACKUP="${MANIFEST}.bak-$(date +%Y%m%d%H%M%S)"

# Backup manifest
cp "$MANIFEST" "$BACKUP"
echo "Manifest backed up to: $BACKUP"

# Conta giochi nel range
COUNT=$(jq --argjson from "$FROM_ID" --argjson to "$TO_ID" \
  '[.games[] | select(.id >= $from and .id <= $to)] | length' "$MANIFEST")

echo "=== Processing group: ID $FROM_ID - $TO_ID ($COUNT games) ==="
echo ""

# Mostra giochi nel range
jq --argjson from "$FROM_ID" --argjson to "$TO_ID" -r \
  '.games[] | select(.id >= $from and .id <= $to) | "\(.id)\t\(.slug)\t\(.pdfStatus)\t\(.indexingStatus // "-")"' \
  "$MANIFEST"

echo ""
echo "Press Enter to start import, Ctrl+C to cancel..."
read -r

# Crea manifest temporaneo con solo i giochi del gruppo
TEMP_MANIFEST="${MANIFEST}.group"
jq --argjson from "$FROM_ID" --argjson to "$TO_ID" \
  '.games = [.games[] | select(.id >= $from and .id <= $to)]' \
  "$MANIFEST" > "$TEMP_MANIFEST"

# Esegui import-games.sh con manifest temporaneo
# Override MANIFEST path
MANIFEST_OVERRIDE="$TEMP_MANIFEST" bash "${SCRIPT_DIR}/import-games.sh"

# [FIX C4] Merge atomico: una sola operazione jq invece di field-by-field
echo "Merging results back to main manifest..."
tmp="${MANIFEST}.tmp"
jq -s '
  .[0] as $main | .[1].games as $updates |
  $main | .games = [.games[] | . as $g |
    ($updates | map(select(.id == $g.id)) | first) // $g
  ]
' "$MANIFEST" "$TEMP_MANIFEST" > "$tmp" && mv "$tmp" "$MANIFEST"

rm -f "$TEMP_MANIFEST"
echo "Done. Results merged."

# Report del gruppo
echo ""
echo "=== Group Report ==="
jq --argjson from "$FROM_ID" --argjson to "$TO_ID" -r \
  '.games[] | select(.id >= $from and .id <= $to) | "\(.id)\t\(.slug)\t\(.indexingStatus // "-")"' \
  "$MANIFEST"
SCRIPT
chmod +x data/rulebook/import-group.sh
```

- [ ] **Step 2.2: Creare file tracking risultati RAG**

```bash
cat > data/rulebook/rag-test-results.md << 'EOF'
# RAG Test Results

Data inizio: 2026-04-14

## Legenda
- **Pass**: risposta pertinente + citazioni dal PDF corretto
- **Fail**: risposta generica, citazioni vuote/sbagliate, o errore
- **Skip**: embedding fallito, non testabile

## Risultati per gruppo

### Gruppo 1 (ID 20-35): Piccoli/semplici

| ID | Gioco | Embedding | RAG Test | Note |
|----|-------|-----------|----------|------|

### Gruppo 2 (ID 36-51): Medi classici

| ID | Gioco | Embedding | RAG Test | Note |
|----|-------|-----------|----------|------|

### Gruppo 3 (ID 52-67): Medi strategici

| ID | Gioco | Embedding | RAG Test | Note |
|----|-------|-----------|----------|------|

### Gruppo 4 (ID 68-83): Complessi

| ID | Gioco | Embedding | RAG Test | Note |
|----|-------|-----------|----------|------|

### Gruppo 5 (ID 84-99): Molto complessi

| ID | Gioco | Embedding | RAG Test | Note |
|----|-------|-----------|----------|------|

### Gruppo 6 (ID 100-115): Mega

| ID | Gioco | Embedding | RAG Test | Note |
|----|-------|-----------|----------|------|

### Gruppo 7 (ID 116-131): Card games / LCG

| ID | Gioco | Embedding | RAG Test | Note |
|----|-------|-----------|----------|------|

### Gruppo 8 (ID 132+): Residui + retry

| ID | Gioco | Embedding | RAG Test | Note |
|----|-------|-----------|----------|------|

## Riepilogo

| Gruppo | Totale | Ready | Failed | Pass | Fail |
|--------|--------|-------|--------|------|------|
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |
| 4 | | | | | |
| 5 | | | | | |
| 6 | | | | | |
| 7 | | | | | |
| 8 | | | | | |
| **Totale** | | | | | |
EOF
```

- [ ] **Step 2.3: Commit**

```bash
git add data/rulebook/import-group.sh data/rulebook/rag-test-results.md
git commit -m "chore(rulebook): add group import script and RAG test tracking"
```

---

## Task 3: Adattare import-games.sh per supportare manifest override

**Files:**
- Modify: `data/rulebook/import-games.sh`

**Obiettivo:** Permettere a `import-group.sh` di passare un manifest temporaneo.

- [ ] **Step 3.1: Aggiungere supporto MANIFEST_OVERRIDE**

In `data/rulebook/import-games.sh`, modificare la riga 11:

```bash
# Riga 11 attuale:
MANIFEST="${SCRIPT_DIR}/manifest.json"

# Sostituire con:
MANIFEST="${MANIFEST_OVERRIDE:-${SCRIPT_DIR}/manifest.json}"
```

- [ ] **Step 3.2: Verificare che lo script funziona ancora normalmente**

```bash
cd data/rulebook
# Dry run: solo health check + login (Ctrl+C dopo login)
ADMIN_EMAIL=admin@meepleai.com ADMIN_PASSWORD=<password> bash import-games.sh
```

- [ ] **Step 3.3: Commit**

```bash
git add data/rulebook/import-games.sh
git commit -m "feat(rulebook): support MANIFEST_OVERRIDE env var in import-games.sh"
```

---

## Task 4-11: Processare i gruppi (ripetere per ogni gruppo)

**Il ciclo e' identico per ogni gruppo. Qui documentiamo il template.**

### Template per ogni gruppo N

- [ ] **Step N.1: Lanciare l'import del gruppo**

```bash
cd data/rulebook
ADMIN_EMAIL=admin@meepleai.com ADMIN_PASSWORD=<password> \
  bash import-group.sh <FROM_ID> <TO_ID>
```

Mapping gruppi — **[FIX I3] Questi range sono provvisori. Dopo Task 1, gli ID saranno assegnati
in ordine alfabetico (glob order). Ricalcolare i range in base agli ID reali e riordinare
per complessita' se desiderato. Usare questo comando per vedere gli ID assegnati:**

```bash
jq -r '.games[] | select(.id >= 20) | "\(.id)\t\(.slug)"' data/rulebook/manifest.json | sort -n
```

| Gruppo | FROM_ID | TO_ID | Giochi stimati |
|--------|---------|-------|----------------|
| 1 | 20 | 35 | ~16 piccoli |
| 2 | 36 | 51 | ~16 medi classici |
| 3 | 52 | 67 | ~16 medi strategici |
| 4 | 68 | 83 | ~16 complessi |
| 5 | 84 | 99 | ~16 molto complessi |
| 6 | 100 | 115 | ~16 mega |
| 7 | 116 | 131 | ~16 card games |
| 8 | 132 | 145 | residui + retry |

- [ ] **Step N.2: Attendere completamento**

Lo script `import-games.sh` fa poll automatico (5 min timeout per gioco).
Monitorare i log se serve:

```bash
pwsh -c "docker logs meepleai-api --tail=20 -f"
pwsh -c "docker logs meepleai-embedding-service --tail=20 -f"
```

- [ ] **Step N.3: Verificare stato del gruppo**

```bash
jq --argjson from <FROM_ID> --argjson to <TO_ID> -r \
  '.games[] | select(.id >= $from and .id <= $to) | "\(.slug)\t\(.indexingStatus)"' \
  data/rulebook/manifest.json
```

- [ ] **Step N.4: Test RAG manuale**

Per ogni gioco con `indexingStatus=Completed`:

1. Apri `http://localhost:3000`
2. Vai al gioco nel catalogo
3. Avvia chat con agente RAG
4. Chiedi: "Quanti giocatori possono giocare? Come si prepara il gioco?"
5. Verifica: risposta pertinente + citazioni dal PDF

Alternativa curl (richiede re-login — i cookies vengono eliminati dopo import-games.sh):

```bash
# [FIX I1] Re-login necessario: import-games.sh cancella i cookies al termine
COOKIES="data/rulebook/.cookies-ragtest"
curl -s -c "$COOKIES" -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" \
  http://localhost:8080/api/v1/auth/login > /dev/null

# Sostituire GAME_ID con lo sharedGameId dal manifest
GAME_ID=$(jq -r '.games[] | select(.slug == "<SLUG>") | .sharedGameId' data/rulebook/manifest.json)
curl -s -b "$COOKIES" \
  -H "Content-Type: application/json" \
  -d "{\"gameId\":\"$GAME_ID\",\"query\":\"How many players can play? How do you set up the game?\",\"language\":\"en\"}" \
  http://localhost:8080/api/v1/knowledge-base/ask | jq '{answer: .answer[0:200], citations: [.citations[]?.snippet[0:80]]}'

rm -f "$COOKIES"
```

- [ ] **Step N.5: Aggiornare rag-test-results.md**

Compilare la tabella del gruppo con i risultati dei test.

- [ ] **Step N.6: Retry falliti (se necessario)**

```bash
# Retry di tutti i pending/failed
curl -s -b .cookies -X POST http://localhost:8080/api/v1/admin/pdfs/process-pending
```

- [ ] **Step N.7: Commit progressi**

```bash
git add data/rulebook/manifest.json data/rulebook/rag-test-results.md
git commit -m "chore(rulebook): complete group N embedding + RAG test"
```

---

## Task 12: Snapshot finale

**Files:**
- Run: `infra/scripts/seed-index-dump.sh`
- Output: `data/snapshots/meepleai_seed_<timestamp>_*.dump`

- [ ] **Step 12.1: Verificare stato globale**

```bash
pwsh -c "docker exec meepleai-postgres psql -U meepleai -d meepleai_dev -c \"
  SELECT status, COUNT(*) FROM processing_jobs GROUP BY status ORDER BY COUNT(*) DESC;
\""
```

Verificare che Ready >= 85% (target: < 15% falliti).

- [ ] **Step 12.2: Conteggio chunk e embedding**

```bash
pwsh -c "docker exec meepleai-postgres psql -U meepleai -d meepleai_dev -c \"
  SELECT
    (SELECT COUNT(*) FROM text_chunks) as chunks,
    (SELECT COUNT(*) FROM pgvector_embeddings) as embeddings;
\""
```

- [ ] **Step 12.3: Eseguire il dump**

```bash
cd infra && bash scripts/seed-index-dump.sh
```

Expected output: basename dello snapshot creato (es. `meepleai_seed_20260414T...`)

- [ ] **Step 12.4: Verificare snapshot metadata**

```bash
# [FIX I4] I campi reali nel meta.json sono pdf_count, failed_pdf_ids (non pdf_count_ready/failed)
ls -t data/snapshots/*.meta.json | head -1 | xargs jq '{
  pdf_count,
  chunk_count,
  embedding_count,
  embedding_model,
  embedding_dim,
  failed_count: (.failed_pdf_ids | length),
  ready_games_count: (.ready_games | length)
}'
```

- [ ] **Step 12.5: Aggiornare .latest**

```bash
# Il dump script dovrebbe farlo automaticamente.
# Verificare:
cat data/snapshots/.latest
```

- [ ] **Step 12.6: Test restore su ambiente pulito**

```bash
cd infra
make dev-from-snapshot-force
# Attendere boot completo
curl -s http://localhost:8080/api/v1/health | jq .
```

Poi verificare un gioco random via RAG:

```bash
# Login + test ask endpoint per un gioco a caso
```

- [ ] **Step 12.7: Commit finale**

```bash
git add data/snapshots/*.meta.json data/rulebook/manifest.json data/rulebook/rag-test-results.md
git commit -m "chore(snapshot): new baseline with ~120 embedded rulebooks"
```

- [ ] **Step 12.8: (Opzionale) Pubblicare snapshot**

```bash
cd infra && make seed-index-publish
```

---

## Task 13: Cleanup

- [ ] **Step 13.1: Aggiungere pattern temp a .gitignore**

```bash
# [FIX M2] Evitare commit accidentale di backup e file temporanei
cat >> data/rulebook/.gitignore << 'EOF'
manifest.json.bak-*
manifest.json.tmp
manifest.json.group
.cookies
.cookies-ragtest
EOF
```

- [ ] **Step 13.2: Rimuovere file temporanei**

```bash
rm -f data/rulebook/manifest.json.bak-*
rm -f data/rulebook/manifest.json.tmp
rm -f data/rulebook/manifest.json.group
rm -f data/rulebook/.cookies
rm -f data/rulebook/.cookies-ragtest
```

- [ ] **Step 13.3: Commit cleanup**

```bash
git add data/rulebook/.gitignore
git commit -m "chore(rulebook): add gitignore for temp files and cleanup"
```

---

## Procedura di Rollback

**[FIX M3]** Se qualcosa va storto durante il processo:

### Rollback manifest
```bash
# Ogni import-group.sh crea un backup automatico
ls -lt data/rulebook/manifest.json.bak-* | head -5
# Ripristinare l'ultimo backup funzionante:
cp data/rulebook/manifest.json.bak-<TIMESTAMP> data/rulebook/manifest.json
```

### Rollback database (torna al baseline)
```bash
cd infra
make dev-from-snapshot-force
# Ripristina il baseline snapshot (14 giochi) e riavvia lo stack
```

### Rollback parziale (rimuovere un gruppo fallito)
```bash
# Identificare i processing_jobs del gruppo fallito e reimpostarli
pwsh -c "docker exec meepleai-postgres psql -U meepleai -d meepleai_dev -c \"
  UPDATE processing_jobs SET status = 'Pending', error_message = NULL
  WHERE pdf_document_id IN (
    SELECT id FROM pdf_documents WHERE file_name LIKE '%<slug>%'
  );
\""
# Poi ritriggare: POST /api/v1/admin/pdfs/process-pending
```

---

## Riepilogo gruppi e tempi stimati

| Task | Descrizione | Tempo stimato |
|------|-------------|---------------|
| 0 | Diagnosi fallimenti | 30-60 min |
| 1 | Popolare manifest | 15 min |
| 2 | Script import gruppo | 15 min |
| 3 | Adattare import-games.sh | 5 min |
| 4 | Gruppo 1 (piccoli) | 30 min embed + 15 min test |
| 5 | Gruppo 2 (medi classici) | 45 min + 15 min |
| 6 | Gruppo 3 (medi strategici) | 45 min + 15 min |
| 7 | Gruppo 4 (complessi) | 60 min + 15 min |
| 8 | Gruppo 5 (molto complessi) | 60 min + 15 min |
| 9 | Gruppo 6 (mega) | 90 min + 15 min |
| 10 | Gruppo 7 (card games) | 60 min + 15 min |
| 11 | Gruppo 8 (residui) | 60 min + 15 min |
| 12 | Snapshot | 30 min |
| 13 | Cleanup | 5 min |
| **Totale** | | **~10-14 ore** |

Il lavoro e' distribuibile su piu' sessioni. Ogni gruppo e' indipendente dopo il Task 3.
