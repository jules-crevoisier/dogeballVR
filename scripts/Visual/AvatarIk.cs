using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// IK des avatars riggés (rig maison de tools/blender/rig.py), appliquée après l'animation :
/// bras à deux os vers une cible par main (mains réseau d'un joueur VR, ou pose d'armé), et accroupi qui garde
/// les pieds posés (bassin abaissé, genoux pliés, buste penché) au lieu d'enfoncer le corps dans le sol.
/// Avatar remplit les cibles à chaque image ; ce nœud ne lit rien du jeu.
/// </summary>
public partial class AvatarIk : SkeletonModifier3D
{
    /// <summary>Cibles des poignets, en coordonnées monde.</summary>
    public Vector3 LeftTarget, RightTarget;
    /// <summary>Poids de l'IK par bras : 0 = animation seule, 1 = poignet sur la cible.</summary>
    public float LeftWeight, RightWeight;
    /// <summary>Coude ouvert sur le côté et relevé (1, bras armé) plutôt que vers le bas (0).</summary>
    public float LeftElbowOut, RightElbowOut;
    /// <summary>Descente du bassin (mètres, monde), pieds gardés au sol.</summary>
    public float Crouch;
    /// <summary>Buste penché en avant (radians), pour l'accroupi.</summary>
    public float Lean;

    /// <summary>Pose finale de la dernière image, en coordonnées monde (tests et captures : hors de ce nœud, le squelette rend la pose d'avant l'IK).</summary>
    public Vector3 FinalWristL, FinalWristR, FinalHead;
    public float FinalAnkleY;

    /// <summary>Descente du bassin au-delà de laquelle les jambes ne plient plus (squat profond du modèle).</summary>
    public const float MaxCrouch = 0.48f;

    bool resolved, valid;
    int hips, spine, neck, head;
    readonly int[] arm = new int[6];   // Bras, AvantBras, Main (G puis D)
    readonly int[] leg = new int[6];   // Cuisse, Tibia, Pied (G puis D)
    Vector3 up, right, forward;        // repère du personnage dans l'espace du squelette (tiré du repos)

    void Resolve(Skeleton3D skel)
    {
        resolved = true;
        hips = skel.FindBone("Hanches");
        spine = skel.FindBone("Dos");
        neck = skel.FindBone("Cou");
        head = skel.FindBone("Tete");
        string[] armNames = { "Bras.G", "AvantBras.G", "Main.G", "Bras.D", "AvantBras.D", "Main.D" };
        string[] legNames = { "Cuisse.G", "Tibia.G", "Pied.G", "Cuisse.D", "Tibia.D", "Pied.D" };
        for (int i = 0; i < 6; i++) { arm[i] = skel.FindBone(armNames[i]); leg[i] = skel.FindBone(legNames[i]); }
        valid = hips >= 0 && spine >= 0 && neck >= 0 && head >= 0 && System.Array.TrueForAll(arm, b => b >= 0) && System.Array.TrueForAll(leg, b => b >= 0);
        if (!valid) { GD.PushWarning("[AvatarIk] os du rig introuvables : IK désactivée"); return; }
        up = (skel.GetBoneGlobalRest(head).Origin - skel.GetBoneGlobalRest(hips).Origin).Normalized();
        right = (skel.GetBoneGlobalRest(arm[3]).Origin - skel.GetBoneGlobalRest(arm[0]).Origin).Normalized();
        forward = up.Cross(right).Normalized();   // le personnage regarde vers -Z : Y × X = -Z
        right = forward.Cross(up).Normalized();
    }

    public override void _ProcessModification()
    {
        var skel = GetSkeleton();
        if (skel == null) return;
        if (!resolved) Resolve(skel);
        if (!valid) return;
        var toSkel = skel.GlobalTransform.AffineInverse();
        float scale = toSkel.Basis.Scale.X;   // modèle à l'échelle 1 en pratique ; prudence si un parent est mis à l'échelle

        if (Crouch > 0.001f || Lean > 0.001f)
        {
            // Chevilles et orientation des pieds avant de baisser le bassin : les pieds restent où l'animation les pose.
            var ankleL = skel.GetBoneGlobalPose(leg[2]);
            var ankleR = skel.GetBoneGlobalPose(leg[5]);
            var hipPose = skel.GetBonePose(hips);
            skel.SetBonePosePosition(hips, hipPose.Origin - up * Mathf.Min(Crouch, MaxCrouch) * scale);
            if (Lean > 0.001f)
            {
                // Bassin basculé vers l'avant (la moitié), le dos fait le reste : le buste reste au-dessus des pieds.
                RotateGlobal(skel, hips, new Quaternion(right, -Lean * 0.5f));
                RotateGlobal(skel, spine, new Quaternion(right, -Lean * 0.5f));
                RotateGlobal(skel, neck, new Quaternion(right, Lean * 0.6f));   // regard gardé vers l'avant
            }
            for (int s = 0; s < 2; s++)
            {
                var ankle = s == 0 ? ankleL : ankleR;
                int thigh = leg[s * 3], shin = leg[s * 3 + 1], foot = leg[s * 3 + 2];
                float side = s == 0 ? -1f : 1f;
                var hip = skel.GetBoneGlobalPose(thigh).Origin;
                // genoux vers l'avant, un peu écartés
                SolveTwoBone(skel, thigh, shin, foot, ankle.Origin, hip + (forward + right * side * 0.25f) * 0.5f, 1f);
                SetGlobalBasis(skel, foot, ankle.Basis);
            }
        }

        for (int s = 0; s < 2; s++)
        {
            float w = s == 0 ? LeftWeight : RightWeight;
            if (w <= 0.001f) continue;
            int upper = arm[s * 3], lower = arm[s * 3 + 1], hand = arm[s * 3 + 2];
            float side = s == 0 ? -1f : 1f;
            var target = toSkel * (s == 0 ? LeftTarget : RightTarget);
            var shoulder = skel.GetBoneGlobalPose(upper).Origin;
            // coudes vers le bas, en dehors et un peu en arrière ; armé : coude ouvert sur le côté, à hauteur d'épaule
            float o = s == 0 ? LeftElbowOut : RightElbowOut;
            var pole = shoulder + (-up * Mathf.Lerp(0.6f, 0.05f, o) + right * side * Mathf.Lerp(0.45f, 0.8f, o) - forward * 0.3f) * scale;
            SolveTwoBone(skel, upper, lower, hand, target, pole, Mathf.Min(w, 1f));
        }

        var toWorld = skel.GlobalTransform;
        FinalWristL = toWorld * skel.GetBoneGlobalPose(arm[2]).Origin;
        FinalWristR = toWorld * skel.GetBoneGlobalPose(arm[5]).Origin;
        FinalHead = toWorld * skel.GetBoneGlobalPose(head).Origin;
        FinalAnkleY = Mathf.Min((toWorld * skel.GetBoneGlobalPose(leg[2]).Origin).Y, (toWorld * skel.GetBoneGlobalPose(leg[5]).Origin).Y);
    }

