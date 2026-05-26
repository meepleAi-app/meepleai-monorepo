from scripts.v2_audit.finding import Finding, Severity, Confidence, Dimension


def test_finding_creation():
    f = Finding(
        dimension=Dimension.NAV,
        severity=Severity.CRITICAL,
        confidence=Confidence.HIGH,
        component="GameDetailHero.tsx",
        route="/games/[id]",
        description="Missing Link to game-onboarding",
        evidence={"expected": "librogame-runthrough-game-onboarding.html", "actual": None, "line": 42},
    )
    assert f.dimension == "nav"
    assert f.severity == "critical"
    assert f.confidence == "high"


def test_severity_ordering():
    # Critical > Important > Minor for sorting
    assert Severity.CRITICAL.weight > Severity.IMPORTANT.weight > Severity.MINOR.weight


def test_dimension_string_values():
    assert Dimension.NAV.value == "nav"
    assert Dimension.STRUCTURAL.value == "structural"
    assert Dimension.TOKENS.value == "tokens"
    assert Dimension.PROPS.value == "props"
