namespace PwAssistant.Core.Models;

/// <summary>Playable class with its badge art (key matches the PNG file name).</summary>
public sealed record ClassInfo(string Key, string Abbreviation, string DisplayName)
{
    public string Display => $"{DisplayName} ({Abbreviation})";
    public string ImageFile => Key + ".png";
}

/// <summary>Curated class table (icons ship under App Resources/Classes).</summary>
public static class ClassCatalog
{
    public static IReadOnlyList<ClassInfo> All { get; } =
    [
        new("andarilho", "ADR", "Andarilho"),
        new("arcano", "SK", "Arcano"),
        new("arqueiro", "EA", "Arqueiro"),
        new("atiradora", "AT", "Atiradora"),
        new("barbaro", "WB", "Bárbaro"),
        new("bardo", "BD", "Bardo"),
        new("ceifador", "CF", "Ceifador"),
        new("espiritualista", "PSY", "Espiritualista"),
        new("feiticeira", "WF", "Feiticeira"),
        new("guerreiro", "WR", "Guerreiro"),
        new("mago", "MG", "Mago"),
        new("mistico", "MS", "Místico"),
        new("mercenario", "MC", "Mercenário"),
        new("paladino", "PL", "Paladino"),
        new("retalhador", "RT", "Retalhador"),
        new("sacerdote", "EP", "Sacerdote"),
        new("tormentador", "TM", "Tormentador"),
    ];

    public static bool TryGet(string? key, out ClassInfo info)
    {
        info = All.FirstOrDefault(c =>
            string.Equals(c.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase))!;
        return info is not null;
    }
}
