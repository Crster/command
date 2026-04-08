using System;
using System.Threading.Tasks;
using ElBruno.LocalEmbeddings;
using Microsoft.Extensions.AI;

namespace CrsterCommand.Services;

public class EmbeddingService
{
    private IEmbeddingGenerator<string, Embedding<float>> _generator = new LocalEmbeddingGenerator();

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var result = await _generator.GenerateEmbeddingAsync(text);
        return result.Vector.ToArray();
    }

    public float CalculateSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length) return 0;

        float dotProduct = 0;
        float magnitude1 = 0;
        float magnitude2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = (float)Math.Sqrt(magnitude1);
        magnitude2 = (float)Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0) return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }
}
