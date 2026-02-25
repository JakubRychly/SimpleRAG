using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace SimpleRAG;

public partial class MainViewModel : ObservableObject
{
    private RagService? _ragService;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ke spuštění potřebujete zadat Gemini API klíč.";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isServiceReady = false;

    public ObservableCollection<TextChunkModel> SearchResults { get; } = new();

    [RelayCommand]
    private async Task InitializeServiceAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "Zadejte nejprve API klíč!";
            return;
        }

        try
        {
            StatusMessage = "Inicializace služby a databáze...";
            _ragService = new RagService(ApiKey);
            await _ragService.InitializeAsync();
            IsServiceReady = true;
            StatusMessage = "Služba je připravena! Můžete nahrát dokumenty nebo vyhledávat.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chyba při inicializaci: {ex.Message} " + (ex.InnerException?.Message ?? "");
        }
    }

    [RelayCommand]
    private async Task LoadDocumentsAsync()
    {
        if (_ragService == null || !IsServiceReady)
            return;

        var openFileDialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Textové soubory (*.txt)|*.txt|Všechny soubory (*.*)|*.*",
            Title = "Vyberte dokumenty k nahrání do RAG"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            IsServiceReady = false; // Disable UI
            int totalChunks = 0;
            StatusMessage = "Zpracovávám dokumenty...";

            try
            {
                foreach (string file in openFileDialog.FileNames)
                {
                    totalChunks += await _ragService.IngestDocumentAsync(file);
                }
                StatusMessage = $"Dokumenty zpracovány. Celkem uloženo {totalChunks} úryvků (chunků) textu.";
            }
            catch (Exception ex)
            {
                File.WriteAllText("error_log.txt", ex.ToString());
                StatusMessage = $"Chyba při zpracování: {ex.Message}. Detaily v error_log.txt.";
            }
            finally
            {
                IsServiceReady = true;
            }
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (_ragService == null || !IsServiceReady || string.IsNullOrWhiteSpace(SearchQuery))
            return;

        IsServiceReady = false;
        StatusMessage = "Vyhledávání...";
        SearchResults.Clear();

        try
        {
            var results = await _ragService.SearchAsync(SearchQuery, maxResults: 3);
            foreach (var r in results)
            {
                SearchResults.Add(r);
            }
            StatusMessage = $"Nalezeno {results.Count} výsledků.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chyba hledání: {ex.Message}";
        }
        finally
        {
            IsServiceReady = true;
        }
    }
}
