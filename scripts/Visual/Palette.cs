using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Palette unique du rendu : tout s'y réfère. Valeurs de la direction artistique v3 (arène sombre, deux équipes,
/// balle jaune-vert réservée à la balle). Les noms historiques (Night, Indigo, Danger, UiGrey) sont conservés.
/// </summary>
public static class Palette
{
    public static readonly Color Wall = new("1b2540");        // murs et fond
    public static readonly Color Floor = new("3a3f52");       // sol de l'arène
    public static readonly Color FloorTeamA = new("4a3f4c");  // moitié de sol côté équipe 1 (orange)
    public static readonly Color FloorTeamB = new("364866");  // moitié de sol côté équipe 2 (bleue)
    public static readonly Color Lines = new("f2f4f8");       // lignes blanches, texte
    public static readonly Color Panel = new("101830");       // panneaux d'interface, contour sombre
    public static readonly Color TeamA = new("ff6a1a");       // équipe 1 : orange (disque blanc sur le torse)
    public static readonly Color TeamB = new("6cc8ff");       // équipe 2 : bleue (losange blanc sur le torse)
    public static readonly Color BallCore = new("d6ff2e");    // balle : seule à utiliser cette couleur

    public static readonly Color Night = Wall;                // fond d'écran et base des sols
    public static readonly Color Indigo = new("2b3658");      // abris, plateaux (éclairage simple)
    public static readonly Color Danger = Lines;              // bande lumineuse de limite
    public static readonly Color UiGrey = Lines;

    public static Color Team(int team) => team switch { 0 => TeamA, 1 => TeamB, _ => BallCore };

    /// <summary>Teinte désaturée pour les bots : on les distingue des humains au tournage.</summary>
    public static Color BotTeam(int team)
    {
        var c = Team(team);
        return c.Lerp(new Color(0.55f, 0.55f, 0.6f), 0.45f);
    }
}
