"""Tests for language detection with majority voting"""
import pytest
from src.application.language_detector import detect_document_language, LanguageResult


class TestLanguageDetector:
    def test_detect_italian(self):
        chunks = [
            "Il giocatore pesca una carta dal mazzo e la aggiunge alla propria mano durante il suo turno.",
            "Durante il proprio turno, ogni giocatore può eseguire fino a tre azioni diverse sul tabellone.",
            "La partita termina quando un giocatore raggiunge venti punti vittoria sul tracciato segnapunti.",
        ]
        result = detect_document_language(chunks)
        assert result.language == "it"
        assert result.confidence >= 0.8

    def test_detect_english(self):
        chunks = [
            "The player draws a card from the deck and adds it to their hand during their turn.",
            "During their turn, each player may perform up to three different actions on the board.",
            "The game ends when a player reaches twenty victory points on the score track.",
        ]
        result = detect_document_language(chunks)
        assert result.language == "en"
        assert result.confidence >= 0.8

    def test_detect_german(self):
        chunks = [
            "Der Spieler zieht eine Karte vom Stapel und nimmt sie auf die Hand während seines Zuges.",
            "Während seines Zuges kann jeder Spieler bis zu drei verschiedene Aktionen auf dem Spielbrett ausführen.",
        ]
        result = detect_document_language(chunks)
        assert result.language == "de"
        assert result.confidence >= 0.8

    def test_empty_chunks_fallback_to_english(self):
        result = detect_document_language([])
        assert result.language == "en"
        assert result.confidence == 0.0

    def test_short_chunks_skipped(self):
        result = detect_document_language(["Hi", "OK", "No", "Yes"])
        assert result.language == "en"
        assert result.confidence == 0.0

    def test_deterministic_results(self):
        chunks = [
            "This is a test sentence with enough length to be detected properly by the algorithm."
        ]
        result1 = detect_document_language(chunks)
        result2 = detect_document_language(chunks)
        assert result1.language == result2.language
        assert result1.confidence == result2.confidence

    def test_mixed_language_majority_wins(self):
        chunks = [
            "Il giocatore pesca una carta dal mazzo e la aggiunge alla propria mano durante il suo turno.",
            "Durante il proprio turno, ogni giocatore può eseguire fino a tre azioni diverse sul tabellone.",
            "The player draws a card from the deck.",  # one English chunk
            "La partita termina quando un giocatore raggiunge venti punti vittoria sul tracciato segnapunti.",
        ]
        result = detect_document_language(chunks)
        assert result.language == "it"  # Italian majority

    def test_returns_language_result_dataclass(self):
        result = detect_document_language([])
        assert isinstance(result, LanguageResult)
        assert hasattr(result, "language")
        assert hasattr(result, "confidence")

    def test_max_ten_chunks_processed(self):
        """Only first 10 chunks should be processed even if more are provided."""
        # 15 English chunks - should still work (just uses first 10)
        chunks = [
            f"This is English sentence number {i} with enough text to be detected properly."
            for i in range(15)
        ]
        result = detect_document_language(chunks)
        assert result.language == "en"
        assert result.confidence >= 0.8

    def test_custom_min_chunk_length(self):
        """Chunks shorter than min_chunk_length should be skipped."""
        chunks = [
            "Short text here",  # 15 chars - below default 50 but above custom 10
            "Another short one",
        ]
        # With default min_chunk_length=50, these are skipped
        result_default = detect_document_language(chunks, min_chunk_length=50)
        assert result_default.confidence == 0.0

        # With lower threshold, they should be processed
        result_low = detect_document_language(chunks, min_chunk_length=5)
        assert result_low.confidence > 0.0
