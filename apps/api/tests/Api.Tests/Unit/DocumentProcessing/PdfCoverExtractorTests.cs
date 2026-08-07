using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// Unit tests for <see cref="PdfCoverExtractor"/> — issue #1831 (umbrella #1821 L4).
/// Focused on the early-return failure modes; the heuristic + rendering paths
/// require a real PDF fixture and are covered by integration tests (Testcontainers).
/// </summary>
public class PdfCoverExtractorTests
{
    private readonly PdfCoverExtractor _sut = new(NullLogger<PdfCoverExtractor>.Instance);

    [Fact]
    public async Task ExtractAsync_ReturnsFailed_WhenBytesAreNull()
    {
        var result = await _sut.ExtractAsync(null!, CancellationToken.None);

        Assert.Equal(PdfCoverExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.ErrorMessage);
        Assert.Null(result.ThumbnailWebp);
        Assert.Null(result.PreviewWebp);
        Assert.Null(result.SelectedPageIndex);
    }

    [Fact]
    public async Task ExtractAsync_ReturnsFailed_WhenBytesAreEmpty()
    {
        var result = await _sut.ExtractAsync(System.Array.Empty<byte>(), CancellationToken.None);

        Assert.Equal(PdfCoverExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractAsync_ReturnsFailed_OnGarbageBytes()
    {
        // Random bytes that are definitely not a PDF — Docnet should throw and
        // the service should map the exception to a Failed result.
        var garbage = new byte[] { 0x42, 0x43, 0x44, 0x45, 0x00, 0xFF, 0x10, 0x20 };

        var result = await _sut.ExtractAsync(garbage, CancellationToken.None);

        Assert.Equal(PdfCoverExtractionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.ErrorMessage);
        Assert.True(result.ErrorMessage!.Length <= 500, "Error message must be truncated to ≤500 chars to fit DB column");
    }

    [Fact]
    public async Task ExtractAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var garbage = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF" magic, but truncated

        // Cancellation thrown BEFORE Docnet processes — the service either
        // throws OperationCanceledException (preferred) or returns Failed with
        // a cancellation-derived message. Both behaviours acceptable for the
        // pipeline caller; assert the operation didn't claim success.
        var result = await _sut.ExtractAsync(garbage, cts.Token).ContinueWith(t =>
            t.IsCanceled
                ? new PdfCoverExtractionResult { Outcome = PdfCoverExtractionOutcome.Failed, ErrorMessage = "cancelled" }
                : t.Result);

        Assert.NotEqual(PdfCoverExtractionOutcome.Generated, result.Outcome);
    }

    // -- Euristica di selezione pagina (#3590) --------------------------------
    //
    // Fino a questo punto la classe copriva solo i percorsi d'errore: l'euristica
    // non era esercitata da alcun test, ed e' il motivo per cui la sua taratura ha
    // potuto rigettare 27 dei 28 PDF poi risultati Skipped in staging senza che
    // nulla lo segnalasse. Le fixture sono PDF generati con caratteristiche
    // misurate, incorporati in base64 per non dipendere da file esterni.

    private const string COVER_WITH_TEXT =
        "JVBERi0xLjcKJcK1wrYKCjEgMCBvYmoKPDwvVHlwZS9DYXRhbG9nL1BhZ2VzIDIgMCBSPj4KZW5kb2JqCgoyIDAgb2JqCjw8L1R5cGUvUGFnZX"
        + "MvQ291bnQgMS9LaWRzWzQgMCBSXT4+CmVuZG9iagoKMyAwIG9iago8PC9Gb250PDwvaGVsdiA3IDAgUj4+Pj4KZW5kb2JqCgo0IDAgb2JqCjw8"
        + "L1R5cGUvUGFnZS9NZWRpYUJveFswIDAgMzAwIDQyMF0vUm90YXRlIDAvUmVzb3VyY2VzIDMgMCBSL1BhcmVudCAyIDAgUi9Db250ZW50c1s1ID"
        + "AgUiA2IDAgUiA4IDAgUl0+PgplbmRvYmoKCjUgMCBvYmoKPDwvTGVuZ3RoIDQyPj4Kc3RyZWFtCgpxCjAgMCAzMDAgNDIwIHJlCmgKMSAxIDEg"
        + "UkcgMSAxIDEgcmcgQgpRCgplbmRzdHJlYW0KZW5kb2JqCgo2IDAgb2JqCjw8L0xlbmd0aCA0OS9GaWx0ZXIvRmxhdGVEZWNvZGU+PgpzdHJlYW"
        + "0KeNrjKuQyUDBQMDYwUDAxMlAoSuXK4NIzVNAzNFXQM1YIcldAcIrSFZy4ArkA8X8JowplbmRzdHJlYW0KZW5kb2JqCgo3IDAgb2JqCjw8L1R5"
        + "cGUvRm9udC9TdWJ0eXBlL1R5cGUxL0Jhc2VGb250L0hlbHZldGljYS9FbmNvZGluZy9XaW5BbnNpRW5jb2Rpbmc+PgplbmRvYmoKCjggMCBvYm"
        + "oKPDwvTGVuZ3RoIDU1MC9GaWx0ZXIvRmxhdGVEZWNvZGU+PgpzdHJlYW0KeNrll82O1DAMx+/zFH0CE9uJPyTEAQkhcQPNDXHaduAABzjw/Djp"
        + "sBIbENnZw3S2qiqlTuLkZyvxv4fvh9fHA04pHpwoTewMpUzHb9OLL8vXn5NMx1P01OfD23Pjx+fp48t8JyclKTJTEtekrGs7y0laH4UtenLYUG"
        + "YprcXRu4S9CGuuNi11ZIzI1Uu8HGNcFtGwlvDUPLQZefXe7B7zOLyeqrV6kjlmt1Xb7Ooj1zd2tTQP6/p3sRKtO3/16fjuD3TLIKY6AH+GuUeV"
        + "El9zuNZYGhtO3TjG4ni/dGwkNjSvsG28/7aonHuxfdV5EYgYs4T3CJSSVvyF0iVh70C1gFHJI6Abylf189iwd+gi4MI4lONr5eupx6SDLgaYko"
        + "1AXzlflxyTDjc7ENvIJbaNfF1+THr0BKxCA+jXy9fTjkmHzAgF2Ueyvcky9L+wd8BEIAXLIPGG6tDj4t5xI4Oa89jVvbE6NB72DjtlcBoRJVsr"
        + "QyNRf0hLLhDThjTYxsrQeNg7aFOgRHmwPt+Guv5b2DtwNYiSNaTGNquu/xX2DlYcsqoO3ts3Ia8fxr1nTiBYso9l+Eb09eq9Yy0IWhh9rEDdsM"
        + "CmTGCezHeosIkzJDLZkcImKoAitB+JTSjAiX1vEjv8Qc4otjONjW5Q1Id+Ip+DykZzCI7ku5LZaAmsZLXd6WxUBHfKtguhjcKAnHBPQjs0NpCo"
        + "PR+h3QjfHA/vD78AAL8FjgplbmRzdHJlYW0KZW5kb2JqCgp4cmVmCjAgOQowMDAwMDAwMDAwIDY1NTM1IGYgCjAwMDAwMDAwMTYgMDAwMDAgbi"
        + "AKMDAwMDAwMDA2MiAwMDAwMCBuIAowMDAwMDAwMTE0IDAwMDAwIG4gCjAwMDAwMDAxNTUgMDAwMDAgbiAKMDAwMDAwMDI3NCAwMDAwMCBuIAow"
        + "MDAwMDAwMzY1IDAwMDAwIG4gCjAwMDAwMDA0ODIgMDAwMDAgbiAKMDAwMDAwMDU3MSAwMDAwMCBuIAoKdHJhaWxlcgo8PC9TaXplIDkvUm9vdC"
        + "AxIDAgUi9JRFs8QzI5RkMyOUQyMzM2MTFDMjlGMTJDMkEzMEIzMUMyODE+PEY0OUUyMUZBRDNBNjEyQjJDQjNBRDM1OTA0QjY5NEYzPl0+Pgpz"
        + "dGFydHhyZWYKMTE5MAolJUVPRgo=";

    private const string PLAIN_TEXT =
        "JVBERi0xLjcKJcK1wrYKCjEgMCBvYmoKPDwvVHlwZS9DYXRhbG9nL1BhZ2VzIDIgMCBSPj4KZW5kb2JqCgoyIDAgb2JqCjw8L1R5cGUvUGFnZX"
        + "MvQ291bnQgMS9LaWRzWzQgMCBSXT4+CmVuZG9iagoKMyAwIG9iago8PC9Gb250PDwvaGVsdiA2IDAgUj4+Pj4KZW5kb2JqCgo0IDAgb2JqCjw8"
        + "L1R5cGUvUGFnZS9NZWRpYUJveFswIDAgMzAwIDQyMF0vUm90YXRlIDAvUmVzb3VyY2VzIDMgMCBSL1BhcmVudCAyIDAgUi9Db250ZW50c1s1ID"
        + "AgUiA3IDAgUl0+PgplbmRvYmoKCjUgMCBvYmoKPDwvTGVuZ3RoIDQyPj4Kc3RyZWFtCgpxCjAgMCAzMDAgNDIwIHJlCmgKMSAxIDEgUkcgMSAx"
        + "IDEgcmcgQgpRCgplbmRzdHJlYW0KZW5kb2JqCgo2IDAgb2JqCjw8L1R5cGUvRm9udC9TdWJ0eXBlL1R5cGUxL0Jhc2VGb250L0hlbHZldGljYS"
        + "9FbmNvZGluZy9XaW5BbnNpRW5jb2Rpbmc+PgplbmRvYmoKCjcgMCBvYmoKPDwvTGVuZ3RoIDQ3Ny9GaWx0ZXIvRmxhdGVEZWNvZGU+PgpzdHJl"
        + "YW0KeNrdl8Fu3CAQhu9+Cp5gwsAwA1KVQ6WqUm6JfKt6iu320B7SQ54/A95UTam2s5J3nUSWJTzAD59/g4fhYfg4Dui8XuhCdlECkCQ3/nRX3+"
        + "cfj07cuLRq7+4+Hwq/vrkvH+ieFwmceAqei3iJspaJF251QWNaQxpDnji1UtTaWeOJo1CNSaottQVVFb2jtik8s2g0qVJTaD1oVW/xov2iqi41"
        + "WpV40t466vXX8eYFESMUMgC1QevQVG+FmdvA67TvdYJhBa7DC/0J2mKTthBVwAZTp42qgb8VVE91pxW1tS/PEeFDLban2k9fQweSEMhbrFGtRe"
        + "0gCVJf2hz8fmZ1FOQhiwXiiMcVYh+zOpzoIUaLKf82WbV3NavjwQLZQmNej5f1quPxBQJTKZYv7r9Lch+3/mYKJYNE2xI6efu8lF0dVM6AxbS7"
        + "GffPc5vVEYgAsxFgm3/ddmZ1MCyAwWjHBj+7rczqOBJDMi3+sycmp5rVoRCDTyZLzpyZ2M3qGGKChMW2He+cSB7zq+MKBCWbvNkhkzzuV8eCBG"
        + "RCecVZf/ARCqZ3kPZjiRDFuGReceKPOUAmYyb2JnJ/1LNyxHeT+qMelMV09H8ryT/qiTnEcuHkv83i0zjcDk/qQeyjCmVuZHN0cmVhbQplbmRv"
        + "YmoKCnhyZWYKMCA4CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAxNiAwMDAwMCBuIAowMDAwMDAwMDYyIDAwMDAwIG4gCjAwMDAwMDAxMT"
        + "QgMDAwMDAgbiAKMDAwMDAwMDE1NSAwMDAwMCBuIAowMDAwMDAwMjY4IDAwMDAwIG4gCjAwMDAwMDAzNTkgMDAwMDAgbiAKMDAwMDAwMDQ0OCAw"
        + "MDAwMCBuIAoKdHJhaWxlcgo8PC9TaXplIDgvUm9vdCAxIDAgUi9JRFs8NkM3REMzOUY0OUMzODgzQUMyQjVDM0I5NUM0MTQ3QzI+PDBDMDAwN0"
        + "JFRTFFN0NGMTcxNTE5NTY5MDM5NUM4MzNGPl0+PgpzdGFydHhyZWYKOTk0CiUlRU9GCg==";

    private const string CLEAN_COVER =
        "JVBERi0xLjcKJcK1wrYKCjEgMCBvYmoKPDwvVHlwZS9DYXRhbG9nL1BhZ2VzIDIgMCBSPj4KZW5kb2JqCgoyIDAgb2JqCjw8L1R5cGUvUGFnZX"
        + "MvQ291bnQgMS9LaWRzWzQgMCBSXT4+CmVuZG9iagoKMyAwIG9iago8PC9Gb250PDwvaGVsdiA3IDAgUj4+Pj4KZW5kb2JqCgo0IDAgb2JqCjw8"
        + "L1R5cGUvUGFnZS9NZWRpYUJveFswIDAgMzAwIDQyMF0vUm90YXRlIDAvUmVzb3VyY2VzIDMgMCBSL1BhcmVudCAyIDAgUi9Db250ZW50c1s1ID"
        + "AgUiA2IDAgUiA4IDAgUl0+PgplbmRvYmoKCjUgMCBvYmoKPDwvTGVuZ3RoIDQyPj4Kc3RyZWFtCgpxCjAgMCAzMDAgNDIwIHJlCmgKMSAxIDEg"
        + "UkcgMSAxIDEgcmcgQgpRCgplbmRzdHJlYW0KZW5kb2JqCgo2IDAgb2JqCjw8L0xlbmd0aCA0OS9GaWx0ZXIvRmxhdGVEZWNvZGU+PgpzdHJlYW"
        + "0KeNrjKuQyUDBQMDYwUDAxMlAoSuXK4NIzVNAzNFXQM1YIcldAcIrSFZy4ArkA8X8JowplbmRzdHJlYW0KZW5kb2JqCgo3IDAgb2JqCjw8L1R5"
        + "cGUvRm9udC9TdWJ0eXBlL1R5cGUxL0Jhc2VGb250L0hlbHZldGljYS9FbmNvZGluZy9XaW5BbnNpRW5jb2Rpbmc+PgplbmRvYmoKCjggMCBvYm"
        + "oKPDwvTGVuZ3RoIDk0L0ZpbHRlci9GbGF0ZURlY29kZT4+CnN0cmVhbQp42uMq5HIK4TJUMABCQwUjAwVjCws9Q3NThZBcBf2M1JwyBUNDhZA0"
        + "oBwIBrlDGUXpCtE2JslmaeZGZqZmKUYGZpbmBubG5hC2iVmaGVDOLjbEi8s1hCuQCwBw0RcJCmVuZHN0cmVhbQplbmRvYmoKCnhyZWYKMCA5Cj"
        + "AwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAxNiAwMDAwMCBuIAowMDAwMDAwMDYyIDAwMDAwIG4gCjAwMDAwMDAxMTQgMDAwMDAgbiAKMDAw"
        + "MDAwMDE1NSAwMDAwMCBuIAowMDAwMDAwMjc0IDAwMDAwIG4gCjAwMDAwMDAzNjUgMDAwMDAgbiAKMDAwMDAwMDQ4MiAwMDAwMCBuIAowMDAwMD"
        + "AwNTcxIDAwMDAwIG4gCgp0cmFpbGVyCjw8L1NpemUgOS9Sb290IDEgMCBSL0lEWzw3QjBCQzNBNDBFQzJBQjVCQzM5MEMzOENDM0FCQzJCOD48"
        + "RkVBMzYxMTU1NTYxQ0RGQTc2MzI1MjdBRTFDNDA5M0M+XT4+CnN0YXJ0eHJlZgo3MzMKJSVFT0YK";

    private static byte[] Pdf(string base64) => System.Convert.FromBase64String(base64);

    /// <summary>
    /// Il caso che #3590 ha scoperto: pagina a grafica piena CON molto testo
    /// (titolo, editore, claim - cio' che ha ogni copertina di rulebook moderna).
    /// Senza il cap la penalita' da sola supera l'imageRatio, che satura a 1.0, e
    /// la pagina viene rigettata per quanto sia grafica.
    /// </summary>
    [Fact]
    [Trait("Issue", "3590")]
    public async Task ExtractAsync_GraphicPageWithMuchText_IsStillSelected()
    {
        var result = await _sut.ExtractAsync(Pdf(COVER_WITH_TEXT), CancellationToken.None);

        Assert.Equal(PdfCoverExtractionOutcome.Generated, result.Outcome);
        Assert.Equal(0, result.SelectedPageIndex);
    }

    /// <summary>
    /// Il guard che impedisce al cap di degenerare: una pagina di solo testo non ha
    /// grafica da compensare, quindi resta sotto soglia e il PDF e' Skipped. Senza
    /// questo, "non penalizzare mai" soddisferebbe banalmente il test sopra.
    /// </summary>
    [Fact]
    [Trait("Issue", "3590")]
    public async Task ExtractAsync_TextOnlyPage_IsStillSkipped()
    {
        var result = await _sut.ExtractAsync(Pdf(PLAIN_TEXT), CancellationToken.None);

        Assert.Equal(PdfCoverExtractionOutcome.Skipped, result.Outcome);
    }

    /// <summary>Non-regressione: una copertina pulita passava prima e passa ancora.</summary>
    [Fact]
    [Trait("Issue", "3590")]
    public async Task ExtractAsync_CleanCover_StillSelected()
    {
        var result = await _sut.ExtractAsync(Pdf(CLEAN_COVER), CancellationToken.None);

        Assert.Equal(PdfCoverExtractionOutcome.Generated, result.Outcome);
        Assert.Equal(0, result.SelectedPageIndex);
    }

    /// <summary>
    /// Pinna il valore del cap. Non e' pedanteria: e' la costante che decide fra
    /// "correzione" e "veto", e cambiarla sposta le cover in silenzio su tutto il
    /// catalogo - 9 su 89 gia' con questo valore.
    /// </summary>
    [Fact]
    [Trait("Issue", "3590")]
    public void MaxTextPenalty_IsPinnedTo_HalfOfMaxImageRatio()
    {
        Assert.Equal(0.5d, PdfCoverExtractor.MaxTextPenalty);
    }
}
