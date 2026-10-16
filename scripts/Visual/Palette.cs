using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Palette unique du rendu : tout s'y réfère, donc la future DA se change ici. Valeurs PROVISOIRES de greybox
/// (neutres, deux équipes lisibles, magenta pour les limites) : la DA est en refonte.
/// </summary>
public static class Palette
{
    public static readonly Color Night = new("20232b");      // sol
    public static readonly Color Indigo = new("3a3f4b");     // structures, armure
    public static readonly Color TeamA = new("3d8bff");      // équipe 1 : bleu
    public static readonly Color TeamB = new("ff7a2e");      // équipe 2 : orange
    public static readonly Color BallCore = new("f2f2f2");   // balle libre / cœur de balle
    public static readonly Color Danger = new("ff2e93");     // limites de zone
    public static readonly Color UiGrey = new("c9d1e8");

    public static Color Team(int team) => team switch { 0 => TeamA, 1 => TeamB, _ => BallCore };

    /// <summary>Teinte désaturée pour les bots : on les distingue des humains au tournage.</summary>
    public static Color BotTeam(int team)
    {
        var c = Team(team);
        return c.Lerp(new Color(0.55f, 0.55f, 0.6f), 0.45f);
    }
}
