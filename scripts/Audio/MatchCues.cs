using System;
using Godot;
using DogeballVR.Gameplay;

namespace DogeballVR.Audio;

/// <summary>
/// VR : les annonces sonores de la partie que le HUD du clavier-souris joue déjà (DesktopHud) : bips « 3, 2, 1 », « GO ! », sifflet de fin de
/// manche, mort subite, jingle de victoire ou de défaite. Le casque n'a pas de HUD : sans ces sons, rien ne dit que la manche démarre.
/// « GO ! » fait aussi vibrer les deux manettes (départ senti même sans regarder le panneau de score).
/// </summary>
public partial class MatchCues : Node
{
    public MatchView View { get; set; } = null!;
    /// <summary>Vibration des manettes (main -1 : les deux), branchée par Main.</summary>
    public Action<int, float, float>? Haptic { get; set; }

    MatchView.Snapshot prev;
    bool first = true;

    public override void _ExitTree() => View.Detach();

    public override void _Process(double delta)
    {
        View.Update();
        var s = View.State;
        if (first) { prev = s; first = false; return; }
        if (s.Phase == MatchView.Phase.Countdown && s.Countdown > 0 && (prev.Phase != MatchView.Phase.Countdown || s.Countdown != prev.Countdown))
            Sfx.Play(this, SfxKind.Countdown);
        if (prev.Phase == MatchView.Phase.Countdown && s.Phase == MatchView.Phase.Playing)
        {
            Sfx.Play(this, SfxKind.Go);
            Haptic?.Invoke(-1, 0.5f, 0.12f);
        }
        if (s.Phase == MatchView.Phase.RoundOver && prev.Phase != MatchView.Phase.RoundOver) Sfx.Play(this, SfxKind.Whistle);
        if (s.Overtime && !prev.Overtime && s.Phase == MatchView.Phase.Playing) Sfx.Play(this, SfxKind.Whistle, null, 0.8f);
        if (s.Phase == MatchView.Phase.MatchOver && prev.Phase != MatchView.Phase.MatchOver)
        {
            Sfx.Play(this, SfxKind.Whistle);
            Music.Stinger(s.Winner == View.LocalTeam ? SfxKind.Victory : SfxKind.Defeat);
        }
        prev = s;
    }
}
