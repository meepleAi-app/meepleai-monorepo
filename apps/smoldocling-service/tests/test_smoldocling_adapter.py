"""
Tests for SmolDoclingAdapter DocTags decoding — fix P1/P2 + review hardening (issue #3435).

Context (spec v1.3 §5ter + adversarial review):
  - P1: decode with skip_special_tokens=False + trim prompt + strip ONLY chat/control
        tokens, so DocTags markup (<loc_*>, <otsl>, <fcel>) survives for region grounding.
  - P2: has_tables checks <otsl> (OTSL), not <table>.
  - #6: build the chat prompt via apply_chat_template — the real Idefics3Processor only
        emits input_ids when text= is supplied, so the images-only call produced no
        input_ids and broke generation + the prompt trim.
  - #2/#3: content-free / conversion-failed pages must count as empty, never leak markup.
  - #4/#8/#10: confidence/has_equations aligned to real DocTags tags (<otsl>/<formula>).

The 256M model is never loaded — lightweight fakes stand in for processor/model, mirroring
the mock pattern in test_metrics.py. The fakes intentionally reproduce two real contracts:
  (a) the processor yields input_ids ONLY when text is not None (Idefics3Processor);
  (b) batch_decode is a function of the ids it receives (so trim/leak is observable).
"""
import torch
import pytest
from PIL import Image

from src.infrastructure.smoldocling_adapter import SmolDoclingAdapter
from src.domain.models import PageImage, PageExtractionResult


# Realistic raw decode (skip_special_tokens=False): DocTags markup + OTSL table wrapped in
# the chat/control tokens that must be stripped.
RAW_DOCTAGS = (
    "<|im_start|>"
    "<doctag>"
    "<text><loc_44><loc_43><loc_461><loc_59>Campo arato vale 1 punto</text>"
    "<otsl><loc_50><loc_100><loc_450><loc_300>"
    "<fcel>Campo arato<fcel>1<nl><fcel>Pascolo<fcel>1<nl></otsl>"
    "</doctag>"
    "<end_of_utterance><|im_end|>"
)

# Text-only page (no OTSL table): a text element with location tokens.
TEXT_ONLY_DOCTAGS = (
    "<|im_start|><doctag>"
    "<text><loc_10><loc_10><loc_490><loc_60>Alcune regole del gioco.</text>"
    "</doctag><end_of_utterance><|im_end|>"
)

FORMULA_DOCTAGS = (
    "<|im_start|><doctag>"
    "<formula><loc_10><loc_10><loc_200><loc_60>E = m c^2</formula>"
    "</doctag><end_of_utterance>"
)

DOLLAR_NARRATIVE_DOCTAGS = (
    "<|im_start|><doctag>"
    "<paragraph><loc_10><loc_10><loc_490><loc_60>Paga $5 alla banca.</paragraph>"
    "</doctag><end_of_utterance>"
)


class _FakeBatch(dict):
    """Mimics a transformers BatchFeature: dict-like + a no-op .to(device)."""

    def to(self, device):
        return self


