using System.Collections.Generic;
using System.Text;

namespace SolarEclipse
{
    /// <summary>
    /// Local profanity filter for chat. Applied to what YOU see (everyone's messages, including your own), so it
    /// works the same in singleplayer and multiplayer. Forced on for players who said they're under 13.
    /// Catches common disguises: l33t speak (sh1t, @ss), repeated letters (fuuuck), separators (f.u.c.k).
    /// </summary>
    public static class ChatFilter
    {
        public static readonly string[] AgeGroups = { "Not set", "Under 13", "13 - 17", "18 or older" };

        public static bool ForcedOn => Settings.Data.ageGroup == 1;
        public static bool Active => ForcedOn || Settings.Data.chatFilter;

        // Matched as the start of a word ("fuck" also catches "fucking", "fucker").
        static readonly string[] Stems =
        {
            "fuck", "fuk", "fck", "motherfuck", "shit", "bullshit", "bitch", "bastard", "cunt", "dick", "cock", "pussy",
            "asshole", "arsehole", "wank", "twat", "slut", "whore", "bollock", "piss", "jackass", "dumbass", "fag", "nigg",
            "retard", "porn", "nude", "dildo", "boob", "tits", "penis", "vagina", "rape", "kys",
        };
        // Matched only as a whole word (too short or too common as part of normal words).
        static readonly HashSet<string> Words = new HashSet<string>
        {
            "ass", "arse", "damn", "crap", "hell", "wtf", "stfu", "fu", "sex", "tit", "cum", "hoe", "prick", "bloody", "goddamn",
        };
        // Normal words that start with a stem and must NOT be censored.
        static readonly HashSet<string> Safe = new HashSet<string>
        {
            "dickens", "cocktail", "cockpit", "cockroach", "peacock", "shiitake", "scrape", "grape", "drape", "therapist", "titan",
            "titanic", "title", "titles", "pissarro", "assassin", "assess", "assets", "asset", "assist", "assume", "class", "pass",
            "hello", "shell", "shells", "helmet", "skyscraper", "rapeseed", "boobytrap", "cockatoo", "retardant", "cockerel",
        };

        public static string Apply(string text) => !Active ? text : Censor(text);

        /// <summary>Always filters, whatever the player's setting (mod names/descriptions shown to everyone).</summary>
        public static string Censor(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new StringBuilder(text);
            int i = 0;
            while (i < text.Length)
            {
                if (!char.IsLetterOrDigit(text[i]) && !IsLeet(text[i])) { i++; continue; }
                // Word = letters/digits/leet chars, allowing single separators between letters (f.u.c.k / f-u-c-k).
                int start = i;
                int end = i;
                while (end < text.Length)
                {
                    char c = text[end];
                    if (char.IsLetterOrDigit(c) || IsLeet(c)) { end++; continue; }
                    if ((c == '.' || c == '-' || c == '_' || c == '*') && end + 1 < text.Length && (char.IsLetterOrDigit(text[end + 1]) || IsLeet(text[end + 1])) && end > start) { end++; continue; }
                    break;
                }
                string raw = text.Substring(start, end - start);
                if (IsBad(raw))
                    for (int k = start; k < end; k++) if (!char.IsWhiteSpace(sb[k])) sb[k] = '*';
                i = end;
            }
            return sb.ToString();
        }

        static bool IsLeet(char c) => c == '@' || c == '$' || c == '!' || c == '+';

        static bool IsBad(string raw)
        {
            string w = Normalize(raw);
            if (w.Length < 2 || Safe.Contains(w)) return false;
            if (Words.Contains(w)) return true;
            string squeezed = Squeeze(w);
            if (Words.Contains(squeezed)) return true;
            foreach (var s in Stems)
                if (w.StartsWith(s) || squeezed.StartsWith(Squeeze(s))) return !Safe.Contains(w);
            return false;
        }

        /// <summary>Lowercase, strip separators, undo l33t speak.</summary>
        static string Normalize(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            foreach (char ch in raw.ToLowerInvariant())
            {
                switch (ch)
                {
                    case '@': case '4': sb.Append('a'); break;
                    case '3': sb.Append('e'); break;
                    case '1': case '!': case '|': sb.Append('i'); break;
                    case '0': sb.Append('o'); break;
                    case '$': case '5': sb.Append('s'); break;
                    case '7': case '+': sb.Append('t'); break;
                    case '.': case '-': case '_': case '*': break;
                    default: if (char.IsLetter(ch)) sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>"fuuuuck" → "fuck" (collapses repeated letters).</summary>
        static string Squeeze(string w)
        {
            var sb = new StringBuilder(w.Length);
            foreach (char c in w) if (sb.Length == 0 || sb[sb.Length - 1] != c) sb.Append(c);
            return sb.ToString();
        }
    }
}
