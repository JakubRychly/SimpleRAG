using System;

namespace SimpleRAG;

public class TextChunkModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Text { get; set; } = string.Empty;

    public string SourceFile { get; set; } = string.Empty;

    public double Distance { get; set; }

    // Vektor se ukládá přímo v sqlite-vec tabulce, v modelu ho nepotřebujeme
    public ReadOnlyMemory<float>? Vector { get; set; }
}
