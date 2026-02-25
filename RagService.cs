using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Text;

namespace PrvniRAG;

public class RagService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
    private readonly SqliteConnection _connection;

    public RagService(string geminiApiKey)
    {
        // 1. Vytvoříme Kernel s Google Gemini pro embeddingy
        var builder = Kernel.CreateBuilder();
        builder.AddGoogleAIEmbeddingGenerator("gemini-embedding-001", geminiApiKey);
        
        var kernel = builder.Build();
        _embeddingService = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        // 2. Otevřeme SQLite připojení a načteme sqlite-vec rozšíření
        SQLitePCL.Batteries_V2.Init();
        _connection = new SqliteConnection("Data Source=vectordb.sqlite");
        _connection.Open();
        _connection.LoadExtension("vec0");
    }

    public async Task InitializeAsync()
    {
        // Vytvoříme tabulku pro textové chunky
        using var cmdText = _connection.CreateCommand();
        cmdText.CommandText = @"
            CREATE TABLE IF NOT EXISTS text_chunks (
                id TEXT PRIMARY KEY,
                text TEXT NOT NULL,
                source_file TEXT NOT NULL
            )";
        cmdText.ExecuteNonQuery();

        // Vytvoříme virtuální tabulku pro vektorové vyhledávání (3072 dimenzí pro Gemini)
        using var cmdVec = _connection.CreateCommand();
        cmdVec.CommandText = @"
            CREATE VIRTUAL TABLE IF NOT EXISTS text_chunks_vec USING vec0(
                id TEXT PRIMARY KEY,
                embedding float[3072]
            )";
        cmdVec.ExecuteNonQuery();
    }

    public async Task<int> IngestDocumentAsync(string filePath)
    {
        int chunksCount = 0;
        string text = await File.ReadAllTextAsync(filePath);
        string fileName = Path.GetFileName(filePath);

        // Rozdělíme text na menší části (chunky)
        var lines = TextChunker.SplitPlainTextLines(text, 100);
        var chunks = TextChunker.SplitPlainTextParagraphs(lines, 250, 50);

        foreach (var chunk in chunks)
        {
            // Vygenerujeme vektor (embedding) pro daný chunk
            var embeddingResult = await _embeddingService.GenerateAsync(chunk);
            var embedding = embeddingResult.Vector;

            var id = Guid.NewGuid().ToString();

            // Uložíme textová data
            using var cmdInsertText = _connection.CreateCommand();
            cmdInsertText.CommandText = @"
                INSERT OR REPLACE INTO text_chunks (id, text, source_file) 
                VALUES ($id, $text, $source_file)";
            cmdInsertText.Parameters.AddWithValue("$id", id);
            cmdInsertText.Parameters.AddWithValue("$text", chunk);
            cmdInsertText.Parameters.AddWithValue("$source_file", fileName);
            cmdInsertText.ExecuteNonQuery();

            // Uložíme vektor do vec tabulky
            using var cmdInsertVec = _connection.CreateCommand();
            cmdInsertVec.CommandText = @"
                INSERT INTO text_chunks_vec (id, embedding) 
                VALUES ($id, $embedding)";
            cmdInsertVec.Parameters.AddWithValue("$id", id);
            cmdInsertVec.Parameters.AddWithValue("$embedding", FloatArrayToBlob(embedding));
            cmdInsertVec.ExecuteNonQuery();

            chunksCount++;
        }

        return chunksCount;
    }

    public async Task<List<TextChunkModel>> SearchAsync(string query, int maxResults = 3)
    {
        // 1. Převedeme hledaný dotaz (query) na vektor (embedding)
        var embeddingResult = await _embeddingService.GenerateAsync(query);
        var queryEmbedding = embeddingResult.Vector;

        // 2. Prohledáme vektorovou databázi pomocí sqlite-vec
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT v.id, v.distance, t.text, t.source_file
            FROM text_chunks_vec v
            INNER JOIN text_chunks t ON t.id = v.id
            WHERE v.embedding MATCH $query
            AND k = $limit
            ORDER BY v.distance";
        cmd.Parameters.AddWithValue("$query", FloatArrayToBlob(queryEmbedding));
        cmd.Parameters.AddWithValue("$limit", maxResults);

        var results = new List<TextChunkModel>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new TextChunkModel
            {
                Id = reader.GetString(0),
                Text = reader.GetString(2),
                SourceFile = reader.GetString(3)
            });
        }

        return results;
    }

    /// <summary>
    /// Převede ReadOnlyMemory&lt;float&gt; na byte[] blob pro sqlite-vec.
    /// sqlite-vec očekává vektory jako raw little-endian float32 blob.
    /// </summary>
    private static byte[] FloatArrayToBlob(ReadOnlyMemory<float> vector)
    {
        var span = vector.Span;
        var bytes = new byte[span.Length * sizeof(float)];
        MemoryMarshal.AsBytes(span).CopyTo(bytes);
        return bytes;
    }
}
