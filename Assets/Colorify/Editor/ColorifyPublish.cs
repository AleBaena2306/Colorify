using KovusPride.Publishing;
using UnityEditor;

// COLORIFY > Build > <portal>: una build lista para subir a cada portal web (ver KovusPridePublish).
// El portfolio sigue con COLORIFY > Build > Web (móvil + PC).
public static class ColorifyPublish
{
    private const string Game = "Colorify";

    [MenuItem("COLORIFY/Build/CrazyGames", priority = 100)]
    public static void CrazyGames() => KovusPridePublish.Build(Store.CrazyGames, Game);

    [MenuItem("COLORIFY/Build/Playgama", priority = 101)]
    public static void Playgama() => KovusPridePublish.Build(Store.Playgama, Game);

    [MenuItem("COLORIFY/Build/Playgama · subir la última build", priority = 111)]
    public static void PlaygamaUpload() => KovusPridePublish.UploadLast(Store.Playgama, Game);

    [MenuItem("COLORIFY/Build/GameDistribution", priority = 102)]
    public static void GameDistribution() => KovusPridePublish.Build(Store.GameDistribution, Game);

    [MenuItem("COLORIFY/Build/itch.io", priority = 103)]
    public static void Itch() => KovusPridePublish.Build(Store.Itch, Game);

    [MenuItem("COLORIFY/Build/Todos los portales", priority = 120)]
    public static void All() => KovusPridePublish.BuildAll(Game);
}
