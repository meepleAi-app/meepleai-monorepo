from scripts.mockup_demo.fuzzy_heuristic import score_candidates

CATALOG = [
    "sp4-games-index.html",
    "sp4-game-detail.html",
    "sp7-game-night-live.html",
    "sp4-discover.html",
]

def test_high_confidence_match():
    results = score_candidates("Discover", CATALOG)
    assert results[0][0] == "sp4-discover.html"
    assert results[0][1] >= 0.7

def test_low_confidence_match():
    results = score_candidates("Random unrelated text 123", CATALOG)
    assert results[0][1] < 0.7

def test_returns_sorted_descending():
    results = score_candidates("game", CATALOG)
    scores = [s for _, s in results]
    assert scores == sorted(scores, reverse=True)
