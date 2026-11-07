using System.Collections.Generic;
using Godot;
using DogeballVR.UI;

namespace DogeballVR.Online;

/// <summary>Un couvre-chef du catalogue : identifiant stable (réseau, sauvegarde), nom affiché, niveau qui le débloque.</summary>
public sealed record HeadwearDef(string Id, string Name, int Level);

/// <summary>
/// Récompenses de progression : les couvre-chefs, débloqués par niveau (Progression) et portés en partie par le personnage.
/// Purement cosmétiques : ils ne changent ni la boîte de touche ni la silhouette de l'équipe (couleurs neutres, petite taille).
/// Le choix est gardé dans settings.cfg (GameSettings.Headwear, recopié par Steam Cloud) et envoyé à la salle en ligne.
/// </summary>
public static class Cosmetics
{
    /// <summary>Aucun couvre-chef.</summary>
    public const string None = "aucun";

    /// <summary>Catalogue, dans l'ordre de déblocage. Ajouter à la fin : l'identifiant circule sur le réseau.</summary>
    public static readonly IReadOnlyList<HeadwearDef> Headwear = new[]
    {
        new HeadwearDef(None, "Tête nue", 1),
        new HeadwearDef("casquette", "Casquette", 2),
        new HeadwearDef("bandeau", "Bandeau", 4),
        new HeadwearDef("bonnet", "Bonnet", 7),
        new HeadwearDef("lunettes", "Masque de course", 10),
        new HeadwearDef("casque", "Casque de chantier", 15),
        new HeadwearDef("bob", "Bob", 20),
        new HeadwearDef("casquette_encre", "Casquette encre", 25),
        new HeadwearDef("crete", "Crête", 30),
        new HeadwearDef("bob_kaki", "Bob kaki", 35),
        new HeadwearDef("casque_blanc", "Casque blanc", 40),
        new HeadwearDef("couronne_argent", "Couronne d'argent", 45),
        new HeadwearDef("couronne", "Couronne d'or", 50),
        new HeadwearDef("bonnet_ocre", "Bonnet ocre", 60),
        new HeadwearDef("crete_papier", "Crête papier", 70),
        new HeadwearDef("lunettes_or", "Masque doré", 80),
        new HeadwearDef("bandeau_papier", "Bandeau papier", 90),
        new HeadwearDef("couronne_noire", "Couronne noire", 100),
    };

    /// <summary>Définition d'un identifiant reçu (sauvegarde, réseau) ; null s'il est inconnu (version plus récente, fichier modifié).</summary>
    public static HeadwearDef? Find(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var h in Headwear) if (h.Id == id) return h;
        return null;
    }

    /// <summary>Niveau actuel du joueur.</summary>
    public static int Level => Progression.LevelOf(Progression.TotalXp).Level;

    /// <summary>Le joueur a-t-il atteint le niveau qui débloque ce couvre-chef ?</summary>
    public static bool IsUnlocked(HeadwearDef h, int level) => level >= h.Level;

    /// <summary>
    /// Couvre-chef porté par le joueur local : le choix enregistré s'il est débloqué, sinon tête nue
    /// (progression remise à zéro, fichier copié d'un autre profil).
    /// </summary>
    public static string Equipped
    {
        get
        {
            if (Tools.DevFlags.Value("--headwear=") is { } forced) return Find(forced)?.Id ?? None;   // captures et tests
            GameSettings.Load();
            return Find(GameSettings.Headwear) is { } h && IsUnlocked(h, Level) ? h.Id : None;
        }
    }

    /// <summary>Porte ce couvre-chef (refusé s'il n'est pas débloqué) et l'enregistre.</summary>
    public static bool Equip(string id)
    {
        if (Find(id) is not { } h || !IsUnlocked(h, Level)) return false;
        GameSettings.Headwear = h.Id;
        GameSettings.Save();
        EquippedChanged?.Invoke(h.Id);
        return true;
    }

    /// <summary>Nouveau couvre-chef porté (le fond du menu et la salle en ligne suivent).</summary>
    public static event System.Action<string>? EquippedChanged;

    /// <summary>
    /// Couvre-chef d'un bot, fixe pour une place donnée : deux bots sur trois en portent un des premiers niveaux (jusqu'au niveau 10),
    /// comme un joueur qui débute ; les plus rares restent à gagner.
    /// </summary>
    public static string ForBot(int playerId)
    {
        if (playerId < 0 || playerId % 3 == 2) return None;
        var pool = new List<HeadwearDef>();
        foreach (var h in Headwear) if (h.Level > 1 && h.Level <= 10) pool.Add(h);
        return pool[(playerId * 7 + 3) % pool.Count].Id;
    }

    /// <summary>Nombre de couvre-chefs gagnés au niveau donné (tête nue non comptée).</summary>
    public static int OwnedCount(int level)
    {
        int n = 0;
        foreach (var h in Headwear) if (h.Id != None && IsUnlocked(h, level)) n++;
        return n;
    }

    /// <summary>Prochain couvre-chef à gagner après ce niveau, null s'il n'en reste pas.</summary>
    public static HeadwearDef? NextAfter(int level)
    {
        foreach (var h in Headwear) if (h.Level > level) return h;
        return null;
    }

    /// <summary>Couvre-chefs débloqués en passant du niveau <paramref name="before"/> au niveau <paramref name="after"/> (bandeau d'XP).</summary>
    public static List<HeadwearDef> UnlockedBetween(int before, int after)
    {
        var list = new List<HeadwearDef>();
        foreach (var h in Headwear) if (h.Level > before && h.Level <= after) list.Add(h);
        return list;
    }
}
