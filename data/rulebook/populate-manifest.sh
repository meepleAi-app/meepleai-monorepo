#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# populate-manifest.sh
#
# Scans data/rulebook/*_rulebook.pdf and populates manifest.json with entries
# for any PDF not already present. Also fixes overlapping entries that may have
# pdfStatus != "local" despite having a local PDF.
#
# Usage:  cd <repo-root>/data/rulebook && bash populate-manifest.sh
#         OR  bash data/rulebook/populate-manifest.sh   (from repo root)
###############################################################################

# ---------------------------------------------------------------------------
# Resolve paths
# ---------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MANIFEST="$SCRIPT_DIR/manifest.json"
PDF_DIR="$SCRIPT_DIR"

if [[ ! -f "$MANIFEST" ]]; then
  echo "ERROR: manifest.json not found at $MANIFEST" >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# BGG ID map  (slug -> bggId)
# ---------------------------------------------------------------------------
declare -A BGG_IDS=(
  [7-wonders]=68448
  [7-wonders-duel]=173346
  [aeons-end]=191189
  [a-feast-for-odin]=177736
  [age-of-innovation]=363369
  [agricola]=31260
  [agricola-revised]=200680
  [android-netrunner]=124742
  [arkham-horror-tcg]=205637
  [ark-nova]=342942
  [azul]=230802
  [barrage]=26872
  [betrayal-at-house-on-the-hill]=10547
  [brass-birmingham]=224517
  [brass-lancashire]=28720
  [carcassone]=822
  [cascadia]=295947
  [castles-of-burgundy]=84876
  [catan_en]=13
  [century-spice-road]=209685
  [clank]=201808
  [clank-legacy]=266507
  [codenames]=178900
  [concordia]=124361
  [coup]=131357
  [cthulhu-death-may-die]=253344
  [darwins-journey]=307890
  [descent]=104162
  [dixit]=39856
  [dixit-odyssey]=92828
  [dominion]=36218
  [dominion-prosperity]=66690
  [dune-imperium]=316554
  [dune-imperium-uprising]=397598
  [eclipse]=72125
  [eclipse-second-dawn]=246900
  [el-grande]=93
  [everdell]=199792
  [exploding-kittens]=172225
  [fields-of-arle]=159675
  [final-girl]=277659
  [five-tribes]=157354
  [food-chain-magnate]=175914
  [forbidden-island]=65244
  [frosthaven]=295770
  [gaia-project]=220308
  [gloom]=12692
  [gloomhaven]=174430
  [gloomhaven-jaws-of-the-lion]=291457
  [great-western-trail]=193738
  [great-western-trail-argentina]=363144
  [guillotine]=1123
  [hanabi]=98778
  [hanamikoji]=158600
  [harmonies]=405317
  [heat]=366013
  [hegemony]=321608
  [hero-realms]=198994
  [hive-pocket]=154597
  [imperial-settlers]=154203
  [jaipur]=54043
  [kanban-ev]=284378
  [king-of-tokyo]=70323
  [kingdomino]=204583
  [le-havre]=35677
  [lost-ruins-of-arnak]=312484
  [lotr-card-game]=77423
  [love-letter]=129622
  [mage-knight]=154852
  [maracaibo]=276025
  [marvel-champions]=285774
  [mechs-vs-minions]=209010
  [munchkin]=1927
  [mysterium]=181304
  [nemesis]=167355
  [orleans]=164928
  [paladins-of-the-west-kingdom]=266810
  [pandemic]=30549
  [pandemic-legacy-s1]=161936
  [pandemic-legacy-s2]=221107
  [patchwork]=163412
  [pax-pamir]=256960
  [photosynthesis]=218603
  [power-grid]=2651
  [puerto-rico]=3076
  [quacks-of-quedlinburg]=244521
  [race-for-the-galaxy]=28143
  [raiders-of-the-north-sea]=170042
  [res-arcana]=262712
  [revive]=332772
  [roll-player]=169426
  [root]=237182
  [sagrada]=199561
  [santorini]=194655
  [scacchi-fide_2017]=171
  [scythe]=169786
  [seti]=301784
  [skytear]=262377
  [slay-the-spire]=338960
  [sleeping-gods]=255984
  [small-world]=40692
  [spirit-island]=162886
  [splendor]=148228
  [spot-it]=63268
  [star-wars-imperial-assault]=164153
  [star-wars-rebellion]=187645
  [survive-escape-from-atlantis]=2653
  [sushi-go]=133473
  [terra-mystica]=120677
  [terraforming-mars]=167791
  [the-crew]=284083
  [the-gallerist]=125153
  [the-white-castle]=371942
  [through-the-ages]=182028
  [ticket-to-ride]=9209
  [ticket-to-ride-europe]=14996
  [too-many-bones]=192135
  [twilight-imperium-4e]=233078
  [twilight-struggle]=12333
  [underwater-cities]=247763
  [villainous]=256382
  [viticulture]=183394
  [voidfall]=337627
  [war-of-the-ring]=115746
  [wingspan_en]=266192
  [wingspan-asia]=366161
)