class _SpyProcessor:
    """Stand-in for the SmolDocling Idefics3Processor.

    Reproduces the real contract: input_ids is returned ONLY when text is not None.
    Records the chat-template call, the text/images passed, and the ids + skip flag
    handed to batch_decode. batch_decode is a function of the ids so the prompt trim
    and any leak are observable.
    """

    def __init__(self, prompt_len: int = 3, output: str = RAW_DOCTAGS):
        self.prompt_len = prompt_len
        self.output = output
        self.chat_template_called = False
        self.call_text = None
        self.call_images = None
        self.decode_skip_special = None
        self.decoded_ids = None

    def apply_chat_template(self, messages, add_generation_prompt=False, **kwargs):
        self.chat_template_called = True
        return "PROMPT_STRING"

    def __call__(self, text=None, images=None, return_tensors=None, **kwargs):
        self.call_text = text
        self.call_images = images
        batch = _FakeBatch(pixel_values=torch.zeros(1, 3, 8, 8))
        # Idefics3Processor: input_ids only present when a text prompt is supplied
        if text is not None:
            batch["input_ids"] = torch.arange(1, self.prompt_len + 1).unsqueeze(0)
        return batch

    def batch_decode(self, ids, skip_special_tokens=True, **kwargs):
        self.decode_skip_special = skip_special_tokens
        seq = ids[0].tolist() if hasattr(ids, "__getitem__") else list(ids)
        self.decoded_ids = seq
        if len(seq) == 0:
            return [""]  # trimmed-to-empty degenerate case
        # any prompt id (<= prompt_len) surviving means the prompt leaked into the decode
        if any(t <= self.prompt_len for t in seq):
            return ["<PROMPT_LEAK>" + self.output]
        return [self.output]


class _FakeModel:
    def __init__(self, prompt_len: int = 3, gen_len: int = 12):
        self.prompt_len = prompt_len
        self.gen_len = gen_len

    def generate(self, **kwargs):
        total = self.prompt_len + self.gen_len
        return torch.arange(1, total + 1).unsqueeze(0)


def _make_adapter(processor, model):
    adapter = SmolDoclingAdapter()
    adapter.processor = processor
    adapter.model = model
    adapter._is_initialized = True
    adapter.device = "cpu"
    return adapter


def _page():
    return PageImage.from_pil_image(1, Image.new("RGB", (1000, 1400), "white"), dpi=300)


# --------------------------------------------------------------- P1: clean/decode

def test_clean_doctags_strips_control_tokens_but_keeps_doctags():
    adapter = SmolDoclingAdapter()
    cleaned = adapter._clean_doctags(RAW_DOCTAGS)
    for control in ("<|im_start|>", "<|im_end|>", "<end_of_utterance>"):
        assert control not in cleaned, f"{control} should be stripped"
    assert "<loc_44>" in cleaned
    assert "<otsl>" in cleaned
    assert "<fcel>" in cleaned
    assert "<doctag>" in cleaned


def test_process_page_decodes_without_skipping_special_and_trims_prompt():
    proc = _SpyProcessor(prompt_len=3)
    adapter = _make_adapter(proc, _FakeModel(prompt_len=3, gen_len=12))

    result = adapter.process_page(_page())

    assert proc.decode_skip_special is False               # P1a: keep special tokens
    assert proc.decoded_ids == list(range(4, 16))          # P1b: prompt trimmed off
    assert "<loc_44>" in result.doctags_text               # P1c: markup preserved
    assert "<otsl>" in result.doctags_text
    assert "<|im_end|>" not in result.doctags_text          # control tokens gone
    assert "<end_of_utterance>" not in result.doctags_text


# --------------------------------------------------------------- #6: chat template

def test_process_page_builds_chat_prompt_and_passes_text_to_processor():
    # The real Idefics3Processor needs text= to emit input_ids; assert the adapter
    # builds the prompt (apply_chat_template) and passes a non-None text.
    proc = _SpyProcessor(prompt_len=3)
    adapter = _make_adapter(proc, _FakeModel())
    adapter.process_page(_page())
    assert proc.chat_template_called is True
    assert proc.call_text is not None
    assert proc.call_images is not None


# --------------------------------------------------------------- #1: degenerate path

def test_process_page_no_prompt_leak_when_generation_empty():
    # gen_len=0: generate returns only the prompt; trim yields an empty sequence and the
    # decode must be empty (no prompt/instruction text leaking into doctags_text).
    proc = _SpyProcessor(prompt_len=5)
    adapter = _make_adapter(proc, _FakeModel(prompt_len=5, gen_len=0))
    result = adapter.process_page(_page())
    assert proc.decoded_ids == []                 # trimmed to empty, not the prompt ids
    assert result.doctags_text == ""              # no <PROMPT_LEAK>, no prompt text
    assert result.is_empty is True


