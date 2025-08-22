
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Reo.Core
{
    // ==========================
    // TOKENS
    // ==========================
    enum TokenKind
    {
        EOF,
        // Literals & identifiers
        Number, String, Ident,

        // Delimiters
        LParen, RParen, LBracket, RBracket, Comma, Dot, Colon,

        // Operators (post-preprocess English -> symbols)
        Plus, Minus, Star, Slash, Percent,
        Bang, AndAnd, OrOr,
        EqualEqual, BangEqual, Less, LessEqual, Greater, GreaterEqual,

        // Keywords
        Let, Be, Set, To, If, Then, Otherwise, While, Do, Repeat, Times, For, Each, In, Return, Say, End, Append, Remove, From, True, False, Increase, Decrease, Ask, By
    }

    readonly record struct Token(TokenKind Kind, string Text, int Pos);

    // ==========================
    // PREPROCESSOR (English -> symbols)
    // ==========================
    static class Preprocessor
    {
        public static string Normalize(string src)
        {
            var sb = new StringBuilder(src.Length);
            bool inStr = false;
            for (int i = 0; i < src.Length;)
            {
                char c = src[i];
                if (c == '"')
                {
                    inStr = !inStr;
                    sb.Append(c); i++;
                    continue;
                }
                if (inStr) { sb.Append(c); i++; continue; }

                if (MatchPhrase(src, i, "is greater than or equal to", out int adv)) { sb.Append(">="); i += adv; continue; }
                if (MatchPhrase(src, i, "is less than or equal to", out adv)) { sb.Append("<="); i += adv; continue; }
                if (MatchPhrase(src, i, "is greater than", out adv)) { sb.Append(">"); i += adv; continue; }
                if (MatchPhrase(src, i, "is less than", out adv)) { sb.Append("<"); i += adv; continue; }
                if (MatchPhrase(src, i, "is not equal to", out adv)) { sb.Append("!="); i += adv; continue; }
                if (MatchPhrase(src, i, "is not", out adv)) { sb.Append("!="); i += adv; continue; }
                if (MatchPhrase(src, i, "is equal to", out adv)) { sb.Append("=="); i += adv; continue; }
                if (MatchPhrase(src, i, "is at least", out adv)) { sb.Append(">="); i += adv; continue; }
                if (MatchPhrase(src, i, "is at most", out adv)) { sb.Append("<="); i += adv; continue; }

                if (MatchWord(src, i, "plus", out adv)) { sb.Append("+"); i += adv; continue; }
                if (MatchWord(src, i, "minus", out adv)) { sb.Append("-"); i += adv; continue; }
                if (MatchPhrase(src, i, "multiplied by", out adv) || MatchWord(src, i, "times", out adv)) { sb.Append("*"); i += adv; continue; }
                if (MatchPhrase(src, i, "divided by", out adv)) { sb.Append("/"); i += adv; continue; }
                if (MatchWord(src, i, "modulo", out adv) || MatchWord(src, i, "mod", out adv)) { sb.Append("%"); i += adv; continue; }
                if (MatchWord(src, i, "and", out adv)) { sb.Append("&&"); i += adv; continue; }
                if (MatchWord(src, i, "or", out adv)) { sb.Append("||"); i += adv; continue; }
                if (MatchWord(src, i, "not", out adv)) { sb.Append("!"); i += adv; continue; }

                sb.Append(c); i++;
            }
            return sb.ToString();
        }

        private static bool MatchPhrase(string s, int i, string phrase, out int adv)
        {
            adv = 0;
            if (i + phrase.Length > s.Length) return false;
            var span = s.AsSpan(i, phrase.Length);
            if (span.Equals(phrase.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                bool leftOk = i == 0 or !char.IsLetterOrDigit(s[i - 1]);
                bool rightOk = i + phrase.Length == s.Length or !char.IsLetterOrDigit(s[i + phrase.Length]);
                if (leftOk && rightOk) { adv = phrase.Length; return true; }
            }
            return false;
        }
        private static bool MatchWord(string s, int i, string word, out int adv)
        {
            adv = 0;
            if (i + word.Length > s.Length) return false;
            var span = s.AsSpan(i, word.Length);
            if (span.Equals(word.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                bool leftOk = i == 0 or !char.IsLetterOrDigit(s[i - 1]);
                bool rightOk = i + word.Length == s.Length or !char.IsLetterOrDigit(s[i + word.Length]);
                if (leftOk && rightOk) { adv = word.Length; return true; }
            }
            return false;
        }
    }

    // ==========================
    // LEXER
    // ==========================
    class Lexer
    {
        private readonly string _src;
        private int _pos;
        private static readonly Dictionary<string, TokenKind> _keywords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["let"] = TokenKind.Let,
            ["be"] = TokenKind.Be,
            ["set"] = TokenKind.Set,
            ["to"] = TokenKind.To,
            ["if"] = TokenKind.If,
            ["then"] = TokenKind.Then,
            ["otherwise"] = TokenKind.Otherwise,
            ["while"] = TokenKind.While,
            ["do"] = TokenKind.Do,
            ["repeat"] = TokenKind.Repeat,
            ["times"] = TokenKind.Times,
            ["for"] = TokenKind.For,
            ["each"] = TokenKind.Each,
            ["in"] = TokenKind.In,
            ["return"] = TokenKind.Return,
            ["say"] = TokenKind.Say,
            ["end"] = TokenKind.End,
            ["append"] = TokenKind.Append,
            ["remove"] = TokenKind.Remove,
            ["from"] = TokenKind.From,
            ["true"] = TokenKind.True,
            ["false"] = TokenKind.False,
            ["increase"] = TokenKind.Increase,
            ["decrease"] = TokenKind.Decrease,
            ["ask"] = TokenKind.Ask,
            ["by"] = TokenKind.By
        };

        public Lexer(string src) { _src = Preprocessor.Normalize(src); }

        private char Cur => _pos < _src.Length ? _src[_pos] : '\0';
        private char Peek() => _pos + 1 < _src.Length ? _src[_pos + 1] : '\0';
        private void Next() => _pos++;

        public List<Token> Lex()
        {
            var tks = new List<Token>();
            while (true)
            {
                while (char.IsWhiteSpace(Cur)) Next();
                int start = _pos;
                if (Cur == '\0') { tks.Add(new Token(TokenKind.EOF, "", _pos)); break; }

                char c = Cur;
                if (char.IsDigit(c))
                {
                    while (char.IsDigit(Cur)) Next();
                    if (Cur == '.') { Next(); while (char.IsDigit(Cur)) Next(); }
                    tks.Add(new Token(TokenKind.Number, _src[start.._pos], start));
                    continue;
                }
                if (char.IsLetter(c) or c == '_')
                {
                    while (char.IsLetterOrDigit(Cur) or Cur == '_') Next();
                    string ident = _src[start.._pos];
                    if (_keywords.TryGetValue(ident, out var kw)) tks.Add(new Token(kw, ident, start));
                    else tks.Add(new Token(TokenKind.Ident, ident, start));
                    continue;
                }
                switch (c)
                {
                    case '"':
                        Next();
                        var sb = new StringBuilder();
                        while (Cur != '"' and Cur != '\0')
                        {
                            if (Cur == '\\')
                            {
                                Next();
                                char esc = Cur; if (esc == '\0') break;
                                sb.Append(esc switch
                                {
                                    'n' => '\n',
                                    'r' => '\r',
                                    't' => '\t',
                                    '"' => '"',
                                    '\\' => '\\',
                                    _ => esc
                                });
                                Next();
                            }
                            else { sb.Append(Cur); Next(); }
                        }
                        if (Cur == '"') Next();
                        tks.Add(new Token(TokenKind.String, sb.ToString(), start));
                        break;

                    case '(' : Next(); tks.Add(new Token(TokenKind.LParen, "(", start)); break;
                    case ')' : Next(); tks.Add(new Token(TokenKind.RParen, ")", start)); break;
                    case '[' : Next(); tks.Add(new Token(TokenKind.LBracket, "[", start)); break;
                    case ']' : Next(); tks.Add(new Token(TokenKind.RBracket, "]", start)); break;
                    case ',' : Next(); tks.Add(new Token(TokenKind.Comma, ",", start)); break;
                    case '.' : Next(); tks.Add(new Token(TokenKind.Dot, ".", start)); break;
                    case ':' : Next(); tks.Add(new Token(TokenKind.Colon, ":", start)); break;

                    case '+': Next(); tks.Add(new Token(TokenKind.Plus, "+", start)); break;
                    case '-': Next(); tks.Add(new Token(TokenKind.Minus, "-", start)); break;
                    case '*': Next(); tks.Add(new Token(TokenKind.Star, "*", start)); break;
                    case '/': Next(); tks.Add(new Token(TokenKind.Slash, "/", start)); break;
                    case '%': Next(); tks.Add(new Token(TokenKind.Percent, "%", start)); break;
                    case '!':
                        if (Peek() == '=') { Next(); Next(); tks.Add(new Token(TokenKind.BangEqual, "!=", start)); }
                        else { Next(); tks.Add(new Token(TokenKind.Bang, "!", start)); }
                        break;
                    case '&':
                        if (Peek() == '&') { Next(); Next(); tks.Add(new Token(TokenKind.AndAnd, "&&", start)); }
                        else throw new Exception($"Lex error at {start}: Unexpected '&'");
                        break;
                    case '|':
                        if (Peek() == '|' ) { Next(); Next(); tks.Add(new Token(TokenKind.OrOr, "||", start)); }
                        else throw new Exception($"Lex error at {start}: Unexpected '|'");
                        break;
                    case '=':
                        if (Peek() == '=') { Next(); Next(); tks.Add(new Token(TokenKind.EqualEqual, "==", start)); }
                        else throw new Exception($"Lex error at {start}: Single '=' not allowed; use 'be' or 'set ... to'");
                        break;
                    case '<':
                        if (Peek() == '=') { Next(); Next(); tks.Add(new Token(TokenKind.LessEqual, "<=", start)); }
                        else { Next(); tks.Add(new Token(TokenKind.Less, "<", start)); }
                        break;
                    case '>':
                        if (Peek() == '=') { Next(); Next(); tks.Add(new Token(TokenKind.GreaterEqual, ">=", start)); }
                        else { Next(); tks.Add(new Token(TokenKind.Greater, ">", start)); }
                        break;
                    case '#':
                        while (Cur != '\n' and Cur != '\r' and Cur != '\0') Next();
                        break;
                    default:
                        throw new Exception($"Lex error at {start}: Unexpected character '{c}'");
                }
            }
            return tks;
        }
    }

    // ==========================
    // AST NODES
    // ==========================
    abstract class Expr { public int Pos; }
    sealed class NumExpr : Expr { public double Value; public NumExpr(double v, int p){Value=v;Pos=p;} }
    sealed class StrExpr : Expr { public string Value; public StrExpr(string v,int p){Value=v;Pos=p;} }
    sealed class BoolExpr : Expr { public bool Value; public BoolExpr(bool v,int p){Value=v;Pos=p;} }
    sealed class NameExpr : Expr { public string Name; public NameExpr(string n,int p){Name=n;Pos=p;} }
    sealed class UnaryExpr : Expr { public string Op; public Expr Inner; public UnaryExpr(string op,Expr e,int p){Op=op;Inner=e;Pos=p;} }
    sealed class BinaryExpr : Expr { public string Op; public Expr Left,Right; public BinaryExpr(string op,Expr l,Expr r,int p){Op=op;Left=l;Right=r;Pos=p;} }
    sealed class CallExpr : Expr { public string Name; public List<Expr> Args; public CallExpr(string n,List<Expr>a,int p){Name=n;Args=a;Pos=p;} }
    sealed class IndexExpr : Expr { public Expr Target, Index; public IndexExpr(Expr t, Expr i,int p){Target=t;Index=i;Pos=p;} }
    sealed class ListExpr : Expr { public List<Expr> Items; public ListExpr(List<Expr> items,int p){Items=items;Pos=p;} }

    abstract class Stmt { public int Pos; }
    sealed class LetStmt : Stmt { public string Name; public Expr Value; public LetStmt(string n,Expr v,int p){Name=n;Value=v;Pos=p;} }
    sealed class SetStmt : Stmt { public Expr Target; public Expr Value; public SetStmt(Expr t,Expr v,int p){Target=t;Value=v;Pos=p;} }
    sealed class SayStmt : Stmt { public Expr Value; public SayStmt(Expr v,int p){Value=v;Pos=p;} }
    sealed class IfStmt : Stmt { public Expr Cond; public List<Stmt> Then, Else; public IfStmt(Expr c,List<Stmt> t,List<Stmt> e,int p){Cond=c;Then=t;Else=e;Pos=p;} }
    sealed class WhileStmt : Stmt { public Expr Cond; public List<Stmt> Body; public WhileStmt(Expr c,List<Stmt> b,int p){Cond=c;Body=b;Pos=p;} }
    sealed class RepeatStmt : Stmt { public Expr Count; public List<Stmt> Body; public RepeatStmt(Expr n,List<Stmt> b,int p){Count=n;Body=b;Pos=p;} }
    sealed class ForEachStmt : Stmt { public string Var; public Expr Source; public List<Stmt> Body; public ForEachStmt(string v,Expr s,List<Stmt> b,int p){Var=v;Source=s;Body=b;Pos=p;} }
    sealed class AppendStmt : Stmt { public Expr Value; public string ListName; public AppendStmt(Expr v,string n,int p){Value=v;ListName=n;Pos=p;} }
    sealed class RemoveStmt : Stmt { public Expr Value; public string ListName; public RemoveStmt(Expr v,string n,int p){Value=v;ListName=n;Pos=p;} }
    sealed class ReturnStmt : Stmt { public Expr Value; public ReturnStmt(Expr v,int p){Value=v;Pos=p;} }
    sealed class ExprStmt : Stmt { public Expr Expr; public ExprStmt(Expr e,int p){Expr=e;Pos=p;} }

    sealed class FuncDecl
    {
        public string Name;
        public List<string> Params;
        public List<Stmt> Body;
        public int Pos;
        public FuncDecl(string n,List<string> ps,List<Stmt> b,int p){Name=n;Params=ps;Body=b;Pos=p;}
    }

    sealed class ProgramAst
    {
        public readonly List<FuncDecl> Functions = new();
        public readonly List<Stmt> Statements = new();
    }

    // ==========================
    // PARSER
    // ==========================
    class Parser
    {
        private readonly List<Token> _toks; private int _pos;
        Token Cur => _pos < _toks.Count ? _toks[_pos] : _toks[^1];
        Token Peek(int k=1) => _pos + k < _toks.Count ? _toks[_pos + k] : _toks[^1];
        bool Match(TokenKind k){ if (Cur.Kind==k){_pos++; return true;} return false; }
        Token Expect(TokenKind k, string msg){ if (Cur.Kind!=k) throw new Exception($"{msg} at {Cur.Pos} (found {Cur.Kind})"); var t=Cur; _pos++; return t; }

        public Parser(List<Token> toks){ _toks = toks; }

        public ProgramAst ParseProgram()
        {
            var prog = new ProgramAst();
            while (Cur.Kind != TokenKind.EOF)
            {
                if (Cur.Kind == TokenKind.Ident && Peek().Kind == TokenKind.LParen)
                {
                    // expression statement (call) handled below
                }
                if (Cur.Kind == TokenKind.Return)
                {
                    // handled in ParseStatement
                }
                if (Cur.Kind == TokenKind.If or Cur.Kind == TokenKind.While or Cur.Kind == TokenKind.For or Cur.Kind == TokenKind.Repeat or Cur.Kind == TokenKind.Let or Cur.Kind == TokenKind.Set or Cur.Kind == TokenKind.Say or Cur.Kind == TokenKind.Append or Cur.Kind == TokenKind.Remove or Cur.Kind == TokenKind.Increase or Cur.Kind == TokenKind.Decrease or Cur.Kind == TokenKind.Ask || Cur.Kind == TokenKind.Ident || Cur.Kind == TokenKind.To)
                {
                    if (Cur.Kind == TokenKind.To && Peek().Kind == TokenKind.Ident && Peek(2).Kind == TokenKind.LParen)
                        prog.Functions.Add(ParseFunction());
                    else
                        prog.Statements.Add(ParseStatement());
                }
                else
                {
                    // Fallback: try parse statement; if fails, throw
                    prog.Statements.Add(ParseStatement());
                }
            }
            return prog;
        }

        FuncDecl ParseFunction()
        {
            int start = Expect(TokenKind.To, "Expected 'to'").Pos;
            string name = Expect(TokenKind.Ident, "Expected function name").Text;
            Expect(TokenKind.LParen, "Expected '('");
            var parms = new List<string>();
            if (Cur.Kind != TokenKind.RParen)
            {
                while (true)
                {
                    parms.Add(Expect(TokenKind.Ident, "Expected parameter name").Text);
                    if (Match(TokenKind.Comma)) continue;
                    break;
                }
            }
            Expect(TokenKind.RParen, "Expected ')'");
            Expect(TokenKind.Colon, "Expected ':'");
            var body = new List<Stmt>();
            while (Cur.Kind != TokenKind.End && Cur.Kind != TokenKind.EOF)
                body.Add(ParseStatement());
            Expect(TokenKind.End, "Expected 'end'");
            Expect(TokenKind.Dot, "Expected '.'");
            return new FuncDecl(name, parms, body, start);
        }

        Stmt ParseStatement()
        {
            switch (Cur.Kind)
            {
                case TokenKind.Let:
                    {
                        int p = Cur.Pos; _pos++;
                        string name = Expect(TokenKind.Ident, "Expected variable name").Text;
                        Expect(TokenKind.Be, "Expected 'be'");
                        var val = ParseExpression();
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new LetStmt(name, val, p);
                    }
                case TokenKind.Set:
                    {
                        int p = Cur.Pos; _pos++;
                        var target = ParseAssignable();
                        Expect(TokenKind.To, "Expected 'to'");
                        var val = ParseExpression();
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new SetStmt(target, val, p);
                    }
                case TokenKind.Increase:
                    {
                        int p = Cur.Pos; _pos++;
                        var target = ParseAssignable();
                        Expect(TokenKind.By, "Expected 'by'");
                        var inc = ParseExpression();
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new SetStmt(target, new BinaryExpr("+", target, inc, p), p);
                    }
                case TokenKind.Decrease:
                    {
                        int p = Cur.Pos; _pos++;
                        var target = ParseAssignable();
                        Expect(TokenKind.By, "Expected 'by'");
                        var dec = ParseExpression();
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new SetStmt(target, new BinaryExpr("-", target, dec, p), p);
                    }
                case TokenKind.Say:
                    {
                        int p = Cur.Pos; _pos++;
                        var v = ParseExpression();
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new SayStmt(v, p);
                    }
                case TokenKind.Append:
                    {
                        int p = Cur.Pos; _pos++;
                        var val = ParseExpression();
                        Expect(TokenKind.To, "Expected 'to'");
                        string listName = Expect(TokenKind.Ident, "Expected list name").Text;
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new AppendStmt(val, listName, p);
                    }
                case TokenKind.Remove:
                    {
                        int p = Cur.Pos; _pos++;
                        var val = ParseExpression();
                        Expect(TokenKind.From, "Expected 'from'");
                        string listName = Expect(TokenKind.Ident, "Expected list name").Text;
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new RemoveStmt(val, listName, p);
                    }
                case TokenKind.If:
                    {
                        int p = Cur.Pos; _pos++;
                        var cond = ParseExpression();
                        if (Cur.Kind == TokenKind.Comma) _pos++;
                        if (Cur.Kind == TokenKind.Then) _pos++;
                        Expect(TokenKind.Colon, "Expected ':'");
                        var thenBlock = new List<Stmt>();
                        while (Cur.Kind != TokenKind.End && Cur.Kind != TokenKind.Otherwise && Cur.Kind != TokenKind.EOF)
                            thenBlock.Add(ParseStatement());
                        List<Stmt>? elseBlock = null;
                        if (Match(TokenKind.Otherwise))
                        {
                            Expect(TokenKind.Colon, "Expected ':'");
                            elseBlock = new List<Stmt>();
                            while (Cur.Kind != TokenKind.End && Cur.Kind != TokenKind.EOF)
                                elseBlock.Add(ParseStatement());
                        }
                        Expect(TokenKind.End, "Expected 'end'");
                        if (Cur.Kind == TokenKind.If) _pos++;
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new IfStmt(cond, thenBlock, elseBlock, p);
                    }
                case TokenKind.While:
                    {
                        int p = Cur.Pos; _pos++;
                        var cond = ParseExpression();
                        if (Cur.Kind == TokenKind.Comma) _pos++;
                        if (Cur.Kind == TokenKind.Do) _pos++;
                        Expect(TokenKind.Colon, "Expected ':'");
                        var body = new List<Stmt>();
                        while (Cur.Kind != TokenKind.End && Cur.Kind != TokenKind.EOF)
                            body.Add(ParseStatement());
                        Expect(TokenKind.End, "Expected 'end'");
                        if (Cur.Kind == TokenKind.While) _pos++;
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new WhileStmt(cond, body, p);
                    }
                case TokenKind.Repeat:
                    {
                        int p = Cur.Pos; _pos++;
                        var count = ParseExpression();
                        Expect(TokenKind.Times, "Expected 'times'");
                        Expect(TokenKind.Colon, "Expected ':'");
                        var body = new List<Stmt>();
                        while (Cur.Kind != TokenKind.End && Cur.Kind != TokenKind.EOF)
                            body.Add(ParseStatement());
                        Expect(TokenKind.End, "Expected 'end'");
                        if (Cur.Kind == TokenKind.Repeat) _pos++;
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new RepeatStmt(count, body, p);
                    }
                case TokenKind.For:
                    {
                        int p = Cur.Pos; _pos++;
                        Expect(TokenKind.Each, "Expected 'each'");
                        string var = Expect(TokenKind.Ident, "Expected variable").Text;
                        Expect(TokenKind.In, "Expected 'in'");
                        var src = ParseExpression();
                        Expect(TokenKind.Colon, "Expected ':'");
                        var body = new List<Stmt>();
                        while (Cur.Kind != TokenKind.End && Cur.Kind != TokenKind.EOF)
                            body.Add(ParseStatement());
                        Expect(TokenKind.End, "Expected 'end'");
                        if (Cur.Kind == TokenKind.For) _pos++;
                        if (Cur.Kind == TokenKind.Each) _pos++;
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new ForEachStmt(var, src, body, p);
                    }
                case TokenKind.Return:
                    {
                        int p = Cur.Pos; _pos++;
                        var v = ParseExpression();
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new ReturnStmt(v, p);
                    }
                default:
                    {
                        int p = Cur.Pos;
                        var e = ParseExpression();
                        Expect(TokenKind.Dot, "Expected '.'");
                        return new ExprStmt(e, p);
                    }
            }
        }

        Expr ParseAssignable()
        {
            var nameTok = Expect(TokenKind.Ident, "Expected variable");
            Expr baseExpr = new NameExpr(nameTok.Text, nameTok.Pos);
            if (Match(TokenKind.LBracket))
            {
                var idx = ParseExpression();
                Expect(TokenKind.RBracket, "Expected ']'");
                return new IndexExpr(baseExpr, idx, nameTok.Pos);
            }
            return baseExpr;
        }

        Expr ParseExpression(int minPrec = 0)
        {
            var left = ParseUnary();

            while (true)
            {
                var (prec, opText) = GetBinOpInfo(Cur.Kind);
                if (prec < minPrec) break;
                var opTok = Cur; _pos++;
                var right = ParseExpression(prec + 1);
                left = new BinaryExpr(opText, left, right, opTok.Pos);
            }
            return left;
        }

        Expr ParseUnary()
        {
            if (Cur.Kind == TokenKind.Bang || Cur.Kind == TokenKind.Minus || Cur.Kind == TokenKind.Plus)
            {
                var op = Cur.Text; int p = Cur.Pos; _pos++;
                var inner = ParseUnary();
                return new UnaryExpr(op, inner, p);
            }
            return ParsePostfix();
        }

        Expr ParsePostfix()
        {
            var expr = ParsePrimary();
            while (true)
            {
                if (Match(TokenKind.LParen))
                {
                    var args = new List<Expr>();
                    if (Cur.Kind != TokenKind.RParen)
                    {
                        while (true)
                        {
                            args.Add(ParseExpression());
                            if (Match(TokenKind.Comma)) continue;
                            break;
                        }
                    }
                    Expect(TokenKind.RParen, "Expected ')'");
                    if (expr is NameExpr ne)
                        expr = new CallExpr(ne.Name, args, ne.Pos);
                    else
                        throw new Exception($"Only simple names can be called, near {Cur.Pos}");
                }
                else if (Match(TokenKind.LBracket))
                {
                    var idx = ParseExpression();
                    Expect(TokenKind.RBracket, "Expected ']'");
                    expr = new IndexExpr(expr, idx, expr.Pos);
                }
                else break;
            }
            return expr;
        }

        Expr ParsePrimary()
        {
            var t = Cur;
            switch (t.Kind)
            {
                case TokenKind.Number: _pos++; return new NumExpr(double.Parse(t.Text, CultureInfo.InvariantCulture), t.Pos);
                case TokenKind.String: _pos++; return new StrExpr(t.Text, t.Pos);
                case TokenKind.True: _pos++; return new BoolExpr(true, t.Pos);
                case TokenKind.False: _pos++; return new BoolExpr(false, t.Pos);
                case TokenKind.Ident: _pos++; return new NameExpr(t.Text, t.Pos);
                case TokenKind.LParen:
                    _pos++; var e = ParseExpression(); Expect(TokenKind.RParen, "Expected ')'"); return e;
                case TokenKind.LBracket:
                    {
                        _pos++;
                        var items = new List<Expr>();
                        if (Cur.Kind != TokenKind.RBracket)
                        {
                            while (true)
                            {
                                items.Add(ParseExpression());
                                if (Match(TokenKind.Comma)) continue;
                                break;
                            }
                        }
                        Expect(TokenKind.RBracket, "Expected ']'");
                        return new ListExpr(items, t.Pos);
                    }
                default:
                    throw new Exception($"Unexpected token {t.Kind} at {t.Pos}");
            }
        }

        (int prec, string op) GetBinOpInfo(TokenKind k) => k switch
        {
            TokenKind.Star or TokenKind.Slash or TokenKind.Percent => (6, Cur.Text),
            TokenKind.Plus or TokenKind.Minus => (5, Cur.Text),
            TokenKind.Greater or TokenKind.GreaterEqual or TokenKind.Less or TokenKind.LessEqual => (4, Cur.Text),
            TokenKind.EqualEqual or TokenKind.BangEqual => (3, Cur.Text),
            TokenKind.AndAnd => (2, Cur.Text),
            TokenKind.OrOr => (1, Cur.Text),
            _ => (-1, "")
        };
    }

    // ==========================
    // CODEGEN
    // ==========================
    class CSharpGenerator
    {
        private readonly ProgramAst _ast;
        public CSharpGenerator(ProgramAst ast){ _ast=ast; }

        public string Generate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System; using System.Collections.Generic; using System.Globalization; using System.Linq; using System.Text; using System.IO;");
            sb.AppendLine("namespace ReoGenerated {");
            sb.AppendLine(ReoRuntimeSource());
            sb.AppendLine("public static class ReoProgram {");
            foreach (var f in _ast.Functions)
                EmitFunction(sb, f);

            sb.AppendLine("  public static ReoValue __main(){");
            sb.AppendLine("    var __scope = new Dictionary<string, ReoValue>(StringComparer.OrdinalIgnoreCase);");
            foreach (var st in _ast.Statements)
                EmitStmt(sb, st, "__scope", inFunction:false);
            sb.AppendLine("    return ReoValue.Nothing();");
            sb.AppendLine("  }");
            sb.AppendLine("  public static void Main(){ Console.OutputEncoding = Encoding.UTF8; __main(); }");
            sb.AppendLine("} }");
            return sb.ToString();
        }

        void EmitFunction(StringBuilder sb, FuncDecl f)
        {
            sb.Append("  public static ReoValue ").Append(f.Name).Append("(");
            for (int i=0;i<f.Params.Count;i++)
            {
                if (i>0) sb.Append(", ");
                sb.Append("ReoValue ").Append(f.Params[i]);
            }
            sb.AppendLine("){");
            sb.AppendLine("    var __scope = new Dictionary<string, ReoValue>(StringComparer.OrdinalIgnoreCase);");
            foreach (var p in f.Params) sb.Append("    __scope[\"").Append(p).AppendLine("\"] = "+p+";");
            foreach (var st in f.Body)
                EmitStmt(sb, st, "__scope", inFunction:true);
            sb.AppendLine("    return ReoValue.Nothing();");
            sb.AppendLine("  }");
        }

        void EmitStmt(StringBuilder sb, Stmt st, string scopeVar, bool inFunction)
        {
            switch (st)
            {
                case LetStmt s:
                    sb.Append("    ").Append(scopeVar).Append("[\"").Append(s.Name).Append("\"] = ").Append(EmitExpr(s.Value, scopeVar)).AppendLine(";");
                    break;
                case SetStmt s:
                    if (s.Target is NameExpr ne)
                    {
                        sb.Append("    ").Append(scopeVar).Append("[\"").Append(ne.Name).Append("\"] = ").Append(EmitExpr(s.Value, scopeVar)).AppendLine(";");
                    }
                    else if (s.Target is IndexExpr ie && ie.Target is NameExpr ne2)
                    {
                        sb.Append("    ReoRuntime.ListSet(")
                          .Append(scopeVar).Append("[\"").Append(ne2.Name).Append("\"], ")
                          .Append("ReoRuntime.ToInt(").Append(EmitExpr(ie.Index, scopeVar)).Append("), ")
                          .Append(EmitExpr(s.Value, scopeVar)).AppendLine(");");
                    }
                    else
                    {
                        throw new Exception("Assignment target must be a variable or list index.");
                    }
                    break;
                case SayStmt s:
                    sb.Append("    ReoRuntime.Say(").Append(EmitExpr(s.Value, scopeVar)).AppendLine(");");
                    break;
                case AppendStmt s:
                    sb.Append("    ReoRuntime.Append(").Append(scopeVar).Append("[\"").Append(s.ListName).Append("\"], ").Append(EmitExpr(s.Value, scopeVar)).AppendLine(");");
                    break;
                case RemoveStmt s:
                    sb.Append("    ReoRuntime.Remove(").Append(scopeVar).Append("[\"").Append(s.ListName).Append("\"], ").Append(EmitExpr(s.Value, scopeVar)).AppendLine(");");
                    break;
                case ReturnStmt s:
                    if (!inFunction) throw new Exception("Return only allowed inside functions.");
                    sb.Append("    return ").Append(EmitExpr(s.Value, scopeVar)).AppendLine(";");
                    break;
                case IfStmt s:
                    sb.Append("    if (ReoRuntime.ToBool(").Append(EmitExpr(s.Cond, scopeVar)).AppendLine(")) {");
                    foreach (var st2 in s.Then) EmitStmt(sb, st2, scopeVar, inFunction);
                    sb.AppendLine("    } else {");
                    if (s.Else != null) foreach (var st3 in s.Else) EmitStmt(sb, st3, scopeVar, inFunction);
                    sb.AppendLine("    }");
                    break;
                case WhileStmt s:
                    sb.Append("    while (ReoRuntime.ToBool(").Append(EmitExpr(s.Cond, scopeVar)).AppendLine(")) {");
                    foreach (var b in s.Body) EmitStmt(sb, b, scopeVar, inFunction);
                    sb.AppendLine("    }");
                    break;
                case RepeatStmt s:
                    sb.Append("    for (int __i=0, __n=ReoRuntime.ToInt(").Append(EmitExpr(s.Count, scopeVar)).AppendLine("); __i<__n; __i++){");
                    foreach (var b in s.Body) EmitStmt(sb, b, scopeVar, inFunction);
                    sb.AppendLine("    }");
                    break;
                case ForEachStmt s:
                    sb.Append("    foreach (var __item in ReoRuntime.Iter(").Append(EmitExpr(s.Source, scopeVar)).AppendLine(")) {");
                    sb.Append("      ").Append(scopeVar).Append("[\"").Append(s.Var).Append("\"] = __item;").AppendLine();
                    foreach (var b in s.Body) EmitStmt(sb, b, scopeVar, inFunction);
                    sb.AppendLine("    }");
                    break;
                case ExprStmt s:
                    sb.Append("    _ = ").Append(EmitExpr(s.Expr, scopeVar)).AppendLine(";");
                    break;
                default:
                    throw new NotSupportedException(st.GetType().Name);
            }
        }

        string EmitExpr(Expr e, string scopeVar)
        {
            return e switch
            {
                NumExpr n   => $"ReoValue.Number({n.Value.ToString(CultureInfo.InvariantCulture)})",
                StrExpr s   => $"ReoValue.Text({Literal(s.Value)})",
                BoolExpr b  => b.Value ? "ReoValue.True()" : "ReoValue.False()",
                NameExpr v  => $"{scopeVar}.TryGetValue(\"{v.Name}\", out var __v_{v.Name}) ? __v_{v.Name} : ReoRuntime.Undefined(\"{v.Name}\")",
                ListExpr l  => "ReoValue.List(new List<ReoValue>{" + string.Join(",", l.Items.Select(it => EmitExpr(it, scopeVar))) + "})",
                UnaryExpr u => u.Op switch
                {
                    "!" => $"ReoRuntime.Not({EmitExpr(u.Inner, scopeVar)})",
                    "-" => $"ReoRuntime.Neg({EmitExpr(u.Inner, scopeVar)})",
                    "+" => $"{EmitExpr(u.Inner, scopeVar)}",
                    _ => throw new Exception("Unknown unary op")
                },
                IndexExpr ix => $"ReoRuntime.ListGet({EmitExpr(ix.Target, scopeVar)}, ReoRuntime.ToInt({EmitExpr(ix.Index, scopeVar)}))",
                CallExpr c   => EmitCall(c, scopeVar),
                BinaryExpr b => b.Op switch
                {
                    "+"  => $"ReoRuntime.Add({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    "-"  => $"ReoRuntime.Sub({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    "*"  => $"ReoRuntime.Mul({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    "/"  => $"ReoRuntime.Div({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    "%"  => $"ReoRuntime.Mod({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    "==" => $"ReoRuntime.Eq({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    "!=" => $"ReoRuntime.Neq({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    ">"  => $"ReoRuntime.Gt({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    ">=" => $"ReoRuntime.Gte({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    "<"  => $"ReoRuntime.Lt({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    "<=" => $"ReoRuntime.Lte({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    "&&" => $"ReoRuntime.And({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    "||" => $"ReoRuntime.Or({EmitExpr(b.Left, scopeVar)}, {EmitExpr(b.Right, scopeVar)})",
                    _ => throw new Exception("Unknown binary op: " + b.Op)
                },
                _ => throw new NotSupportedException(e.GetType().Name)
            };
        }

        string EmitCall(CallExpr c, string scopeVar)
        {
            switch (c.Name.ToLowerInvariant())
            {
                case "length":
                    return $"ReoRuntime.Length({EmitExpr(c.Args[0], scopeVar)})";
                case "ask":
                    return $"ReoRuntime.Ask({EmitExpr(c.Args[0], scopeVar)})";
                case "range":
                    if (c.Args.Count==2) return $"ReoRuntime.Range({EmitExpr(c.Args[0], scopeVar)}, {EmitExpr(c.Args[1], scopeVar)})";
                    throw new Exception("range(start,end) expected.");
                case "to_number":
                    return $"ReoRuntime.ToNumberVal({EmitExpr(c.Args[0], scopeVar)})";
                case "to_text":
                    return $"ReoRuntime.ToTextVal({EmitExpr(c.Args[0], scopeVar)})";
                case "to_truth":
                    return $"ReoRuntime.ToTruthVal({EmitExpr(c.Args[0], scopeVar)})";
                case "read_text":
                    return $"ReoRuntime.ReadText({EmitExpr(c.Args[0], scopeVar)})";
                case "write_text":
                    return $"ReoRuntime.WriteText({EmitExpr(c.Args[0], scopeVar)}, {EmitExpr(c.Args[1], scopeVar)})";
                case "now":
                    return $"ReoRuntime.Now()";
                case "format_now":
                    return $"ReoRuntime.FormatNow({EmitExpr(c.Args[0], scopeVar)})";
                default:
                    return $"{c.Name}({string.Join(", ", c.Args.Select(a => EmitExpr(a, scopeVar)))})";
            }
        }

        static string Literal(string s) => "@\"" + s.Replace("\"", "\"\"") + "\"";

        string ReoRuntimeSource()
        {
            return @"
public enum ReoType { Number, Text, Truth, List, Nothing }

public readonly struct ReoValue
{
    public readonly ReoType Type;
    public readonly double Num;
    public readonly string TextVal;
    public readonly bool Bool;
    public readonly System.Collections.Generic.List<ReoValue> ListVal;

    private ReoValue(ReoType t, double n, string s, bool b, System.Collections.Generic.List<ReoValue> l){ Type=t; Num=n; TextVal=s; Bool=b; ListVal=l; }

    public static ReoValue Number(double v) => new ReoValue(ReoType.Number, v, null, false, null);
    public static ReoValue Text(string v) => new ReoValue(ReoType.Text, 0, v ?? string.Empty, false, null);
    public static ReoValue True() => new ReoValue(ReoType.Truth, 0, null, true, null);
    public static ReoValue False() => new ReoValue(ReoType.Truth, 0, null, false, null);
    public static ReoValue Truth(bool v) => v ? True() : False();
    public static ReoValue List(System.Collections.Generic.List<ReoValue> v) => new ReoValue(ReoType.List, 0, null, false, v ?? new System.Collections.Generic.List<ReoValue>());
    public static ReoValue Nothing() => new ReoValue(ReoType.Nothing, 0, null, false, null);

    public override string ToString() => ReoRuntime.Display(this);
}

public static class ReoRuntime
{
    public static string Display(ReoValue v) => v.Type switch
    {
        ReoType.Number => v.Num.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ReoType.Text => v.TextVal ?? string.Empty,
        ReoType.Truth => v.Bool ? ""true"" : ""false"",
        ReoType.List => ""["" + string.Join("", "", v.ListVal.Select(Display)) + ""]"",
        _ => ""nothing""
    };

    public static ReoValue Undefined(string name) => throw new System.Exception($""Undefined variable: {name}"");

    public static double ToNumber(ReoValue v) => v.Type switch
    {
        ReoType.Number => v.Num,
        ReoType.Text => double.TryParse(v.TextVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0,
        ReoType.Truth => v.Bool ? 1 : 0,
        ReoType.List => v.ListVal?.Count ?? 0,
        _ => 0
    };
    public static int ToInt(ReoValue v) => (int)System.Math.Floor(ToNumber(v));
    public static bool ToBool(ReoValue v) => v.Type switch
    {
        ReoType.Truth => v.Bool,
        ReoType.Number => v.Num != 0,
        ReoType.Text => !string.IsNullOrEmpty(v.TextVal),
        ReoType.List => v.ListVal != null && v.ListVal.Count > 0,
        _ => false
    };
    public static string ToText(ReoValue v) => Display(v);

    public static ReoValue ToNumberVal(ReoValue v) => ReoValue.Number(ToNumber(v));
    public static ReoValue ToTextVal(ReoValue v) => ReoValue.Text(ToText(v));
    public static ReoValue ToTruthVal(ReoValue v) => ReoValue.Truth(ToBool(v));

    // arithmetic
    public static ReoValue Add(ReoValue a, ReoValue b)
    {
        if (a.Type == ReoType.Text || b.Type == ReoType.Text) return ReoValue.Text(ToText(a) + ToText(b));
        return ReoValue.Number(ToNumber(a) + ToNumber(b));
    }
    public static ReoValue Sub(ReoValue a, ReoValue b) => ReoValue.Number(ToNumber(a) - ToNumber(b));
    public static ReoValue Mul(ReoValue a, ReoValue b) => ReoValue.Number(ToNumber(a) * ToNumber(b));
    public static ReoValue Div(ReoValue a, ReoValue b) => ReoValue.Number(ToNumber(a) / ToNumber(b));
    public static ReoValue Mod(ReoValue a, ReoValue b) => ReoValue.Number(ToInt(a) % ToInt(b));
    public static ReoValue Neg(ReoValue v) => ReoValue.Number(-ToNumber(v));
    public static ReoValue Not(ReoValue v) => ReoValue.Truth(!ToBool(v));

    // comparisons
    public static ReoValue Eq(ReoValue a, ReoValue b)
    {
        if (a.Type == ReoType.Text || b.Type == ReoType.Text) return ReoValue.Truth(ToText(a) == ToText(b));
        if (a.Type == ReoType.Truth || b.Type == ReoType.Truth) return ReoValue.Truth(ToBool(a) == ToBool(b));
        return ReoValue.Truth(System.Math.Abs(ToNumber(a) - ToNumber(b)) < 1e-12);
    }
    public static ReoValue Neq(ReoValue a, ReoValue b) => ReoValue.Truth(!ToBool(Eq(a,b)));
    public static ReoValue Gt(ReoValue a, ReoValue b)
    {
        if (a.Type == ReoType.Text || b.Type == ReoType.Text) return ReoValue.Truth(string.CompareOrdinal(ToText(a), ToText(b)) > 0);
        return ReoValue.Truth(ToNumber(a) > ToNumber(b));
    }
    public static ReoValue Gte(ReoValue a, ReoValue b)
    {
        if (a.Type == ReoType.Text || b.Type == ReoType.Text) return ReoValue.Truth(string.CompareOrdinal(ToText(a), ToText(b)) >= 0);
        return ReoValue.Truth(ToNumber(a) >= ToNumber(b));
    }
    public static ReoValue Lt(ReoValue a, ReoValue b)
    {
        if (a.Type == ReoType.Text || b.Type == ReoType.Text) return ReoValue.Truth(string.CompareOrdinal(ToText(a), ToText(b)) < 0);
        return ReoValue.Truth(ToNumber(a) < ToNumber(b));
    }
    public static ReoValue Lte(ReoValue a, ReoValue b)
    {
        if (a.Type == ReoType.Text || b.Type == ReoType.Text) return ReoValue.Truth(string.CompareOrdinal(ToText(a), ToText(b)) <= 0);
        return ReoValue.Truth(ToNumber(a) <= ToNumber(b));
    }
    public static ReoValue And(ReoValue a, ReoValue b) => ReoValue.Truth(ToBool(a) && ToBool(b));
    public static ReoValue Or(ReoValue a, ReoValue b) => ReoValue.Truth(ToBool(a) || ToBool(b));

    // printing & input
    public static void Say(ReoValue v) => System.Console.WriteLine(Display(v));
    public static ReoValue Ask(ReoValue prompt) { System.Console.Write(ToText(prompt)); return ReoValue.Text(System.Console.ReadLine() ?? """"); }

    // lists
    public static ReoValue Length(ReoValue v) => v.Type == ReoType.Text ? ReoValue.Number((v.TextVal ?? """").Length)
                                            : v.Type == ReoType.List ? ReoValue.Number(v.ListVal.Count)
                                            : ReoValue.Number(ToNumber(v));
    public static ReoValue ListGet(ReoValue list, int index)
    {
        if (list.Type != ReoType.List) throw new System.Exception(""Indexing requires a list."");
        if (index < 0 || index >= list.ListVal.Count) throw new System.Exception($""Index out of range: {index}"");
        return list.ListVal[index];
    }
    public static void ListSet(ReoValue list, int index, ReoValue value)
    {
        if (list.Type != ReoType.List) throw new System.Exception(""Indexing requires a list."");
        if (index < 0 || index >= list.ListVal.Count) throw new System.Exception($""Index out of range: {index}"");
        list.ListVal[index] = value;
    }
    public static void Append(ReoValue list, ReoValue value)
    {
        if (list.Type != ReoType.List) throw new System.Exception(""append ... to requires a list variable."");
        list.ListVal.Add(value);
    }
    public static void Remove(ReoValue list, ReoValue value)
    {
        if (list.Type != ReoType.List) throw new System.Exception(""remove ... from requires a list variable."");
        list.ListVal.RemoveAll(v => ToText(v) == ToText(value));
    }
    public static System.Collections.Generic.IEnumerable<ReoValue> Iter(ReoValue v)
    {
        if (v.Type == ReoType.List) return v.ListVal;
        throw new System.Exception(""for each expects a list."");
    }

    public static ReoValue Range(ReoValue start, ReoValue end)
    {
        int a = ToInt(start), b = ToInt(end);
        var list = new System.Collections.Generic.List<ReoValue>();
        if (a <= b) for (int i=a;i<=b;i++) list.Add(ReoValue.Number(i));
        else for (int i=a;i>=b;i--) list.Add(ReoValue.Number(i));
        return ReoValue.List(list);
    }

    // simple file I/O
    public static ReoValue ReadText(ReoValue path)
    {
        var p = ToText(path);
        return ReoValue.Text(System.IO.File.ReadAllText(p));
    }
    public static ReoValue WriteText(ReoValue path, ReoValue content)
    {
        var p = ToText(path);
        System.IO.File.WriteAllText(p, ToText(content));
        return ReoValue.Text(p);
    }

    // time
    public static ReoValue Now() => ReoValue.Text(System.DateTime.Now.ToString(""o""));
    public static ReoValue FormatNow(ReoValue format) => ReoValue.Text(System.DateTime.Now.ToString(ToText(format)));
}
";
        }
    }

    // ==========================
    // EMITTER (Roslyn)
    // ==========================
    static class Emitter
    {
        public static bool CompileToExe(string csharpSource, string outputPath, out IEnumerable<Diagnostic> diagnostics)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(csharpSource, new CSharpParseOptions(LanguageVersion.Preview));
            string runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();

            var refs = Directory.EnumerateFiles(runtimeDir, "*.dll")
                                .Where(f => !Path.GetFileName(f).StartsWith("api-ms", StringComparison.OrdinalIgnoreCase))
                                .Select(f => MetadataReference.CreateFromFile(f))
                                .ToList();

            var compilation = CSharpCompilation.Create(
                Path.GetFileNameWithoutExtension(outputPath),
                new[] { syntaxTree },
                refs,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: OptimizationLevel.Release));

            var result = compilation.Emit(outputPath);
            diagnostics = result.Diagnostics;
            return result.Success;
        }
    }

    // ==========================
    // PUBLIC API
    // ==========================
    public static class ReoCompiler
    {
        public static (bool ok, string generatedCSharp, string diagnostics) CompileToExe(string reoSource, string exePath)
        {
            try
            {
                var lexer = new Lexer(reoSource);
                var tokens = lexer.Lex();
                var parser = new Parser(tokens);
                var ast = parser.ParseProgram();
                var gen = new CSharpGenerator(ast);
                string csharp = gen.Generate();
                var ok = Emitter.CompileToExe(csharp, exePath, out var diags);
                var d = string.Join(Environment.NewLine, diags.Select(x => x.ToString()));
                return (ok, csharp, d);
            }
            catch (Exception ex)
            {
                return (false, "", "Compiler error: " + ex.Message);
            }
        }
    }
}
