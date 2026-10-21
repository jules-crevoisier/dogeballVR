using System;
using System.Text;

namespace DogeballVR.Online;

/// <summary>
/// Code de partie à dicter à un ami. Deux formes :
/// « S » + identifiant du lobby Steam (base 32), ou adresse IPv4 + port (10 caractères, rejoint en direct, port ouvert ou UPnP).
/// Alphabet de Crockford : pas de I, L, O, U (pas de confusion à l'oral ni à l'écrit).
/// </summary>
public static class JoinCode
{
    const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string FromAddress(string ipv4, int port)
    {
        var parts = ipv4.Split('.');
        if (parts.Length != 4) throw new ArgumentException("Adresse IPv4 attendue", nameof(ipv4));
        ulong v = 0;
        foreach (var p in parts) v = (v << 8) | byte.Parse(p);
        v = (v << 16) | (ushort)port;
        return Group(Encode(v, 10));
    }

    public static string FromSteamLobby(ulong lobbyId) => "S" + Group(Encode(lobbyId, 13));

    /// <summary>Décode un code saisi (espaces, tirets et minuscules tolérés).</summary>
    public static bool TryParse(string code, out string address, out int port, out ulong steamLobby)
    {
        address = ""; port = 0; steamLobby = 0;
        var clean = new StringBuilder();
        foreach (char c0 in (code ?? "").ToUpperInvariant())
        {
            char c = c0 switch { 'O' => '0', 'I' or 'L' => '1', _ => c0 };
            if (c == '-' || c == ' ') continue;
            clean.Append(c);
        }
        var s = clean.ToString();
        if (s.StartsWith("S") && s.Length == 14)
            return TryDecode(s[1..], out steamLobby) && steamLobby != 0;
        if (s.Length != 10 || !TryDecode(s, out ulong v)) return false;
        port = (int)(v & 0xFFFF);
        v >>= 16;
        address = $"{(v >> 24) & 255}.{(v >> 16) & 255}.{(v >> 8) & 255}.{v & 255}";
        return port > 0;
    }

    static string Encode(ulong v, int length)
    {
        var chars = new char[length];
        for (int i = length - 1; i >= 0; i--) { chars[i] = Alphabet[(int)(v & 31)]; v >>= 5; }
        return new string(chars);
    }

    static bool TryDecode(string s, out ulong v)
    {
        v = 0;
        foreach (char c in s)
        {
            int d = Alphabet.IndexOf(c);
            if (d < 0) return false;
            v = (v << 5) | (uint)d;
        }
        return true;
    }

    static string Group(string s) => s.Length <= 5 ? s : s[..5] + "-" + Group(s[5..]);
}