    /// <summary>
    /// IK analytique à deux os : amène l'origine de <paramref name="end"/> sur <paramref name="target"/> (espace du squelette),
    /// l'articulation du milieu dans le plan qui contient <paramref name="pole"/>. Mélangée à l'animation selon <paramref name="weight"/>.
    /// </summary>
    static void SolveTwoBone(Skeleton3D skel, int upper, int lower, int end, Vector3 target, Vector3 pole, float weight)
    {
        var gUpper = skel.GetBoneGlobalPose(upper);
        var gLower = skel.GetBoneGlobalPose(lower);
        var a = gUpper.Origin;
        var b = gLower.Origin;
        var c = skel.GetBoneGlobalPose(end).Origin;
        float la = a.DistanceTo(b), lb = b.DistanceTo(c);
        if (la < 1e-4f || lb < 1e-4f) return;
        var toTarget = target - a;
        float d = Mathf.Clamp(toTarget.Length(), Mathf.Abs(la - lb) + 1e-3f, (la + lb) * 0.999f);
        var dir = toTarget.LengthSquared() > 1e-8f ? toTarget.Normalized() : (c - a).Normalized();
        var bend = pole - a;
        bend -= dir * bend.Dot(dir);
        if (bend.LengthSquared() < 1e-8f) { bend = (b - a) - dir * (b - a).Dot(dir); if (bend.LengthSquared() < 1e-8f) return; }
        bend = bend.Normalized();
        float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
        var newB = a + dir * (la * cosA) + bend * (la * Mathf.Sqrt(1f - cosA * cosA));
        var newC = a + dir * d;

        var r1 = Between(b - a, newB - a);
        var upperBasis = new Basis(r1) * gUpper.Basis;
        var lowerDir = new Basis(r1) * (c - b);
        var r2 = Between(lowerDir, newC - newB);
        var lowerBasis = new Basis(r2 * r1) * gLower.Basis;

        int parent = skel.GetBoneParent(upper);
        var parentBasis = parent >= 0 ? skel.GetBoneGlobalPose(parent).Basis : Basis.Identity;
        var localUpper = (parentBasis.Inverse() * upperBasis).GetRotationQuaternion();
        var localLower = (upperBasis.Inverse() * lowerBasis).GetRotationQuaternion();
        if (weight < 1f)
        {
            localUpper = skel.GetBonePoseRotation(upper).Slerp(localUpper, weight);
            localLower = skel.GetBonePoseRotation(lower).Slerp(localLower, weight);
        }
        skel.SetBonePoseRotation(upper, localUpper);
        skel.SetBonePoseRotation(lower, localLower);
    }

    /// <summary>Rotation la plus courte qui amène la direction <paramref name="from"/> sur <paramref name="to"/>.</summary>
    static Quaternion Between(Vector3 from, Vector3 to)
    {
        var f = from.Normalized();
        var t = to.Normalized();
        float dot = Mathf.Clamp(f.Dot(t), -1f, 1f);
        if (dot > 0.99999f) return Quaternion.Identity;
        var axis = f.Cross(t);
        if (axis.LengthSquared() < 1e-10f) axis = Mathf.Abs(f.X) < 0.9f ? f.Cross(Vector3.Right) : f.Cross(Vector3.Up);
        return new Quaternion(axis.Normalized(), Mathf.Acos(dot));
    }

    /// <summary>Tourne un os dans l'espace du squelette (autour de son origine), ses enfants suivent.</summary>
    static void RotateGlobal(Skeleton3D skel, int bone, Quaternion rot)
    {
        var g = skel.GetBoneGlobalPose(bone);
        SetGlobalBasis(skel, bone, new Basis(rot) * g.Basis);
    }

    /// <summary>Donne à un os une orientation dans l'espace du squelette, sans toucher à sa position.</summary>
    static void SetGlobalBasis(Skeleton3D skel, int bone, Basis global)
    {
        int parent = skel.GetBoneParent(bone);
        var parentBasis = parent >= 0 ? skel.GetBoneGlobalPose(parent).Basis : Basis.Identity;
        skel.SetBonePoseRotation(bone, (parentBasis.Inverse() * global).GetRotationQuaternion());
    }
}
