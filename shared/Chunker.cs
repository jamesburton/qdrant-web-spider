namespace QdrantWebSpider;

public record Chunk(string Heading, string Text, int Index);

public static class Chunker
{
    private const int MaxTokenBudget = 512;
    private const int MinTokenThreshold = 10;
    private const double ApproxCharsPerToken = 4.0;

    public static List<Chunk> ChunkPage(ExtractedPage page)
    {
        var chunks = new List<Chunk>();
        int index = 0;

        foreach (var section in page.Sections)
        {
            var sectionChunks = ChunkSection(section, ref index);
            chunks.AddRange(sectionChunks);
        }

        // If no sections produced, chunk the whole body text
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(page.BodyText))
        {
            var sectionChunks = ChunkSection(new ExtractedSection("", page.BodyText), ref index);
            chunks.AddRange(sectionChunks);
        }

        return chunks;
    }

    private static List<Chunk> ChunkSection(ExtractedSection section, ref int index)
    {
        var chunks = new List<Chunk>();
        var text = section.Text.Trim();
        var heading = section.Heading;

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        var estimatedTokens = EstimateTokens(text);

        if (estimatedTokens <= MaxTokenBudget)
        {
            // Section fits in one chunk
            if (estimatedTokens >= MinTokenThreshold)
            {
                chunks.Add(new Chunk(heading, PrefixHeading(heading, text), index++));
            }
            return chunks;
        }

        // Split at paragraph boundaries
        var paragraphs = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        var currentChunkParts = new List<string>();
        int currentTokens = 0;

        foreach (var para in paragraphs)
        {
            var paraTokens = EstimateTokens(para);

            if (currentTokens + paraTokens > MaxTokenBudget && currentChunkParts.Count > 0)
            {
                // Flush current chunk
                var chunkText = string.Join("\n\n", currentChunkParts);
                if (EstimateTokens(chunkText) >= MinTokenThreshold)
                {
                    chunks.Add(new Chunk(heading, PrefixHeading(heading, chunkText), index++));
                }
                currentChunkParts.Clear();
                currentTokens = 0;
            }

            // If a single paragraph exceeds budget, split at sentence boundaries
            if (paraTokens > MaxTokenBudget)
            {
                var sentenceChunks = SplitBySentences(para, heading, ref index);
                // Flush anything before
                if (currentChunkParts.Count > 0)
                {
                    var chunkText = string.Join("\n\n", currentChunkParts);
                    if (EstimateTokens(chunkText) >= MinTokenThreshold)
                    {
                        chunks.Add(new Chunk(heading, PrefixHeading(heading, chunkText), index++));
                    }
                    currentChunkParts.Clear();
                    currentTokens = 0;
                }
                chunks.AddRange(sentenceChunks);
                continue;
            }

            currentChunkParts.Add(para);
            currentTokens += paraTokens;
        }

        // Flush remaining
        if (currentChunkParts.Count > 0)
        {
            var chunkText = string.Join("\n\n", currentChunkParts);
            if (EstimateTokens(chunkText) >= MinTokenThreshold)
            {
                chunks.Add(new Chunk(heading, PrefixHeading(heading, chunkText), index++));
            }
        }

        return chunks;
    }

    private static List<Chunk> SplitBySentences(string text, string heading, ref int index)
    {
        var chunks = new List<Chunk>();
        var sentences = SplitIntoSentences(text);

        var currentParts = new List<string>();
        int currentTokens = 0;

        foreach (var sentence in sentences)
        {
            var sentenceTokens = EstimateTokens(sentence);

            if (currentTokens + sentenceTokens > MaxTokenBudget && currentParts.Count > 0)
            {
                var chunkText = string.Join(" ", currentParts);
                if (EstimateTokens(chunkText) >= MinTokenThreshold)
                {
                    chunks.Add(new Chunk(heading, PrefixHeading(heading, chunkText), index++));
                }
                currentParts.Clear();
                currentTokens = 0;
            }

            currentParts.Add(sentence);
            currentTokens += sentenceTokens;
        }

        if (currentParts.Count > 0)
        {
            var chunkText = string.Join(" ", currentParts);
            if (EstimateTokens(chunkText) >= MinTokenThreshold)
            {
                chunks.Add(new Chunk(heading, PrefixHeading(heading, chunkText), index++));
            }
        }

        return chunks;
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var current = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is '.' or '!' or '?' && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
            {
                sentences.Add(text[current..(i + 1)].Trim());
                current = i + 2;
            }
        }

        if (current < text.Length)
            sentences.Add(text[current..].Trim());

        return sentences.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    private static string PrefixHeading(string heading, string text)
    {
        if (string.IsNullOrWhiteSpace(heading))
            return text;
        return $"{heading}\n\n{text}";
    }

    private static int EstimateTokens(string text)
    {
        return (int)(text.Length / ApproxCharsPerToken);
    }
}