# ---------------------------------------------------------------------------
# DISPLAY_NAMES map  (slug -> human-friendly name)
# Only slugs whose auto-capitalize would be wrong.
# ---------------------------------------------------------------------------
declare -A DISPLAY_NAMES=(
  [7-wonders]="7 Wonders"
  [7-wonders-duel]="7 Wonders Duel"
  [a-feast-for-odin]="A Feast for Odin"
  [android-netrunner]="Android: Netrunner"
  [arkham-horror-tcg]="Arkham Horror: The Card Game"
  [ark-nova]="Ark Nova"
  [betrayal-at-house-on-the-hill]="Betrayal at House on the Hill"
  [brass-birmingham]="Brass: Birmingham"
  [brass-lancashire]="Brass: Lancashire"
  [castles-of-burgundy]="The Castles of Burgundy"
  [catan_en]="Catan"
  [century-spice-road]="Century: Spice Road"
  [clank-legacy]="Clank! Legacy"
  [cthulhu-death-may-die]="Cthulhu: Death May Die"
  [darwins-journey]="Darwin's Journey"
  [dominion-prosperity]="Dominion: Prosperity"
  [dune-imperium]="Dune: Imperium"
  [dune-imperium-uprising]="Dune: Imperium - Uprising"
  [eclipse-second-dawn]="Eclipse: Second Dawn for the Galaxy"
  [el-grande]="El Grande"
  [fields-of-arle]="Fields of Arle"
  [food-chain-magnate]="Food Chain Magnate"
  [gaia-project]="Gaia Project"
  [gloomhaven-jaws-of-the-lion]="Gloomhaven: Jaws of the Lion"
  [great-western-trail]="Great Western Trail"
  [great-western-trail-argentina]="Great Western Trail: Argentina"
  [hero-realms]="Hero Realms"
  [hive-pocket]="Hive Pocket"
  [imperial-settlers]="Imperial Settlers"
  [kanban-ev]="Kanban EV"
  [king-of-tokyo]="King of Tokyo"
  [le-havre]="Le Havre"
  [lost-ruins-of-arnak]="Lost Ruins of Arnak"
  [lotr-card-game]="The Lord of the Rings: The Card Game"
  [love-letter]="Love Letter"
  [mage-knight]="Mage Knight Board Game"
  [marvel-champions]="Marvel Champions: The Card Game"
  [mechs-vs-minions]="Mechs vs. Minions"
  [paladins-of-the-west-kingdom]="Paladins of the West Kingdom"
  [pandemic-legacy-s1]="Pandemic Legacy: Season 1"
  [pandemic-legacy-s2]="Pandemic Legacy: Season 2"
  [pax-pamir]="Pax Pamir (Second Edition)"
  [power-grid]="Power Grid"
  [quacks-of-quedlinburg]="The Quacks of Quedlinburg"
  [race-for-the-galaxy]="Race for the Galaxy"
  [raiders-of-the-north-sea]="Raiders of the North Sea"
  [res-arcana]="Res Arcana"
  [roll-player]="Roll Player"
  [scacchi-fide_2017]="Chess (FIDE 2017 Rules)"
  [slay-the-spire]="Slay the Spire: The Board Game"
  [sleeping-gods]="Sleeping Gods"
  [small-world]="Small World"
  [spirit-island]="Spirit Island"
  [spot-it]="Spot It!"
  [star-wars-imperial-assault]="Star Wars: Imperial Assault"
  [star-wars-rebellion]="Star Wars: Rebellion"
  [survive-escape-from-atlantis]="Survive: Escape from Atlantis!"
  [sushi-go]="Sushi Go!"
  [terra-mystica]="Terra Mystica"
  [terraforming-mars]="Terraforming Mars"
  [the-crew]="The Crew: The Quest for Planet Nine"
  [the-gallerist]="The Gallerist"
  [the-white-castle]="The White Castle"
  [through-the-ages]="Through the Ages: A New Story of Civilization"
  [ticket-to-ride]="Ticket to Ride"
  [ticket-to-ride-europe]="Ticket to Ride: Europe"
  [too-many-bones]="Too Many Bones"
  [twilight-imperium-4e]="Twilight Imperium (Fourth Edition)"
  [twilight-struggle]="Twilight Struggle"
  [underwater-cities]="Underwater Cities"
  [war-of-the-ring]="War of the Ring (Second Edition)"
  [wingspan_en]="Wingspan"
  [wingspan-asia]="Wingspan: Asia"
)

# ---------------------------------------------------------------------------
# Helper: auto-capitalize a slug  (e.g. "forbidden-island" -> "Forbidden Island")
# ---------------------------------------------------------------------------
auto_capitalize() {
  echo "$1" | sed 's/[-_]/ /g' | sed 's/\b\(.\)/\u\1/g'
}

