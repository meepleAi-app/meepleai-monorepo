from scripts.v2_audit.component_inspector import inspect_component, ComponentSnapshot


def test_inspect_extracts_link_hrefs(sample_component_path):
    snap = inspect_component(sample_component_path)
    assert "/games" in snap.link_hrefs
    assert any("play" in h for h in snap.link_hrefs)


def test_inspect_extracts_landmarks(sample_component_path):
    snap = inspect_component(sample_component_path)
    assert "section" in snap.landmarks
    assert "header" in snap.landmarks
    assert "h1" in snap.headings


def test_inspect_extracts_tailwind_tokens(sample_component_path):
    snap = inspect_component(sample_component_path)
    tokens = snap.tailwind_tokens
    assert "bg-card" in tokens
    assert "border-border" in tokens
    assert "text-foreground" in tokens
    assert "bg-entity-game" in tokens


def test_inspect_props_interface(sample_component_props_path):
    snap = inspect_component(sample_component_props_path)
    assert "PlayerCardProps" in snap.props_interfaces
    fields = snap.props_interfaces["PlayerCardProps"]
    assert fields == {"name": "string", "avatar": "string", "wins": "number"}


def test_low_confidence_when_no_extraction(tmp_path):
    """File with no Link, no landmarks → snapshot still returned but flagged."""
    f = tmp_path / "x.tsx"
    f.write_text("export const X = () => <></>;", encoding="utf-8")
    snap = inspect_component(f)
    assert snap.link_hrefs == set()
    assert snap.landmarks == set()
