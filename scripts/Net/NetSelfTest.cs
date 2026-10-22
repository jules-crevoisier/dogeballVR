using Godot;
using DogeballVR.Gameplay;

namespace DogeballVR.Net;

/// <summary>
/// Joueur automatique pour tester le réseau sans casque ni clavier : le client déplace sa « main » sur la balle libre
/// la plus proche, l'attrape, puis la renvoie vers l'équipe adverse. Les traces <c>[NetTest]</c> sont lues par la CI.
/// </summary>
public partial class NetSelfTest : Node
{
    public NetSession Net { get; set; } = null!;
    public ClientWorld World { get; set; } = null!;
    public int PlayerId { get; set; }
    public Vector3 AimAt { get; set; }
    /// <summary>Zone du joueur automatique : il ne ramasse que les balles à sa portée (l'hôte refuse le reste).</summary>
    public Vector3 ZoneCenter { get; set; }
    public Vector2 ZoneHalfSize { get; set; }

    /// <summary>Lift de la zone (null : pas de test d'altitude). Le test vérifie que l'hôte refuse la triche et accorde puis reprend l'altitude.</summary>
    public Maps.LiftDef? Lift { get; set; }

    Vector3 hand;
    float requestedElevation;
    double liftClock;
    int liftPhase;           // 0 triche hors pad, 1 marche vers le pad, 2 en haut, 3 fini
    float maxCheat, maxHigh;
    double highAt = -1;
    bool dropped;
    bool spoofOnce = true;
    bool crouching;
    double timer;
    int catches, throws;
    double logTimer;

    public override void _Ready()
    {
        hand = ZoneCenter + Vector3.Up * 1.2f;
        Net.LocalPose = () =>
        {
            if (spoofOnce && Lift != null)
            {
                // Triche : un seul paquet avec la tête posée sur le pad, puis retour à la vraie position.
                spoofOnce = false;
                var fake = new Vector3(Lift.Pad.X, 1.6f + Lift.HighElevation, Lift.Pad.Z);
                return new PoseData(fake, 0, fake, fake, Lift.HighElevation);
            }
            return new PoseData(new Vector3(hand.X, (crouching ? 1.0f : 1.6f) + requestedElevation, hand.Z), 0, hand + Vector3.Up * requestedElevation, hand + Vector3.Up * requestedElevation, requestedElevation);
        };
        if (Lift == null) liftPhase = 3;
    }

    /// <summary>Phase 0 : demande d'altitude hors du pad (l'hôte doit refuser). 1 : va sur le pad. 2 : demande l'altitude et la garde (l'hôte doit l'accorder puis la reprendre).</summary>
    void LiftTest(double delta)
    {
        liftClock += delta;
        float seen = World.ElevationOf(PlayerId);
        var pad = new Vector3(Lift!.Pad.X, 0, Lift.Pad.Z);
        switch (liftPhase)
        {
            case 0:
                requestedElevation = Lift.HighElevation;
                maxCheat = Mathf.Max(maxCheat, seen);
                if (liftClock > 2.0)
                {
                    GD.Print($"[NetTest] lift triche hors pad (tête spoofée sur le pad) : altitude vue {maxCheat:0.00} {(maxCheat < 0.1f ? "OK" : "KO")}");
                    requestedElevation = 0;
                    liftPhase = 1;
                }
                break;
            case 1:
                hand = hand.MoveToward(new Vector3(pad.X, hand.Y, pad.Z), 5f * (float)delta);
                if (new Vector2(hand.X - pad.X, hand.Z - pad.Z).Length() < 0.2f) { liftPhase = 2; liftClock = 0; crouching = true; }
                break;
            case 2:
                requestedElevation = liftClock > 1.2 ? Lift.HighElevation : 0f;   // accroupi 1,2 s sur le pad, puis le client « triche » en gardant l'altitude : l'hôte doit la reprendre
                maxHigh = Mathf.Max(maxHigh, seen);
                if (highAt < 0 && seen > 0.1f) { highAt = liftClock; GD.Print("[NetTest] lift accordé sur le pad : OK"); }
                if (highAt >= 0 && seen < 0.1f && !dropped)
                {
                    dropped = true;
                    GD.Print($"[NetTest] lift repris par l'hôte après {liftClock - highAt:0.0} s (max {Lift.MaxHighSeconds:0.0} s) : {(liftClock - highAt <= Lift.MaxHighSeconds + 1.6 ? "OK" : "KO")}");
                    requestedElevation = 0;
                    liftPhase = 3;
                }
                else if (liftClock > Lift.MaxHighSeconds + 6.0)
                {
                    GD.Print("[NetTest] lift jamais accordé ou jamais repris : KO");
                    requestedElevation = 0;
                    liftPhase = 3;
                }
                break;
        }
    }

    const float HandSpeed = 8f;   // sous la vitesse maximale que l'hôte accepte (8 m/s)

    public override void _PhysicsProcess(double delta)
    {
        timer += delta;
        logTimer += delta;
        if (logTimer > 5)
        {
            logTimer = 0;
            GD.Print($"[NetTest] connecte={Net.Connected} snapshots={Net.SnapshotsReceived} balles={World.BallCount} avatars={World.AvatarCount} prises={catches} lancers={throws}");
        }

        if (liftPhase < 3 && Net.Connected && Net.SnapshotsReceived > 30)
        {
            LiftTest(delta);
            return;
        }

        var held = BallActions.FindHeld(GetTree(), PlayerId);
        if (held != null)
        {
            if (timer < 0.5) return;
            timer = 0;
            var dir = (AimAt - held.GlobalPosition).Normalized();
            Net.RequestThrow(1, dir * 18f + Vector3.Up * 1f, 0f);
            throws++;
            return;
        }

        // Aller vers la balle libre la plus proche de la zone, puis demander à l'attraper.
        Ball? best = null;
        float bestD = float.MaxValue;
        foreach (var n in GetTree().GetNodesInGroup("balls"))
        {
            if (n is not Ball { IsHeld: false } b) continue;
            var rel = b.GlobalPosition - ZoneCenter;
            if (Mathf.Abs(rel.X) > ZoneHalfSize.X || Mathf.Abs(rel.Z) > ZoneHalfSize.Y || b.GlobalPosition.Y > 2.5f) continue;
            float d = b.GlobalPosition.DistanceTo(hand);
            if (d < bestD) { bestD = d; best = b; }
        }
        if (best == null) return;
        hand = hand.MoveToward(best.GlobalPosition, HandSpeed * (float)delta);
        if (bestD < 0.5f && timer > 0.4)
        {
            timer = 0;
            Net.RequestGrip(1);   // comme un vrai grip VR : l'hôte tient le blocage miroir (une balle au sol ne bloque jamais)
            Net.RequestCatch(best.NetId, 1);
            catches++;
        }
    }
}
