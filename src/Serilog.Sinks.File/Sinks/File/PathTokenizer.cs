using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serilog.Sinks.File
{
    internal class PathTokenizer
    {
        public class Token
        {
            public enum TokenType { Text, DirectorySeparator, Parameter, Extension }

            public TokenType Type { get; set; }

            public string Value { get; set; } = string.Empty;

            public string? Argument { get; set; }

            public override string ToString()
            {
                switch (Type)
                {
                    default:
                    case TokenType.Text:
                        return Value.Replace("{", "{{").Replace("}", "}}");
                    case TokenType.DirectorySeparator:
                        return Path.DirectorySeparatorChar.ToString();
                    case TokenType.Parameter:
                        if (string.IsNullOrEmpty(Argument))
                            return string.Format("{{{0}}}", Value.Replace("{", "{{").Replace("}", "}}"));
                        else
                            return string.Format("{{{0}:{1}}}", Value.Replace("{", "{{").Replace("}", "}}"), Argument?.Replace("{", "{{").Replace("}", "}}"));
                    case TokenType.Extension:
                        return "." + Value.Replace("{", "{{").Replace("}", "}}");
                }
            }
        }

        public static List<Token> Tokenize(string str)
        {
            var res = new List<Token>();
            var type = Token.TokenType.Text;
            var current = new StringBuilder();
            var inception = 0;

            foreach (var c in str)
            {
                if (c == '{')
                {
                    if (type != Token.TokenType.Parameter)
                    {
                        if (current.Length > 0)
                        {
                            res.Add(new Token
                            {
                                Type = type,
                                Value = current.ToString(),
                            });
                            current.Length = 0;
                        }

                        type = Token.TokenType.Parameter;
                        continue;
                    }
                    else
                        inception++;
                }
                else if (c == '}' && type == Token.TokenType.Parameter)
                {
                    if (inception > 0)
                        inception--;
                    else
                    {
                        if (current.Length > 0)
                        {
                            string? a = null;
                            var v = current.ToString();
                            var i = v.IndexOf(':');
                            if (i > 0)
                            {
                                a = v.Substring(i + 1);
                                v = v.Substring(0, i);
                            }
                            res.Add(new Token
                            {
                                Type = type,
                                Value = v,
                                Argument = a,
                            });
                            current.Length = 0;
                        }

                        type = Token.TokenType.Text;

                        continue;
                    }
                }
                else if (c == Path.DirectorySeparatorChar && type == Token.TokenType.Text)
                {
                    if (current.Length > 0)
                    {
                        res.Add(new Token
                        {
                            Type = type,
                            Value = current.ToString(),
                        });

                        current.Length = 0;
                    }

                    res.Add(new Token
                    {
                        Type = Token.TokenType.DirectorySeparator,
                    });

                    type = Token.TokenType.Text;

                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
            {
                res.Add(new Token
                {
                    Type = type,
                    Value = current.ToString(),
                });

                current.Length = 0;
            }

            var l = res.Last();
            if (l != null && l.Type == Token.TokenType.Text)
            {
                var i = l.Value.LastIndexOf(".");
                if (i >= 0)
                {
                    var e = l.Value.Substring(i + 1);
                    if (i > 0)
                        l.Value = l.Value.Substring(0, i);
                    else
                        res.Remove(l);

                    res.Add(new Token
                    {
                        Type = Token.TokenType.Extension,
                        Value = e,
                    });
                }
            }

            return res;
        }
    }
}
