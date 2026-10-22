namespace Grimoire_Studio.Generator;

/// <summary>Font families offered in the generator. Bundled ones are declared with @font-face in css/app.css.</summary>
public static class TokenFonts
{
    public const string DefaultLabel = "Poetsen One";
    public const string DefaultDescription = "Roboto";

    public static readonly IReadOnlyList<string> Bundled =
    [
        "Appleberry",
        "Autour One",
        "Bellota",
        "Borders Divide But Hearts Shall Conquer",
        "Chopin Script",
        "Chunkfive Ex",
        "Cocogoose",
        "DeFonarts",
        "Great Vibes",
        "KG Red Hands",
        "Poetsen One",
        "Roboto",
    ];

    /// <summary>Common fonts most systems have; not bundled.</summary>
    public static readonly IReadOnlyList<string> Common = ["Georgia", "Times New Roman", "Arial"];
}
