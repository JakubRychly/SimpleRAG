using System;

namespace PrvniRAG;

public class TextChunkModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Text { get; set; } = string.Empty;

    public string SourceFile { get; set; } = string.Empty;

    // Vektor se ukládá přímo v sqlite-vec tabulce, v modelu ho nepotřebujeme
    public ReadOnlyMemory<float>? Vector { get; set; }
}
