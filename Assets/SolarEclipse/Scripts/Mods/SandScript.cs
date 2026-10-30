using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SolarEclipse.Mods
{
    // =====================================================================================================================
    // SandScript: the small, safe scripting language of Advanced Modding. Tokenizer + parser (to an AST) + interpreter.
    // No Unity dependency: the game talks to it through ISandHost, so this file can be compiled and tested on its own.
    // A script can only call the commands in SandApi; it has no reflection, files, network or system access.
    // =====================================================================================================================

    public enum SKind { Num, Str, Bool, Ent }

    /// <summary>A SandScript value: number, text, true/false, or someone (entity handle, 0 = nobody).</summary>
    public struct SVal
    {
        public SKind Kind;
        public double N;
        public string S;

        public static SVal Num(double v) => new SVal { Kind = SKind.Num, N = double.IsNaN(v) || double.IsInfinity(v) ? 0 : v };
        public static SVal Str(string s)
        {
            s = s ?? "";
            if (s.Length > SandApi.MaxStringLength) s = s.Substring(0, SandApi.MaxStringLength);
            return new SVal { Kind = SKind.Str, S = s };
        }
        public static SVal Bool(bool b) => new SVal { Kind = SKind.Bool, N = b ? 1 : 0 };
        public static SVal Ent(int id) => new SVal { Kind = SKind.Ent, N = id };
        public static readonly SVal Zero = Num(0), Nobody = Ent(0);

        public bool Truthy => Kind == SKind.Str ? !string.IsNullOrEmpty(S) : N != 0;
        public double AsNum => Kind == SKind.Str ? (double.TryParse(S, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0) : N;
        public int EntId => Kind == SKind.Ent ? (int)N : 0;
        public string AsText => ToString();

        public override string ToString()
        {
            switch (Kind)
            {
                case SKind.Str: return S ?? "";
                case SKind.Bool: return N != 0 ? "true" : "false";
                case SKind.Ent: return N != 0 ? "someone" : "nobody";
                default:
                    return Math.Abs(N - Math.Round(N)) < 1e-9 ? ((long)Math.Round(N)).ToString(CultureInfo.InvariantCulture) : N.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        public static bool Same(SVal a, SVal b)
        {
            if (a.Kind == SKind.Str || b.Kind == SKind.Str) return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
            if (a.Kind == SKind.Ent || b.Kind == SKind.Ent) return a.Kind == b.Kind && a.N == b.N;
            return Math.Abs(a.N - b.N) < 1e-9;
        }
    }

    /// <summary>A script error with the (1-based) line it happened on.</summary>
    public sealed class SandError : Exception
    {
        public readonly int Line;
        public SandError(int line, string msg) : base(msg) { Line = line; }
        public string Pretty => Line > 0 ? "Line " + Line + ": " + Message : Message;
    }

    /// <summary>What the game provides to a running script (an NPC brain, a weapon...). Implementations clamp everything.</summary>
    public interface ISandHost
    {
        double Now { get; }                                 // game time in seconds
        int Entity(string word);                            // player/target/npc/attacker/self -> handle (0 = nobody)
        bool GetProp(string name, out SVal value);          // health, speed, ...
        void SetProp(string name, SVal value);
        SVal Call(string name, SVal[] args);                // commands and the non-math functions
        void Log(string text);
    }

    // ------------------------------------------------------------------------------------------------------------- tokens

    enum T { Num, Str, Ident, Op, Colon, Comma, LParen, RParen, End }

    struct Tok
    {
        public T Type;
        public string Text;
        public double Num;
        public int Col;
        public override string ToString() => Type == T.End ? "end of line" : Type == T.Str ? "\"" + Text + "\"" : "'" + Text + "'";
    }

    sealed class Line
    {
        public int No, Indent;
        public List<Tok> Toks = new List<Tok>();
        public string Raw;
    }

    static class Lexer
    {
        public static List<Line> Lex(string src)
        {
            var lines = new List<Line>();
            var raw = (src ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (raw.Length > SandApi.MaxLines) throw new SandError(SandApi.MaxLines + 1, "Script is too long (max " + SandApi.MaxLines + " lines).");
            for (int li = 0; li < raw.Length; li++)
            {
                string s = raw[li];
                int no = li + 1;
                if (s.Length > SandApi.MaxLineLength) throw new SandError(no, "Line is too long (max " + SandApi.MaxLineLength + " characters).");
                int indent = 0, i = 0;
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) { indent += s[i] == '\t' ? 4 : 1; i++; }
                var line = new Line { No = no, Indent = indent, Raw = s };
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ' ' || c == '\t') { i++; continue; }
                    if (c == '#' || (c == '/' && i + 1 < s.Length && s[i + 1] == '/')) break; // comment
                    var tok = new Tok { Col = i + 1 };
                    if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                    {
                        int st = i;
                        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                        if (!double.TryParse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture, out tok.Num))
                            throw new SandError(no, "'" + s.Substring(st, i - st) + "' is not a number.");
                        tok.Type = T.Num; tok.Text = s.Substring(st, i - st);
                    }
                    else if (c == '"' || c == '\'')
                    {
                        char q = c; int st = ++i;
                        var sb = new StringBuilder();
                        while (i < s.Length && s[i] != q) sb.Append(s[i++]);
                        if (i >= s.Length) throw new SandError(no, "Text is missing its closing quote " + q + ".");
                        i++;
                        if (sb.Length > SandApi.MaxStringLength) throw new SandError(no, "Text is too long (max " + SandApi.MaxStringLength + ").");
                        tok.Type = T.Str; tok.Text = sb.ToString();
                    }
                    else if (char.IsLetter(c) || c == '_')
                    {
                        int st = i;
                        while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                        tok.Type = T.Ident; tok.Text = s.Substring(st, i - st).ToLowerInvariant();
                    }
                    else if (c == ':') { tok.Type = T.Colon; tok.Text = ":"; i++; }
                    else if (c == ',') { tok.Type = T.Comma; tok.Text = ","; i++; }
                    else if (c == '(') { tok.Type = T.LParen; tok.Text = "("; i++; }
                    else if (c == ')') { tok.Type = T.RParen; tok.Text = ")"; i++; }
                    else
                    {
                        string two = i + 1 < s.Length ? s.Substring(i, 2) : "";
                        if (two == "<=" || two == ">=" || two == "==" || two == "!=" || two == "<>") { tok.Type = T.Op; tok.Text = two == "<>" ? "!=" : two; i += 2; }
                        else if ("+-*/%<>=".IndexOf(c) >= 0) { tok.Type = T.Op; tok.Text = c.ToString(); i++; }
                        else throw new SandError(no, "Unexpected character '" + c + "'.");
                    }
                    line.Toks.Add(tok);
                }
                if (line.Toks.Count > 0) lines.Add(line);
            }
            return lines;
        }
    }

    // ------------------------------------------------------------------------------------------------------------- AST

    public abstract class SExpr { public int Line; }
    sealed class ELit : SExpr { public SVal V; }
    sealed class EVar : SExpr { public string Name; }
    sealed class EProp : SExpr { public string Name; }
    sealed class EEnt : SExpr { public string Word; }
    sealed class EUn : SExpr { public string Op; public SExpr A; }
    sealed class EBin : SExpr { public string Op; public SExpr A, B; }
    sealed class ECall : SExpr { public string Name; public SExpr[] Args; }

    public abstract class SStmt { public int Line; }
    sealed class SSetVar : SStmt { public string Name; public SExpr Value; }
    sealed class SSetProp : SStmt { public string Name; public SExpr Value; }
    sealed class SIf : SStmt { public SExpr Cond; public List<SStmt> Then, Else; }
    sealed class SRepeat : SStmt { public SExpr Count; public List<SStmt> Body; }
    sealed class SWhile : SStmt { public SExpr Cond; public List<SStmt> Body; }
    sealed class SWait : SStmt { public SExpr Secs; }
    sealed class SCmd : SStmt { public string Name; public SExpr[] Args; }
    sealed class SReturn : SStmt { }

    /// <summary>One "on ...:" / "every N:" block.</summary>
    public sealed class SandHandler
    {
        public string Event;     // SandApi.EventSpec.Name
        public double Number;    // every N / near player N
        public int Line;
        internal List<SStmt> Body;
    }

    /// <summary>A parsed, checked script, ready to run any number of times.</summary>
    public sealed class SandProgram
    {
        public ScriptTarget Target;
        public readonly List<SandHandler> Handlers = new List<SandHandler>();
        public readonly HashSet<string> Variables = new HashSet<string>();
        public int StatementCount;

        public bool Has(string evt) { foreach (var h in Handlers) if (h.Event == evt) return true; return false; }

        /// <summary>Parses and checks a script. Throws SandError (with a line number) when something is wrong.</summary>
        public static SandProgram Parse(string src, ScriptTarget target)
        {
            if (src != null && Encoding.UTF8.GetByteCount(src) > SandApi.MaxSourceBytes)
                throw new SandError(0, "Script is too big (max " + SandApi.MaxSourceBytes / 1024 + " KB).");
            return new Parser(Lexer.Lex(src), target).Run();
        }

        /// <summary>Null when the script is fine, otherwise a friendly "Line N: ..." message.</summary>
        public static string Check(string src, ScriptTarget target)
        {
            try { Parse(src, target); return null; }
            catch (SandError e) { return e.Pretty; }
            catch (Exception e) { return "Could not read the script: " + e.Message; }
        }
    }

    // ------------------------------------------------------------------------------------------------------------- parser

    sealed class Parser
    {
        readonly List<Line> lines;
        readonly ScriptTarget target;
        readonly SandProgram prog = new SandProgram();
        int li;           // current line index
        List<Tok> toks;   // tokens of the line being parsed
        int ti, lineNo, depth;

        public Parser(List<Line> lines, ScriptTarget target) { this.lines = lines; this.target = target; prog.Target = target; }

        public SandProgram Run()
        {
            // Pass 1: every name that is ever "set" is a variable (so later lines may read it before the set runs).
            foreach (var l in lines)
                for (int k = 0; k + 1 < l.Toks.Count; k++)
                    if (l.Toks[k].Type == T.Ident && l.Toks[k].Text == "set" && l.Toks[k + 1].Type == T.Ident)
                    {
                        var n = l.Toks[k + 1].Text;
                        if (SandApi.FindProp(n) != null) continue;
                        if (SandApi.IsKeyword(n) || SandApi.IsEntity(n) || SandApi.FindCmd(n) != null)
                            throw new SandError(l.No, "'" + n + "' is a reserved word and can't be a variable name.");
                        prog.Variables.Add(n);
                        if (prog.Variables.Count > SandApi.MaxVariables) throw new SandError(l.No, "Too many variables (max " + SandApi.MaxVariables + ").");
                    }

            li = 0;
            if (lines.Count == 0) throw new SandError(1, "The script is empty. Start with an event, e.g. 'on spawn:'.");
            while (li < lines.Count)
            {
                var l = lines[li];
                if (l.Indent != 0) throw new SandError(l.No, "This line is indented but isn't inside an event. Events start at the left edge.");
                prog.Handlers.Add(ParseHandler(l));
            }
            if (prog.Handlers.Count == 0) throw new SandError(1, "No events found. Start with e.g. 'on spawn:'.");
            return prog;
        }

        // ------------------------------------------------------------------ helpers

        void Begin(Line l) { toks = l.Toks; ti = 0; lineNo = l.No; }
        Tok Peek => ti < toks.Count ? toks[ti] : new Tok { Type = T.End, Text = "" };
        Tok PeekAt(int o) => ti + o < toks.Count ? toks[ti + o] : new Tok { Type = T.End, Text = "" };
        Tok Next() { var t = Peek; if (ti < toks.Count) ti++; return t; }
        bool AtEnd => ti >= toks.Count;
        bool IsWord(string w) => Peek.Type == T.Ident && Peek.Text == w;
        SandError Err(string msg) => new SandError(lineNo, msg);

        void ExpectColonEnd(string what)
        {
            if (Peek.Type != T.Colon) throw Err("Expected ':' at the end of " + what + ", found " + Peek + ".");
            Next();
            if (!AtEnd) throw Err("Nothing may follow the ':' on the same line. Put the commands on the next lines, indented.");
        }

        void ExpectEnd()
        {
            if (!AtEnd) throw Err("Didn't expect " + Peek + " here.");
        }

        // ------------------------------------------------------------------ events

        SandHandler ParseHandler(Line l)
        {
            Begin(l);
            if (IsWord("on")) Next();
            var words = new StringBuilder();
            double num = 0; bool hasNum = false;
            while (!AtEnd && Peek.Type != T.Colon)
            {
                var t = Next();
                if (t.Type == T.Ident) { if (words.Length > 0) words.Append(' '); words.Append(t.Text); }
                else if (t.Type == T.Num && !hasNum) { num = t.Num; hasNum = true; }
                else throw Err("Unexpected " + t + " in the event name.");
            }
            string name = words.ToString();
            if (name == "see players") name = "see player";
            SandApi.EventSpec spec = null;
            foreach (var e in SandApi.Events) if (e.Name == name) spec = e;
            if (spec == null)
            {
                if (name.Length == 0) throw Err("Expected an event like 'on spawn:'.");
                var list = new List<string>();
                foreach (var e in SandApi.Events) if (SandApi.Allowed(target, e.Target)) list.Add(e.Example.TrimEnd(':'));
                throw Err("Unknown event '" + name + "'. Events: " + string.Join(", ", list.ToArray()) + ".");
            }
            if (!SandApi.Allowed(target, spec.Target)) throw Err("'" + name + "' can't be used in a " + (target == ScriptTarget.Npc ? "NPC" : "weapon") + " script.");
            if (spec.HasNumber && !hasNum) throw Err("'" + name + "' needs a number, e.g. '" + spec.Example + "'.");
            if (!spec.HasNumber && hasNum) throw Err("'" + name + "' doesn't take a number.");
            if (spec.HasNumber && (num < spec.NumMin || num > spec.NumMax)) throw Err("The number for '" + name + "' must be between " + spec.NumMin + " and " + spec.NumMax + ".");
            ExpectColonEnd("the event");
            li++;
            var h = new SandHandler { Event = name, Number = num, Line = l.No };
            h.Body = ParseBlock(0, "the event");
            return h;
        }

        /// <summary>Parses the indented lines after a header whose indent is parentIndent.</summary>
        List<SStmt> ParseBlock(int parentIndent, string what)
        {
            int headerNo = lines[li - 1].No;
            if (li >= lines.Count || lines[li].Indent <= parentIndent)
                throw new SandError(headerNo, "Expected indented lines after the ':' of " + what + ".");
            if (++depth > SandApi.MaxNesting) throw new SandError(lines[li].No, "Blocks are nested too deeply (max " + SandApi.MaxNesting + ").");
            int indent = lines[li].Indent;
            var list = new List<SStmt>();
            while (li < lines.Count && lines[li].Indent > parentIndent)
            {
                if (lines[li].Indent != indent) throw new SandError(lines[li].No, "The indentation doesn't match the lines above it.");
                list.Add(ParseStatement(indent));
            }
            depth--;
            return list;
        }

        // ------------------------------------------------------------------ statements

        SStmt ParseStatement(int indent)
        {
            var l = lines[li];
            Begin(l);
            prog.StatementCount++;
            var first = Peek;
            if (first.Type != T.Ident) throw Err("A line must start with a command, found " + first + ".");
            if (first.Text == "on" || first.Text == "every") throw Err("Events can't be inside other events. Move this line to the left edge.");
            if (first.Text == "else" || first.Text == "elif") throw Err("'else' without an 'if' above it.");

            if (first.Text == "if") { Next(); return ParseIf(indent); }

            if (first.Text == "repeat")
            {
                Next();
                var count = Expr();
                if (IsWord("times")) Next();
                ExpectColonEnd("'repeat'");
                li++;
                return new SRepeat { Line = l.No, Count = count, Body = ParseBlock(indent, "'repeat'") };
            }
            if (first.Text == "while")
            {
                Next();
                var cond = Expr();
                ExpectColonEnd("'while'");
                li++;
                return new SWhile { Line = l.No, Cond = cond, Body = ParseBlock(indent, "'while'") };
            }

            SStmt st;
            if (first.Text == "wait")
            {
                Next();
                if (AtEnd) throw Err("'wait' needs a number of seconds, e.g. 'wait 2'.");
                st = new SWait { Line = l.No, Secs = Expr() };
                if (IsWord("seconds") || IsWord("second") || IsWord("s")) Next();
            }
            else if (first.Text == "return") { Next(); st = new SReturn { Line = l.No }; }
            else if (first.Text == "set") { Next(); st = ParseSet(); }
            else st = ParseCommand();
            ExpectEnd();
            li++;
            return st;
        }

        SStmt ParseIf(int indent)
        {
            int no = lineNo;
            var cond = Expr();
            ExpectColonEnd("'if'");
            li++;
            var s = new SIf { Line = no, Cond = cond, Then = ParseBlock(indent, "'if'") };
            if (li < lines.Count && lines[li].Indent == indent && lines[li].Toks[0].Type == T.Ident)
            {
                var w = lines[li].Toks[0].Text;
                if (w == "elif" || (w == "else" && lines[li].Toks.Count > 1 && lines[li].Toks[1].Type == T.Ident && lines[li].Toks[1].Text == "if"))
                {
                    Begin(lines[li]);
                    Next(); if (w == "else") Next();
                    prog.StatementCount++;
                    s.Else = new List<SStmt> { ParseIf(indent) };
                }
                else if (w == "else")
                {
                    Begin(lines[li]);
                    Next();
                    ExpectColonEnd("'else'");
                    li++;
                    s.Else = ParseBlock(indent, "'else'");
                }
            }
            return s;
        }

        SStmt ParseSet()
        {
            if (Peek.Type != T.Ident) throw Err("'set' needs a name, e.g. 'set speed 5' or 'set count = 0'.");
            var name = Next().Text;
            if (Peek.Type == T.Op && (Peek.Text == "=" || Peek.Text == "==")) Next();
            else if (IsWord("to")) Next();
            if (AtEnd) throw Err("'set " + name + "' needs a value.");
            var prop = SandApi.FindProp(name);
            if (prop != null)
            {
                if (!SandApi.Allowed(target, prop.Target)) throw Err("'" + name + "' doesn't exist in a " + TargetName + " script.");
                if (!prop.Settable) throw Err("'" + name + "' can only be read, not set.");
                SExpr v = prop.IsText && Peek.Type == T.Ident && !prog.Variables.Contains(Peek.Text) ? new ELit { Line = lineNo, V = SVal.Str(Next().Text) } : Expr();
                return new SSetProp { Line = lineNo, Name = name, Value = v };
            }
            return new SSetVar { Line = lineNo, Name = name, Value = Expr() };
        }

        string TargetName => target == ScriptTarget.Npc ? "NPC" : target == ScriptTarget.Weapon ? "weapon" : "mod";

        SStmt ParseCommand()
        {
            var t = Next();
            var cmd = SandApi.FindCmd(t.Text);
            if (cmd == null)
            {
                if (prog.Variables.Contains(t.Text) || SandApi.FindProp(t.Text) != null)
                    throw Err("To change '" + t.Text + "' write 'set " + t.Text + " = ...'.");
                throw Err("Unknown command '" + t.Text + "'." + Suggest(t.Text));
            }
            if (cmd.IsFunc) throw Err("'" + cmd.Name + "' gives a value; use it inside 'set' or 'if', e.g. '" + cmd.Example + "'.");
            if (!SandApi.Allowed(target, cmd.Target)) throw Err("'" + cmd.Name + "' can't be used in a " + TargetName + " script.");
            return new SCmd { Line = lineNo, Name = cmd.Name, Args = Args(cmd, false) };
        }

        static string Suggest(string w)
        {
            string best = null; int bd = 3;
            foreach (var c in SandApi.Commands)
            {
                int d = Lev(w, c.Name);
                if (d < bd) { bd = d; best = c.Name; }
            }
            return best != null ? " Did you mean '" + best + "'?" : "";
        }

        static int Lev(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return d[a.Length, b.Length];
        }

        /// <summary>Arguments: "name a, b" / "name a b" / "name(a, b)" (the last form inside expressions).</summary>
        SExpr[] Args(SandApi.Cmd cmd, bool inExpr)
        {
            var args = new List<SExpr>();
            bool paren = Peek.Type == T.LParen && (cmd.MaxArgs != 1 || inExpr);
            if (paren)
            {
                Next();
                while (Peek.Type != T.RParen)
                {
                    if (AtEnd) throw Err("Missing ')' after the arguments of '" + cmd.Name + "'.");
                    args.Add(Arg(cmd, args.Count, false));
                    if (Peek.Type == T.Comma) Next();
                    else if (Peek.Type != T.RParen) throw Err("Expected ',' or ')' in '" + cmd.Name + "(...)'.");
                }
                Next();
            }
            else
            {
                while (args.Count < cmd.MaxArgs && CanStartArg(inExpr, args.Count >= cmd.MinArgs))
                {
                    args.Add(Arg(cmd, args.Count, inExpr));
                    if (!inExpr && Peek.Type == T.Comma) Next();
                }
            }
            if (args.Count < cmd.MinArgs) throw Err("'" + cmd.Name + "' needs " + cmd.MinArgs + " value" + (cmd.MinArgs == 1 ? "" : "s") + ", e.g. '" + cmd.Example + "'.");
            if (args.Count > cmd.MaxArgs) throw Err("'" + cmd.Name + "' takes at most " + cmd.MaxArgs + " value" + (cmd.MaxArgs == 1 ? "" : "s") + ".");
            return args.ToArray();
        }

        bool CanStartArg(bool inExpr, bool optional)
        {
            var p = Peek;
            switch (p.Type)
            {
                case T.Num: case T.Str: case T.LParen: return true;
                case T.Op: return p.Text == "-" && !optional;
                case T.Ident:
                    if (p.Text == "and" || p.Text == "or" || p.Text == "times" || p.Text == "seconds") return false;
                    if (optional && inExpr) return SandApi.IsEntity(p.Text); // optional args in expressions: only obvious ones
                    return true;
                default: return false;
            }
        }

        SExpr Arg(SandApi.Cmd cmd, int index, bool inExpr)
        {
            char k = cmd.Kind(index);
            if (k == 's' && Peek.Type == T.Ident && !prog.Variables.Contains(Peek.Text) && SandApi.FindProp(Peek.Text) == null)
                return new ELit { Line = lineNo, V = SVal.Str(Next().Text) }; // bare names: give_weapon knife, spawn_effect blood
            var e = inExpr ? Unary() : Expr();
            if (k == 'e' && e is ELit lit && lit.V.Kind != SKind.Ent)
                throw Err("'" + cmd.Name + "' needs someone: " + string.Join(", ", SandApi.EntityWords) + ".");
            return e;
        }

        // ------------------------------------------------------------------ expressions

        SExpr Expr()
        {
            if (++depth > SandApi.MaxExprDepth + SandApi.MaxNesting) throw Err("Expression is nested too deeply.");
            var e = Or();
            depth--;
            return e;
        }

        SExpr Or()
        {
            var a = And();
            while (IsWord("or")) { Next(); a = new EBin { Line = lineNo, Op = "or", A = a, B = And() }; }
            return a;
        }

        SExpr And()
        {
            var a = Not();
            while (IsWord("and")) { Next(); a = new EBin { Line = lineNo, Op = "and", A = a, B = Not() }; }
            return a;
        }

        SExpr Not()
        {
            if (IsWord("not")) { Next(); return new EUn { Line = lineNo, Op = "not", A = Not() }; }
            return Cmp();
        }

        SExpr Cmp()
        {
            var a = Add();
            if (Peek.Type == T.Op && (Peek.Text == "<" || Peek.Text == ">" || Peek.Text == "<=" || Peek.Text == ">=" || Peek.Text == "==" || Peek.Text == "!=" || Peek.Text == "="))
            {
                var op = Next().Text;
                if (op == "=") op = "==";
                a = new EBin { Line = lineNo, Op = op, A = a, B = Add() };
            }
            else if (IsWord("is"))
            {
                Next();
                bool neg = false;
                if (IsWord("not")) { Next(); neg = true; }
                a = new EBin { Line = lineNo, Op = neg ? "!=" : "==", A = a, B = Add() };
            }
            return a;
        }

        SExpr Add()
        {
            var a = Mul();
            while (Peek.Type == T.Op && (Peek.Text == "+" || Peek.Text == "-")) { var op = Next().Text; a = new EBin { Line = lineNo, Op = op, A = a, B = Mul() }; }
            return a;
        }

        SExpr Mul()
        {
            var a = Unary();
            while (Peek.Type == T.Op && (Peek.Text == "*" || Peek.Text == "/" || Peek.Text == "%")) { var op = Next().Text; a = new EBin { Line = lineNo, Op = op, A = a, B = Unary() }; }
            return a;
        }

        SExpr Unary()
        {
            if (++depth > SandApi.MaxExprDepth + SandApi.MaxNesting) throw Err("Expression is nested too deeply.");
            SExpr r;
            if (Peek.Type == T.Op && Peek.Text == "-") { Next(); r = new EUn { Line = lineNo, Op = "-", A = Unary() }; }
            else r = Primary();
            depth--;
            return r;
        }

        SExpr Primary()
        {
            var t = Next();
            switch (t.Type)
            {
                case T.Num: return new ELit { Line = lineNo, V = SVal.Num(t.Num) };
                case T.Str: return new ELit { Line = lineNo, V = SVal.Str(t.Text) };
                case T.LParen:
                    {
                        var e = Expr();
                        if (Next().Type != T.RParen) throw Err("Missing ')'.");
                        return e;
                    }
                case T.Ident:
                    {
                        var n = t.Text;
                        if (n == "true" || n == "yes") return new ELit { Line = lineNo, V = SVal.Bool(true) };
                        if (n == "false" || n == "no") return new ELit { Line = lineNo, V = SVal.Bool(false) };
                        if (n == "nobody" || n == "nothing") return new ELit { Line = lineNo, V = SVal.Nobody };
                        if (SandApi.IsEntity(n)) return new EEnt { Line = lineNo, Word = n };
                        if (prog.Variables.Contains(n)) return new EVar { Line = lineNo, Name = n };
                        var prop = SandApi.FindProp(n);
                        if (prop != null)
                        {
                            if (!SandApi.Allowed(target, prop.Target)) throw Err("'" + n + "' doesn't exist in a " + TargetName + " script.");
                            return new EProp { Line = lineNo, Name = n };
                        }
                        var cmd = SandApi.FindCmd(n);
                        if (cmd != null)
                        {
                            if (!cmd.IsFunc) throw Err("'" + n + "' is a command and gives no value. Put it on its own line.");
                            if (!SandApi.Allowed(target, cmd.Target)) throw Err("'" + n + "' can't be used in a " + TargetName + " script.");
                            return new ECall { Line = lineNo, Name = n, Args = Args(cmd, true) };
                        }
                        if (SandApi.IsKeyword(n)) throw Err("Didn't expect '" + n + "' here.");
                        throw Err("Unknown name '" + n + "'. Set it first with 'set " + n + " = 0'.");
                    }
                case T.End: throw Err("A value is missing at the end of the line.");
                default: throw Err("Didn't expect " + t + " here.");
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------- runtime

    /// <summary>
    /// Runs one SandProgram for one owner (an NPC, a weapon). Events start "threads" that may wait; every frame Tick runs
    /// them with a step budget. Errors are caught here: a broken script is switched off, it never crashes the game.
    /// </summary>
    public sealed class SandRuntime
    {
        sealed class Frame
        {
            public List<SStmt> Block;
            public int Pc;
            public SStmt Loop;   // SRepeat / SWhile owning this frame
            public int Left;     // repeat: iterations left; while: iterations done
        }

        sealed class Thread
        {
            public SandHandler Handler;
            public readonly List<Frame> Stack = new List<Frame>();
            public double WaitUntil;
            public int StepsSinceWait;
            public Dictionary<string, SVal> Ctx;
        }

        public readonly SandProgram Program;
        readonly ISandHost host;
        readonly Dictionary<string, SVal> vars = new Dictionary<string, SVal>();
        readonly List<Thread> threads = new List<Thread>();
        readonly Dictionary<SandHandler, double> nextEvery = new Dictionary<SandHandler, double>();
        readonly System.Random rng;
        int budget, errors;
        Thread current;

        public bool Disabled { get; private set; }
        public string LastError { get; private set; }
        public int SlowFrames { get; private set; }        // frames where the budget ran out ("too slow")
        public bool TooSlow => SlowFrames > 30;
        public long TotalSteps { get; private set; }
        public int Running => threads.Count;

        public SandRuntime(SandProgram program, ISandHost host, int seed = 0)
        {
            Program = program; this.host = host;
            rng = new System.Random(seed == 0 ? Environment.TickCount : seed);
            foreach (var v in program.Variables) vars[v] = SVal.Zero;
            budget = SandApi.StepsPerFrame;
        }

        public SVal GetVar(string n) => vars.TryGetValue(n, out var v) ? v : SVal.Zero;

        /// <summary>Starts every handler for this event (skipped if that handler is still running).</summary>
        public void Fire(string evt, Dictionary<string, SVal> ctx = null, double number = double.NaN)
        {
            if (Disabled) return;
            foreach (var h in Program.Handlers)
            {
                if (h.Event != evt) continue;
                if (!double.IsNaN(number) && Math.Abs(h.Number - number) > 1e-9) continue;
                Start(h, ctx);
            }
        }

        /// <summary>Starts one handler and runs it at once (with what's left of this frame's budget).</summary>
        public void Start(SandHandler h, Dictionary<string, SVal> ctx)
        {
            if (Disabled) return;
            foreach (var t in threads) if (t.Handler == h) return;
            if (threads.Count >= SandApi.MaxThreads) return;
            var th = new Thread { Handler = h, Ctx = ctx };
            th.Stack.Add(new Frame { Block = h.Body });
            threads.Add(th);
            if (budget <= 0) return; // out of budget this frame: it starts on the next Tick
            RunThread(th);
            if (th.Stack.Count == 0) threads.Remove(th);
        }

        /// <summary>Call once per frame: "every N" timers, then resumes waiting threads.</summary>
        public void Tick()
        {
            if (Disabled) return;
            budget = SandApi.StepsPerFrame;
            double now = host.Now;
            foreach (var h in Program.Handlers)
            {
                if (h.Event == "every")
                {
                    if (!nextEvery.TryGetValue(h, out var due)) { nextEvery[h] = now + h.Number; continue; }
                    if (now >= due) { nextEvery[h] = now + Math.Max(SandApi.MinEvery, h.Number); Start(h, null); }
                }
                else if (h.Event == "tick") Start(h, null);
            }
            for (int i = 0; i < threads.Count && !Disabled; i++)
            {
                var th = threads[i];
                if (th.WaitUntil > now) continue;
                if (budget <= 0) { SlowFrames++; break; }
                RunThread(th);
                if (Disabled) break;
                if (th.Stack.Count == 0) { int k = threads.IndexOf(th); if (k >= 0) { threads.RemoveAt(k); if (k <= i) i--; } }
            }
        }

        public void StopAll() => threads.Clear();

        void RunThread(Thread th)
        {
            current = th;
            try { Exec(th); }
            catch (SandError e) { Fail(th, e.Pretty); }
            catch (Exception e) { Fail(th, "Line " + CurrentLine(th) + ": " + e.Message); }
            current = null;
        }

        static int CurrentLine(Thread th)
        {
            if (th.Stack.Count == 0) return th.Handler.Line;
            var f = th.Stack[th.Stack.Count - 1];
            return f.Pc < f.Block.Count ? f.Block[f.Pc].Line : th.Handler.Line;
        }

        void Fail(Thread th, string msg)
        {
            th.Stack.Clear();
            errors++;
            LastError = msg;
            try { host.Log("ERROR " + msg); } catch { }
            if (errors >= SandApi.MaxErrors) { Disabled = true; threads.Clear(); LastError = msg + " (script switched off after " + errors + " errors)"; }
        }

        void Exec(Thread th)
        {
            double now = host.Now;
            while (th.Stack.Count > 0)
            {
                if (budget <= 0) { SlowFrames++; return; } // paused, continues next frame
                var f = th.Stack[th.Stack.Count - 1];
                if (f.Pc >= f.Block.Count)
                {
                    // End of a block: loop again or pop.
                    if (f.Loop is SRepeat && --f.Left > 0) { f.Pc = 0; Cost(th, f.Loop.Line); continue; }
                    if (f.Loop is SWhile w)
                    {
                        if (++f.Left > SandApi.MaxLoopIterations) throw new SandError(w.Line, "This 'while' loop ran more than " + SandApi.MaxLoopIterations + " times.");
                        Cost(th, w.Line);
                        if (Eval(w.Cond).Truthy) { f.Pc = 0; continue; }
                    }
                    th.Stack.RemoveAt(th.Stack.Count - 1);
                    continue;
                }
                var s = f.Block[f.Pc++];
                Cost(th, s.Line);
                switch (s)
                {
                    case SSetVar sv: vars[sv.Name] = Eval(sv.Value); break;
                    case SSetProp sp: host.SetProp(sp.Name, Eval(sp.Value)); break;
                    case SIf si:
                        if (Eval(si.Cond).Truthy) Push(th, si.Then, null, 0, s.Line);
                        else if (si.Else != null) Push(th, si.Else, null, 0, s.Line);
                        break;
                    case SRepeat sr:
                        {
                            double n = Eval(sr.Count).AsNum;
                            int count = (int)SandApi.Clamp(Math.Floor(n), 0, SandApi.MaxRepeat);
                            if (count > 0) Push(th, sr.Body, sr, count, s.Line);
                            break;
                        }
                    case SWhile sw:
                        if (Eval(sw.Cond).Truthy) Push(th, sw.Body, sw, 0, s.Line);
                        break;
                    case SWait wt:
                        {
                            double secs = SandApi.Clamp(Eval(wt.Secs).AsNum, 0, SandApi.MaxWait);
                            th.WaitUntil = now + secs;
                            th.StepsSinceWait = 0;
                            return;
                        }
                    case SReturn _: th.Stack.Clear(); return;
                    case SCmd sc:
                        {
                            var a = new SVal[sc.Args.Length];
                            for (int i = 0; i < a.Length; i++) a[i] = Eval(sc.Args[i]);
                            if (sc.Name == "print") { host.Log(a.Length > 0 ? a[0].ToString() : ""); break; }
                            host.Call(sc.Name, a);
                            break;
                        }
                }
            }
        }

        void Push(Thread th, List<SStmt> block, SStmt loop, int left, int line)
        {
            if (th.Stack.Count > SandApi.MaxNesting + 2) throw new SandError(line, "Too deep.");
            th.Stack.Add(new Frame { Block = block, Loop = loop, Left = left });
        }

        void Cost(Thread th, int line)
        {
            budget--;
            TotalSteps++;
            if (++th.StepsSinceWait > SandApi.MaxStepsWithoutWait)
                throw new SandError(line, "Too slow: this event ran " + SandApi.MaxStepsWithoutWait + " steps without a 'wait'. Add 'wait 0.1' inside long loops.");
        }

        // ------------------------------------------------------------------ expressions

        SVal Eval(SExpr e, int d = 0)
        {
            if (d > SandApi.MaxExprDepth * 2) throw new SandError(e.Line, "Expression is nested too deeply.");
            if (current != null) Cost(current, e.Line);
            switch (e)
            {
                case ELit l: return l.V;
                case EVar v: return vars.TryGetValue(v.Name, out var val) ? val : SVal.Zero;
                case EEnt en: return SVal.Ent(host.Entity(en.Word));
                case EProp p:
                    if (current != null && current.Ctx != null && current.Ctx.TryGetValue(p.Name, out var cv)) return cv;
                    return host.GetProp(p.Name, out var pv) ? pv : SVal.Zero;
                case EUn u:
                    {
                        var a = Eval(u.A, d + 1);
                        return u.Op == "not" ? SVal.Bool(!a.Truthy) : SVal.Num(-a.AsNum);
                    }
                case EBin b:
                    {
                        if (b.Op == "and") return SVal.Bool(Eval(b.A, d + 1).Truthy && Eval(b.B, d + 1).Truthy);
                        if (b.Op == "or") return SVal.Bool(Eval(b.A, d + 1).Truthy || Eval(b.B, d + 1).Truthy);
                        var x = Eval(b.A, d + 1); var y = Eval(b.B, d + 1);
                        switch (b.Op)
                        {
                            case "+":
                                if (x.Kind == SKind.Str || y.Kind == SKind.Str) return SVal.Str(x.ToString() + y.ToString());
                                return SVal.Num(x.AsNum + y.AsNum);
                            case "-": return SVal.Num(x.AsNum - y.AsNum);
                            case "*": return SVal.Num(x.AsNum * y.AsNum);
                            case "/": return SVal.Num(Math.Abs(y.AsNum) < 1e-12 ? 0 : x.AsNum / y.AsNum);
                            case "%": return SVal.Num(Math.Abs(y.AsNum) < 1e-12 ? 0 : x.AsNum % y.AsNum);
                            case "<": return SVal.Bool(x.AsNum < y.AsNum);
                            case ">": return SVal.Bool(x.AsNum > y.AsNum);
                            case "<=": return SVal.Bool(x.AsNum <= y.AsNum);
                            case ">=": return SVal.Bool(x.AsNum >= y.AsNum);
                            case "==": return SVal.Bool(SVal.Same(x, y));
                            case "!=": return SVal.Bool(!SVal.Same(x, y));
                        }
                        throw new SandError(b.Line, "Unknown operator " + b.Op);
                    }
                case ECall c:
                    {
                        var a = new SVal[c.Args.Length];
                        for (int i = 0; i < a.Length; i++) a[i] = Eval(c.Args[i], d + 1);
                        return CallFunc(c.Name, a);
                    }
            }
            throw new SandError(e.Line, "Unknown expression.");
        }

        SVal CallFunc(string name, SVal[] a)
        {
            switch (name)
            {
                case "random":
                    {
                        double lo = SandApi.Clamp(a[0].AsNum, -1e6, 1e6), hi = SandApi.Clamp(a[1].AsNum, -1e6, 1e6);
                        if (hi < lo) { var t = lo; lo = hi; hi = t; }
                        return SVal.Num(Math.Floor(lo + rng.NextDouble() * (Math.Floor(hi) - Math.Floor(lo) + 1)));
                    }
                case "chance": return SVal.Bool(rng.NextDouble() * 100 < a[0].AsNum);
                case "abs": return SVal.Num(Math.Abs(a[0].AsNum));
                case "min": return SVal.Num(Math.Min(a[0].AsNum, a[1].AsNum));
                case "max": return SVal.Num(Math.Max(a[0].AsNum, a[1].AsNum));
                case "round": return SVal.Num(Math.Round(a[0].AsNum));
                default: return host.Call(name, a);
            }
        }
    }
}
