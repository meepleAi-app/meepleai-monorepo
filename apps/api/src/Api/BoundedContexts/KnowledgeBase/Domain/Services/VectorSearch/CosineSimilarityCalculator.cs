namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for calculating cosine similarity between text documents
/// ISSUE-975: BGAI-033 - Consensus similarity calculation using cosine ≥0.90
/// </summary>
/// <remarks>
/// Implements TF-IDF (Term Frequency-Inverse Document Frequency) vectorization
/// followed by cosine similarity calculation.
///
/// Algorithm:
/// 1. Tokenize and normalize both texts
/// 2. Build TF-IDF vectors for each document
/// 3. Calculate cosine similarity: (A · B) / (||A|| * ||B||)
///
/// Cosine similarity ranges from -1 to 1:
/// - 1.0 = identical semantic content
/// - 0.0 = orthogonal (no common terms)
/// - -1.0 = opposite (not applicable for text)
///
/// For consensus validation, we use threshold ≥0.90 which indicates
/// very high semantic similarity between model responses.
/// </remarks>
internal class CosineSimilarityCalculator
{
    private static readonly char[] TokenSeparators =
    {
        ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':', '(', ')', '[', ']', '{', '}', '"', '\'', '-', '—', '/', '\\'
    };

    /// <summary>
    /// Calculate cosine similarity between two text documents using TF-IDF
    /// </summary>
    /// <param name="text1">First document</param>
    /// <param name="text2">Second document</param>
    /// <returns>Cosine similarity score (0.0 to 1.0)</returns>
    public double CalculateCosineSimilarity(string text1, string text2)
    {
        if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
        {
            return 0.0;
        }

        // Tokenize both documents
        var tokens1 = Tokenize(text1);
        var tokens2 = Tokenize(text2);

        if (tokens1.Count == 0 || tokens2.Count == 0)
        {
            return 0.0;
        }

        // Build vocabulary (all unique terms across both documents)
        var vocabulary = new HashSet<string>(tokens1.Keys, StringComparer.Ordinal);
        vocabulary.UnionWith(tokens2.Keys);

        // Calculate TF-IDF vectors
        var vector1 = CalculateTfIdfVector(tokens1, tokens2, vocabulary);
        var vector2 = CalculateTfIdfVector(tokens2, tokens1, vocabulary);

        // Calculate cosine similarity
        return CalculateCosineSimilarityFromVectors(vector1, vector2);
    }

    /// <summary>
    /// Tokenize and count term frequencies in a document
    /// </summary>
    /// <param name="text">Input text</param>
    /// <returns>Dictionary of term frequencies</returns>
    private Dictionary<string, int> Tokenize(string text)
    {
        var tokens = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Split on whitespace and punctuation
        var words = text.ToLowerInvariant()
            .Split(TokenSeparators,
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            // Filter out very short tokens (likely noise)
            if (word.Length > 2)
            {
                tokens[word] = tokens.GetValueOrDefault(word, 0) + 1;
            }
        }

        return tokens;
    }

    /// <summary>
    /// Calculate TF-IDF vector for a document
    /// </summary>
    /// <param name="currentDoc">Term frequencies in current document</param>
    /// <param name="otherDoc">Term frequencies in other document (for IDF calculation)</param>
    /// <param name="vocabulary">Complete vocabulary across both documents</param>
    /// <returns>TF-IDF vector</returns>
    private Dictionary<string, double> CalculateTfIdfVector(
        Dictionary<string, int> currentDoc,
        Dictionary<string, int> otherDoc,
        HashSet<string> vocabulary)
    {
        var vector = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var totalTerms = currentDoc.Values.Sum();

        foreach (var term in vocabulary)
        {
            // Calculate TF (Term Frequency): normalized frequency in current document
            var tf = currentDoc.GetValueOrDefault(term, 0) / (double)totalTerms;

            // Calculate IDF (Inverse Document Frequency) for 2-document similarity
            // For cosine similarity measurement, shared vocabulary should contribute MORE to similarity scores
            // Shared terms (both docs): weight = 2 (appears in both documents)
            // Unique terms (one doc): weight = 1 (appears in only one document)
            // Linear weighting (no logarithm) provides balanced discrimination between similar and different documents
            var docsContainingTerm = 0;
            if (currentDoc.ContainsKey(term)) docsContainingTerm++;
            if (otherDoc.ContainsKey(term)) docsContainingTerm++;

            var idf = (double)docsContainingTerm; // Linear: shared terms weighted 2x higher than unique terms

            // TF-IDF = TF * IDF
            vector[term] = tf * idf;
        }

        return vector;
    }

    /// <summary>
    /// Calculate cosine similarity between two TF-IDF vectors
    /// </summary>
    /// <param name="vector1">First TF-IDF vector</param>
    /// <param name="vector2">Second TF-IDF vector</param>
    /// <returns>Cosine similarity (0.0 to 1.0)</returns>
    private double CalculateCosineSimilarityFromVectors(
        Dictionary<string, double> vector1,
        Dictionary<string, double> vector2)
    {
        // Calculate dot product
        var dotProduct = 0.0;
        foreach (var term in vector1.Keys)
        {
            if (vector2.TryGetValue(term, out var value2))
            {
                dotProduct += vector1[term] * value2;
            }
        }

        // Calculate magnitudes
        var magnitude1 = Math.Sqrt(vector1.Values.Sum(v => v * v));
        var magnitude2 = Math.Sqrt(vector2.Values.Sum(v => v * v));

        // S1244: Avoid division by zero with tolerance-based comparison
        const double epsilon = 1e-10;
        if (Math.Abs(magnitude1) < epsilon || Math.Abs(magnitude2) < epsilon)
        {
            return 0.0;
        }

        // Cosine similarity = dot product / (magnitude1 * magnitude2)
        var cosineSimilarity = dotProduct / (magnitude1 * magnitude2);

        // Clamp to [0, 1] range (should already be in this range for text)
        return Math.Max(0.0, Math.Min(1.0, cosineSimilarity));
    }
}
