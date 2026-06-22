"""
Tests for ``scrub_bgg_manifest.py`` (issue #2123 BGG ToS compliance codemod).

Run with::

    cd scripts && python -m pytest tests/test_scrub_bgg_manifest.py -v
"""

from __future__ import annotations

import sys
import textwrap
from pathlib import Path

import pytest

# Make ``scripts/`` directory importable.
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import scrub_bgg_manifest as codemod  # noqa: E402


def _write(path: Path, content: str) -> None:
    path.write_text(textwrap.dedent(content).lstrip("\n"), encoding="utf-8")


# ---------------------------------------------------------------------------
# scrub()
# ---------------------------------------------------------------------------


def test_scrub_removes_all_5_target_fields(tmp_path: Path) -> None:
    src = tmp_path / "test.yml"
    _write(
        src,
        """
        catalog:
          games:
            - title: Catan
              bggId: 13
              imageUrl: https://cf.geekdo-images.com/foo.jpg
              thumbnailUrl: https://cf.geekdo-images.com/bar.jpg
              fallbackImageUrl: https://placehold.co/baz.jpg
              fallbackThumbnailUrl: https://placehold.co/qux.jpg
              bggEnhanced: true
              description: A game
              language: en
        """,
    )

    games, stripped = codemod.scrub(src)

    assert games == 1
    assert stripped == 5
    result = src.read_text(encoding="utf-8")
    assert "imageUrl" not in result
    assert "thumbnailUrl" not in result
    assert "fallbackImageUrl" not in result
    assert "fallbackThumbnailUrl" not in result
    assert "bggEnhanced" not in result


def test_scrub_preserves_non_target_fields(tmp_path: Path) -> None:
    src = tmp_path / "test.yml"
    _write(
        src,
        """
        catalog:
          games:
            - title: Catan
              bggId: 13
              language: en
              description: A game of trade
              yearPublished: 1995
              minPlayers: 3
              maxPlayers: 4
              imageUrl: https://cf.geekdo-images.com/foo.jpg
              categories:
                - Strategy
                - Economic
              mechanics:
                - Trading
                - Dice Rolling
              pdfBlobKey: rulebooks/v1/catan.pdf
              pdfSha256: deadbeef
        """,
    )

    games, stripped = codemod.scrub(src)

    assert games == 1
    assert stripped == 1
    result = src.read_text(encoding="utf-8")
    assert "title: Catan" in result
    assert "bggId: 13" in result
    assert "language: en" in result
    assert "description: A game of trade" in result
    assert "yearPublished: 1995" in result
    assert "minPlayers: 3" in result
    assert "maxPlayers: 4" in result
    assert "Strategy" in result
    assert "Trading" in result
    assert "pdfBlobKey: rulebooks/v1/catan.pdf" in result
    assert "pdfSha256: deadbeef" in result


def test_scrub_handles_multiple_games(tmp_path: Path) -> None:
    src = tmp_path / "test.yml"
    _write(
        src,
        """
        catalog:
          games:
            - title: Catan
              bggId: 13
              imageUrl: https://cf.geekdo-images.com/catan.jpg
              bggEnhanced: true
            - title: Wingspan
              bggId: 266192
              imageUrl: https://cf.geekdo-images.com/wingspan.jpg
              thumbnailUrl: https://cf.geekdo-images.com/wingspan_thumb.jpg
              fallbackImageUrl: https://placehold.co/wingspan.jpg
              bggEnhanced: false
            - title: Patchwork
              bggId: 163412
              language: en
        """,
    )

    games, stripped = codemod.scrub(src)

    assert games == 3
    assert stripped == 6  # Catan: 2 (imageUrl, bggEnhanced); Wingspan: 4; Patchwork: 0


def test_scrub_is_idempotent(tmp_path: Path) -> None:
    src = tmp_path / "test.yml"
    _write(
        src,
        """
        catalog:
          games:
            - title: Catan
              imageUrl: https://cf.geekdo-images.com/foo.jpg
              bggEnhanced: true
        """,
    )

    games1, stripped1 = codemod.scrub(src)
    games2, stripped2 = codemod.scrub(src)
    games3, stripped3 = codemod.scrub(src)

    assert games1 == 1 and stripped1 == 2
    assert games2 == 1 and stripped2 == 0
    assert games3 == 1 and stripped3 == 0


def test_scrub_handles_empty_manifest(tmp_path: Path) -> None:
    src = tmp_path / "test.yml"
    _write(
        src,
        """
        catalog:
          games: []
        """,
    )

    games, stripped = codemod.scrub(src)

    assert games == 0
    assert stripped == 0


def test_scrub_handles_missing_catalog_key(tmp_path: Path) -> None:
    src = tmp_path / "test.yml"
    _write(src, "profile: dev\n")

    games, stripped = codemod.scrub(src)

    assert games == 0
    assert stripped == 0


def test_scrub_raises_on_missing_file(tmp_path: Path) -> None:
    src = tmp_path / "does-not-exist.yml"
    with pytest.raises(FileNotFoundError):
        codemod.scrub(src)


def test_scrub_preserves_comments(tmp_path: Path) -> None:
    src = tmp_path / "test.yml"
    src.write_text(
        textwrap.dedent(
            """\
            # Top-level comment
            catalog:
              games:
                # Comment on Catan
                - title: Catan
                  bggId: 13
                  imageUrl: https://cf.geekdo-images.com/catan.jpg  # inline comment
                  description: classic
            """
        ),
        encoding="utf-8",
    )

    games, stripped = codemod.scrub(src)
    result = src.read_text(encoding="utf-8")

    assert games == 1
    assert stripped == 1
    assert "# Top-level comment" in result
    assert "# Comment on Catan" in result


# ---------------------------------------------------------------------------
# main() CLI
# ---------------------------------------------------------------------------


def test_main_processes_multiple_files(tmp_path: Path, capsys: pytest.CaptureFixture) -> None:
    file_a = tmp_path / "a.yml"
    file_b = tmp_path / "b.yml"
    _write(
        file_a,
        """
        catalog:
          games:
            - title: A
              imageUrl: https://cf.geekdo-images.com/a.jpg
        """,
    )
    _write(
        file_b,
        """
        catalog:
          games:
            - title: B
              thumbnailUrl: https://cf.geekdo-images.com/b.jpg
              fallbackImageUrl: x.jpg
        """,
    )

    rc = codemod.main([str(file_a), str(file_b)])

    captured = capsys.readouterr()
    assert rc == 0
    assert "1 games, 1 fields removed" in captured.out
    assert "1 games, 2 fields removed" in captured.out


def test_main_returns_1_on_error(tmp_path: Path, capsys: pytest.CaptureFixture) -> None:
    rc = codemod.main([str(tmp_path / "missing.yml")])

    captured = capsys.readouterr()
    assert rc == 1
    assert "ERROR" in captured.err


# ---------------------------------------------------------------------------
# Security guard — assert the unsafe PyYAML API is NOT used
# ---------------------------------------------------------------------------


def test_codemod_does_not_import_unsafe_yaml() -> None:
    """
    Regression guard: ensure the script never starts importing the vulnerable
    PyYAML ``yaml`` module. ``ruamel.yaml`` round-trip mode is the documented
    safe variant — see ``_parse_safely`` docstring in scrub_bgg_manifest.py.
    """
    source = Path(codemod.__file__).read_text(encoding="utf-8")
    # Allow the word in comments/docstrings, but no bare imports.
    assert "import yaml" not in source, "vulnerable PyYAML API must not be imported"
    assert "from yaml " not in source, "vulnerable PyYAML API must not be imported"