# ---------------------------------------------------------------------------
# Helper: get display name for slug
# ---------------------------------------------------------------------------
get_display_name() {
  _slug="$1"
  if [[ -n "${DISPLAY_NAMES[$_slug]+x}" ]]; then
    echo "${DISPLAY_NAMES[$_slug]}"
  else
    auto_capitalize "$_slug"
  fi
}

# ---------------------------------------------------------------------------
# Collect existing slugs from manifest
# ---------------------------------------------------------------------------
EXISTING_SLUGS=$(jq -r '.games[].slug' "$MANIFEST")

# ---------------------------------------------------------------------------
# Get max existing ID
# ---------------------------------------------------------------------------
MAX_ID=$(jq '[.games[].id] | max' "$MANIFEST")
NEXT_ID=$((MAX_ID + 1))

echo "=== populate-manifest.sh ==="
echo "Manifest:       $MANIFEST"
echo "Max existing ID: $MAX_ID"
echo ""

# ---------------------------------------------------------------------------
# Build JSON array of new entries (written to temp file to avoid arg-list limits)
# ---------------------------------------------------------------------------
NEW_ENTRIES_FILE="${MANIFEST}.new_entries.json"
echo "[]" > "$NEW_ENTRIES_FILE"
ADDED=0
SKIPPED=0

for pdf in "$PDF_DIR"/*_rulebook.pdf; do
  filename="$(basename "$pdf")"
  slug="${filename%_rulebook.pdf}"

  # Skip if already in manifest
  if echo "$EXISTING_SLUGS" | grep -qx "$slug"; then
    SKIPPED=$((SKIPPED + 1))
    continue
  fi

  # Resolve BGG ID
  bgg_id="null"
  bgg_found="false"
  if [[ -n "${BGG_IDS[$slug]+x}" ]]; then
    bgg_id="${BGG_IDS[$slug]}"
    bgg_found="true"
  fi

  # Resolve display name
  display_name="$(get_display_name "$slug")"

  # Build JSON entry and append to temp file
  jq -n \
    --argjson id "$NEXT_ID" \
    --arg name "$display_name" \
    --arg slug "$slug" \
    --argjson bggId "$bgg_id" \
    --argjson bggFound "$bgg_found" \
    --arg pdfLocal "data/rulebook/${slug}_rulebook.pdf" \
    '{
      id: $id,
      name: $name,
      slug: $slug,
      bggId: $bggId,
      bggFound: $bggFound,
      language: "en",
      pdfUrl: null,
      pdfLocal: $pdfLocal,
      pdfStatus: "local",
      isExpansion: false,
      baseGameSlug: null,
      sharedGameId: null,
      documentId: null,
      indexingStatus: null,
      metadata: {
        yearPublished: null,
        minPlayers: null,
        maxPlayers: null,
        playingTimeMinutes: null,
        minAge: null,
        designers: [],
        publishers: [],
        categories: [],
        mechanics: []
      }
    }' > "${NEW_ENTRIES_FILE}.entry"

  jq --slurpfile entry "${NEW_ENTRIES_FILE}.entry" '. + $entry' "$NEW_ENTRIES_FILE" > "${NEW_ENTRIES_FILE}.tmp"
  mv "${NEW_ENTRIES_FILE}.tmp" "$NEW_ENTRIES_FILE"

  NEXT_ID=$((NEXT_ID + 1))
  ADDED=$((ADDED + 1))
  echo "  + [$((NEXT_ID - 1))] $slug  ($display_name)"
done

rm -f "${NEW_ENTRIES_FILE}.entry"

echo ""
echo "New entries:  $ADDED"
echo "Skipped:      $SKIPPED (already in manifest)"

# ---------------------------------------------------------------------------
# Fix overlapping entries: ensure pdfStatus=local and pdfLocal is set
# ---------------------------------------------------------------------------
FIXED=$(jq '[.games[] | select(.slug == "barrage" or .slug == "dominion" or .slug == "mage-knight" or .slug == "marvel-champions") | select(.pdfStatus != "local")] | length' "$MANIFEST")
echo "Overlapping fixes needed: $FIXED"

# ---------------------------------------------------------------------------
# Single jq merge: append new entries + fix overlapping
# ---------------------------------------------------------------------------
TEMP_MANIFEST="${MANIFEST}.tmp"

jq --slurpfile new_entries "$NEW_ENTRIES_FILE" '
  # Append new entries
  .games += $new_entries[0]
  # Fix overlapping entries
  | .games = [.games[] |
      if (.slug == "barrage" or .slug == "dominion" or .slug == "mage-knight" or .slug == "marvel-champions")
      then . + { pdfStatus: "local", pdfLocal: ("data/rulebook/" + .slug + "_rulebook.pdf") }
      else .
      end
    ]
' "$MANIFEST" > "$TEMP_MANIFEST"

mv "$TEMP_MANIFEST" "$MANIFEST"
rm -f "$NEW_ENTRIES_FILE"

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
TOTAL=$(jq '.games | length' "$MANIFEST")
echo ""
echo "=== Done ==="
echo "Total games in manifest: $TOTAL"
echo "Manifest updated: $MANIFEST"
