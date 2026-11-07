using System.Text;

/// The world-space lab UI (tablet "pads", wrist holo board, reaction footer) renders
/// with LiberationSans SDF, whose baked atlas is missing arrows, Greek and box glyphs
/// (→ ← ↑ ↓ Δ ⇌ ☑ ☐ ▶ …). Those showed as blank "missing-glyph" boxes on the tablets.
/// Rather than degrade the source chemistry data or regenerate the font atlas (fragile),
/// we map the unsupported glyphs to font-safe equivalents at DISPLAY time. Pure + tested.
public static class GlyphSafe
{
    /// Replace glyphs absent from LiberationSans SDF with readable ASCII/Latin-1 forms.
    public static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            switch (c)
            {
                case '→': sb.Append("->");     break; // → yields
                case '←': sb.Append("<-");     break; // ←
                case '↔': sb.Append("<->");    break; // ↔
                case '⇌': sb.Append("<=>");    break; // ⇌ equilibrium
                case '⇄': sb.Append("<=>");    break; // ⇄
                case '↑': sb.Append("(g)");    break; // ↑ gas evolved
                case '↓': sb.Append("(s)");    break; // ↓ precipitate
                case 'Δ':                                 // Δ (heat)
                case '∆': sb.Append("(heat)"); break; // ∆
                case '≡': sb.Append('=');      break; // ≡
                case '≈': sb.Append('~');      break; // ≈
                case '≠': sb.Append("!=");     break; // ≠
                case '≤': sb.Append("<=");     break; // ≤
                case '≥': sb.Append(">=");     break; // ≥
                case '☑':                                 // ☑ checked box
                case '✅': sb.Append("[x]");    break;
                case '☐': sb.Append("[ ]");    break; // ☐ empty box
                case '▶':                                 // ▶
                case '▸':
                case '►': sb.Append('»'); break; // » (font-safe)
                default: sb.Append(c); break;
            }
        }
        return Dashes(sb.ToString());
    }

    /// No em or en dashes on screen (house style). A dash joining two phrases becomes a
    /// comma ("Wash Bottle — 500 ml" -> "Wash Bottle, 500 ml"); a range between two numbers
    /// ("1–3") or a dash standing alone as a placeholder becomes a hyphen.
    public static string Dashes(string s)
    {
        if (string.IsNullOrEmpty(s) || (s.IndexOf('—') < 0 && s.IndexOf('–') < 0)) return s;
        var sb = new StringBuilder(s.Length + 4);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '—' && c != '–') { sb.Append(c); continue; }
            int a = sb.Length - 1; while (a >= 0 && sb[a] == ' ') a--;
            int b = i + 1; while (b < s.Length && s[b] == ' ') b++;
            bool before = a >= 0 && IsWordEdge(sb[a]);
            bool after = b < s.Length && IsWordEdge(s[b]);
            bool spaced = (i > 0 && s[i - 1] == ' ') || (i + 1 < s.Length && s[i + 1] == ' ');
            if (before && after && !(char.IsDigit(sb[a]) && char.IsDigit(s[b]) && !spaced))
            {
                sb.Length = a + 1;            // drop the space before the dash
                sb.Append(", ");
                i = b - 1;                    // and the spaces after it
            }
            else sb.Append('-');
        }
        return sb.ToString();
    }

    private static bool IsWordEdge(char c) => char.IsLetterOrDigit(c) || c == '>' || c == '<' || c == ')' || c == '(' || c == '"' || c == '\'' || c == '%';
}
