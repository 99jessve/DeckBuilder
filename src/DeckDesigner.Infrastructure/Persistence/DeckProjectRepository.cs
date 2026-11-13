namespace DeckDesigner.Infrastructure.Persistence;

using System.Text.Json;
using DeckDesigner.Domain.Model;

/// <summary>
/// Persists deck projects as JSON documents.
/// </summary>
public sealed class DeckProjectRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string Serialize(IReadOnlyList<Deck> decks)
    {
        ArgumentNullException.ThrowIfNull(decks);
        var dto = new DeckProjectDto
        {
            Version = 2,
            Decks = decks.Select(DeckMapper.ToDto).ToArray(),
        };

        return JsonSerializer.Serialize(dto, Options);
    }

    public IReadOnlyList<Deck> Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var dto = JsonSerializer.Deserialize<DeckProjectDto>(json, Options)
                  ?? throw new InvalidOperationException("Unable to deserialize deck project.");

        if (dto.Version is < 1 or > 2)
        {
            throw new InvalidOperationException($"Unsupported project version {dto.Version}.");
        }

        return dto.Decks.Select(DeckMapper.ToDomain).ToList();
    }

    public async Task SaveAsync(string path, IReadOnlyList<Deck> decks, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = Serialize(decks);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Deck>> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Deserialize(json);
    }
}
