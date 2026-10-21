using Godot;
using DogeballVR.UI;

namespace DogeballVR.Visual;

/// <summary>
/// Secousses et coups de caméra du joueur clavier-souris (jamais en VR : le casque ne doit pas bouger tout seul).
/// Modèle « trauma » (Squirrel Eiserloh, GDC 2016) : chaque événement ajoute du trauma, qui décroît linéairement ;
/// l'amplitude vaut trauma², en rotation surtout (la translation ferait traverser les murs), sur un bruit lisse.
/// Les coups directionnels (Vlambeer) poussent la vue dans une direction puis reviennent en ressort.
/// </summary>
public static class CameraFx
{
    const float Decay = 1.6f;            // trauma perdu par seconde
    const float MaxYaw = 2.2f, MaxPitch = 1.8f, MaxRoll = 3.5f;   // degrés à trauma 1
    const float MaxOffset = 0.06f;       // m à trauma 1

    static float trauma;
    static Vector2 kick;                 // degrés (lacet, tangage) : coup directionnel qui revient en ressort
    static Vector2 kickVel;
    static float fovPunch;               // degrés ajoutés au champ, retombent en 0,25 s
    static float time;
    static readonly FastNoiseLite noise = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.9f, Seed = 11 };

    public static float Trauma => trauma;

    /// <summary>Ajoute du trauma (0,15 lancer fort, 0,35 touche reçue, 0,5 KO, 0,6 explosion proche). Plafonné à 1.</summary>
    public static void Shake(float amount) => trauma = Mathf.Clamp(trauma + amount * GameSettings.ScreenShake, 0f, 1f);

    /// <summary>Coup directionnel en degrés (x = lacet vers la droite, y = tangage vers le haut).</summary>
    public static void Kick(Vector2 degrees) => kick += degrees * Mathf.Max(0.35f, GameSettings.ScreenShake);

    public static void FovPunch(float degrees) => fovPunch = Mathf.Max(fovPunch, degrees);

    public static void Reset() { trauma = 0; kick = Vector2.Zero; kickVel = Vector2.Zero; fovPunch = 0; }

    /// <summary>Avance l'état et renvoie (rotation en degrés : tangage, lacet, roulis ; décalage local en m ; champ ajouté).</summary>
    public static (Vector3 rotation, Vector3 offset, float fov) Step(float dt)
    {
        time += dt;
        trauma = Mathf.Max(0f, trauma - Decay * dt);
        // Ressort amorti pour le coup directionnel (retour en ~0,2 s, sans rebond marqué).
        const float k = 260f, c = 26f;
        kickVel += (-k * kick - c * kickVel) * dt;
        kick += kickVel * dt;
        fovPunch = Mathf.MoveToward(fovPunch, 0f, dt * Mathf.Max(8f, fovPunch * 4f));

        float s = trauma * trauma;
        float t = time * 60f;
        var rot = new Vector3(
            MaxPitch * s * noise.GetNoise1D(t) + kick.Y,
            MaxYaw * s * noise.GetNoise1D(t + 100f) + kick.X,
            MaxRoll * s * noise.GetNoise1D(t + 200f));
        var off = new Vector3(noise.GetNoise1D(t + 300f), noise.GetNoise1D(t + 400f), 0f) * (MaxOffset * s);
        return (rot, off, fovPunch);
    }
}
