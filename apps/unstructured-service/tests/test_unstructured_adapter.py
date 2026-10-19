"""Unit tests for the language field handling of UnstructuredAdapter (#4435).

The API sends one form field, `language`, with Tesseract codes joined by '+'
(e.g. 'ita+eng+deu': the document's language first, then the rest of its set).
The adapter must hand partition_pdf a list of codes, not one '+'-joined string.
"""
from unittest.mock import patch

import pytest

from src.config.settings import Settings
from src.infrastructure.unstructured_adapter import UnstructuredAdapter, parse_languages


class TestLanguageField:
    @patch("src.infrastructure.unstructured_adapter.partition_pdf")
    def test_plus_joined_field_reaches_partition_pdf_as_a_list(self, mock_partition, temp_pdf_file):
        mock_partition.return_value = []

        UnstructuredAdapter().partition_pdf(temp_pdf_file, strategy="hi_res", language="eng+ita+deu")

        _, kwargs = mock_partition.call_args
        assert kwargs["languages"] == ["eng", "ita", "deu"]

    @patch("src.infrastructure.unstructured_adapter.partition_pdf")
    def test_order_is_kept_the_first_code_is_the_primary(self, mock_partition, temp_pdf_file):
        mock_partition.return_value = []

        UnstructuredAdapter().partition_pdf(temp_pdf_file, strategy="hi_res", language="ita+eng+deu")

        _, kwargs = mock_partition.call_args
        assert kwargs["languages"] == ["ita", "eng", "deu"]

    @pytest.mark.parametrize(
        "value, expected",
        [
            ("deu", ["deu"]),
            (" ita + eng ", ["ita", "eng"]),
            ("eng++ita", ["eng", "ita"]),
        ],
    )
    def test_parse_languages(self, value, expected):
        assert parse_languages(value) == expected

    @pytest.mark.parametrize("value", ["", "   ", "+"])
    def test_blank_field_falls_back_to_the_multilingual_default(self, value):
        assert parse_languages(value, default="eng+ita+deu") == ["eng", "ita", "deu"]


def test_defaults_are_the_multilingual_set_not_ita():
    # Read the declared default, not the live Settings: pydantic-settings maps `language` to the
    # LANGUAGE environment variable, which is also a gettext locale variable on many hosts.
    assert Settings.model_fields["language"].default == "eng+ita+deu"
    assert UnstructuredAdapter.partition_pdf.__defaults__[-1] == "eng+ita+deu"
