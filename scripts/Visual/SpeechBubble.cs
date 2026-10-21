using Godot;

namespace DogeballVR.Visual;

/// <summary>Bulle de texte monde (« PASSE ! », « PRISE ! », « KO ! ») : monte de 0,5 m en 1 s puis disparaît. Lisible à 10 m (lettres de 0,25 m).</summary>
public partial class SpeechBubble : Label3D
{
    const float Life = 1.0f;
    double age;

    public static void Spawn(Node context, string text, Vector3 position, Color color)
    {
        var scene = context.GetTree().CurrentScene;
        var b = new SpeechBubble
        {
            Text = text, FontSize = 96, PixelSize = 0.0035f, Modulate = color, OutlineModulate = Palette.Panel, OutlineSize = 24,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
        };
        scene.AddChild(b);
        b.GlobalPosition = position;
    }

    public override void _Process(double delta)
    {
        age += delta;
        Position += new Vector3(0, 0.5f * (float)delta, 0);
        var m = Modulate; m.A = Mathf.Clamp(1.6f - (float)age / Life * 1.6f + 0.6f, 0f, 1f);
        Modulate = m;
        if (age >= Life) QueueFree();
    }
}