# --------------------------------------------------------------- P2 + #9: tables/conf

def test_has_tables_detects_otsl_not_html_table():
    adapter = SmolDoclingAdapter()
    assert adapter._detect_has_tables("<doctag><otsl><fcel>x<nl></otsl></doctag>") is True
    assert adapter._detect_has_tables("<doctag><table><tr><td>x</td></tr></table>") is False
    assert adapter._detect_has_tables("<doctag><paragraph>plain</paragraph></doctag>") is False


def test_process_page_sets_has_tables_true_and_confidence_rewards_otsl():
    adapter = _make_adapter(_SpyProcessor(), _FakeModel())
    result = adapter.process_page(_page())
    assert result.has_tables is True
    # base 0.7 + OTSL table bonus 0.1 — the old <table> check never fired (#9)
    assert round(result.confidence_score, 2) >= 0.8


# --------------------------------------------------------------- #10: equations

def test_has_equations_true_for_formula_tag():
    adapter = _make_adapter(_SpyProcessor(output=FORMULA_DOCTAGS), _FakeModel())
    assert adapter.process_page(_page()).has_equations is True


def test_has_equations_false_for_dollar_amount_narrative():
    # "$5" in narrative must NOT be flagged as an equation (old heuristic false-positive)
    adapter = _make_adapter(_SpyProcessor(output=DOLLAR_NARRATIVE_DOCTAGS), _FakeModel())
    assert adapter.process_page(_page()).has_equations is False


# --------------------------------------------------------------- #5: no markup leak

def test_text_only_page_markdown_has_no_doctags_markup():
    adapter = _make_adapter(_SpyProcessor(output=TEXT_ONLY_DOCTAGS), _FakeModel())
    result = adapter.process_page(_page())
    assert result.has_tables is False
    for token in ("<loc_", "<otsl>", "<doctag>", "<text>"):
        assert token not in result.markdown_text, f"{token} leaked into markdown_text"
    assert "Alcune regole del gioco" in result.markdown_text


# --------------------------------------------------------------- #3: fallback safety

def test_convert_to_markdown_fallback_returns_empty_not_raw_doctags(monkeypatch):
    import src.infrastructure.smoldocling_adapter as mod

    def boom(*a, **k):
        raise ValueError("docling parse failure")

    monkeypatch.setattr(mod.DocTagsDocument, "from_doctags_and_image_pairs", boom)
    adapter = SmolDoclingAdapter()
    out = adapter._convert_to_markdown("<doctag><otsl><fcel>x<nl></otsl></doctag>")
    assert out == ""                    # never emit raw DocTags as content
    assert "<otsl>" not in out
    assert "<loc_" not in out


def test_cleaned_doctags_convert_to_markdown_rebuilds_table():
    # Integration with the REAL docling-core (no model): cleaned DocTags round-trip into
    # a markdown table, proving the fix yields usable content (spec §5ter).
    adapter = SmolDoclingAdapter()
    md = adapter._convert_to_markdown(adapter._clean_doctags(RAW_DOCTAGS))
    assert "Campo arato" in md
    assert "|" in md


# --------------------------------------------------------------- #2: is_empty semantics

def test_is_empty_true_for_markup_only_page():
    # A blank page emits only the structural wrapper; markdown is empty → empty page.
    r = PageExtractionResult(
        page_number=1, doctags_text="<doctag></doctag>", markdown_text="",
        char_count=0, has_tables=False, has_equations=False, confidence_score=0.7,
    )
    assert r.is_empty is True


def test_is_empty_false_for_real_content():
    r = PageExtractionResult(
        page_number=1, doctags_text="<doctag><paragraph>hi</paragraph></doctag>",
        markdown_text="hi", char_count=2, has_tables=False, has_equations=False,
        confidence_score=0.8,
    )
    assert r.is_empty is False
