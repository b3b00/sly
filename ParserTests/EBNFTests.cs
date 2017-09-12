﻿using System.Collections.Generic;
using System.Linq;
using jsonparser;
using NUnit.Framework;
using sly.lexer;
using sly.parser;
using sly.parser.generator;
using sly.parser.llparser;
using sly.parser.syntax;

namespace ParserTests
{
    [TestFixture]
    public class EBNFTests
    {

        public enum TokenType
        {
            a = 1,
            b = 2,
            c = 3,
            e = 4,
            f = 5,
            WS = 100,
            EOL = 101
        }


        [LexerConfiguration]
        public ILexer<TokenType> BuildLexer(ILexer<TokenType> lexer)
        {
            lexer.AddDefinition(new TokenDefinition<TokenType>(TokenType.a, "a"));
            lexer.AddDefinition(new TokenDefinition<TokenType>(TokenType.b, "b"));
            lexer.AddDefinition(new TokenDefinition<TokenType>(TokenType.c, "c"));
            lexer.AddDefinition(new TokenDefinition<TokenType>(TokenType.e, "e"));
            lexer.AddDefinition(new TokenDefinition<TokenType>(TokenType.f, "f"));

            lexer.AddDefinition(new TokenDefinition<TokenType>(TokenType.WS, "[ \\t]+", true));
            lexer.AddDefinition(new TokenDefinition<TokenType>(TokenType.EOL, "[\\n\\r]+", true, true));
            return lexer;
        }


        [Production("R : A B c ")]
        public object R(string A, string B, Token<TokenType> c)
        {
            string result = "R(";
            result += A + ",";
            result += B + ",";
            result += c.Value;
            result += ")";
            return result;
        }

        [Production("R : G+ ")]
        public object RManyNT(List<object> gs)
        {
            string result = "R(";
            result += gs
                    .Select(g => g.ToString())
                    .Aggregate((string s1, string s2) => s1 + "," + s2);
            result += ")";
            return result;
        }

        [Production("G : e f ")]
        public object RManyNT(Token<TokenType> e, Token<TokenType> f)
        {
            string result = $"G({e.Value},{f.Value})";
            return result;
        }

        [Production("A : a + ")]
        public object A(List<Token<TokenType>> astr)
        {
            string result = "A(";
            result += (string)astr
                .Select(a => a.Value)
                .Aggregate<string>((a1, a2) => a1 + ", " + a2);
            result += ")";
            return result;
        }

        [Production("B : b * ")]
        public object B(List<Token<TokenType>> bstr)
        {
            if (bstr.Any())
            {
                string result = "B(";
                result += bstr
                    .Select(b => b.Value)
                    .Aggregate<string>((b1, b2) => b1 + ", " + b2);
                result += ")";
                return result;
            }
            return "B()";
        }

        private Parser<TokenType> Parser;

        private Parser<JsonToken> JsonParser;

        private Parser<TokenType> BuildParser()
        {
            EBNFTests parserInstance = new EBNFTests();
            ParserBuilder builder = new ParserBuilder();

            Parser = builder.BuildParser<TokenType>(parserInstance, ParserType.EBNF_LL_RECURSIVE_DESCENT, "R");
            return Parser;
        }


        private Parser<JsonToken> BuildEbnfJsonParser()
        {
            EbnfJsonParser parserInstance = new EbnfJsonParser();
            ParserBuilder builder = new ParserBuilder();

            JsonParser = builder.BuildParser<JsonToken>(parserInstance, ParserType.EBNF_LL_RECURSIVE_DESCENT, "root");
            return JsonParser;
        }

        [Test]
        public void TestParseBuild()
        {
            Parser = BuildParser();
            Assert.AreEqual(typeof(EBNFRecursiveDescentSyntaxParser<TokenType>), Parser.SyntaxParser.GetType());
            Assert.AreEqual(Parser.Configuration.NonTerminals.Count, 4);
            NonTerminal<TokenType> nt = Parser.Configuration.NonTerminals["R"];
            Assert.AreEqual(nt.Rules.Count, 2);
            nt = Parser.Configuration.NonTerminals["A"];
            Assert.AreEqual(nt.Rules.Count, 1);
            Rule<TokenType> rule = nt.Rules[0];
            Assert.AreEqual(rule.Clauses.Count, 1);
            Assert.IsInstanceOf<OneOrMoreClause<TokenType>>(rule.Clauses[0]);
            nt = Parser.Configuration.NonTerminals["B"];
            Assert.AreEqual(nt.Rules.Count, 1);
            rule = nt.Rules[0];
            Assert.AreEqual(rule.Clauses.Count, 1);
            Assert.IsInstanceOf<ZeroOrMoreClause<TokenType>>(rule.Clauses[0]);
        }

        [Test]
        public void TestOneOrMoreNonTerminal()
        {
            Parser = BuildParser();
            ParseResult<TokenType> result = Parser.Parse("e f e f");
            Assert.False(result.IsError);
            Assert.IsInstanceOf<string>(result.Result);
            Assert.AreEqual("R(G(e,f),G(e,f))", result.Result.ToString().Replace(" ", ""));
        }

        [Test]
        public void TestOneOrMoreWithMany()
        {
            Parser = BuildParser();
            ParseResult<TokenType> result = Parser.Parse("aaa b c");
            Assert.False(result.IsError);
            Assert.IsInstanceOf<string>(result.Result);
            Assert.AreEqual("R(A(a,a,a),B(b),c)", result.Result.ToString().Replace(" ", ""));
        }

        [Test]
        public void TestOneOrMoreWithOne()
        {
            Parser = BuildParser();
            ParseResult<TokenType> result = Parser.Parse(" b c");
            Assert.True(result.IsError);
        }

        [Test]
        public void TestZeroOrMoreWithOne()
        {
            Parser = BuildParser();
            ParseResult<TokenType> result = Parser.Parse("a b c");
            Assert.False(result.IsError);
            Assert.IsInstanceOf<string>(result.Result);
            Assert.AreEqual("R(A(a),B(b),c)", result.Result.ToString().Replace(" ", ""));
        }

        [Test]
        public void TestZeroOrMoreWithMany()
        {
            Parser = BuildParser();
            ParseResult<TokenType> result = Parser.Parse("a bb c");
            Assert.False(result.IsError);
            Assert.IsInstanceOf<string>(result.Result);
            Assert.AreEqual("R(A(a),B(b,b),c)", result.Result.ToString().Replace(" ", ""));
        }

        [Test]
        public void TestZeroOrMoreWithNone()
        {
            Parser = BuildParser();
            ParseResult<TokenType> result = Parser.Parse("a  c");
            Assert.False(result.IsError);
            Assert.IsInstanceOf<string>(result.Result);
            Assert.AreEqual("R(A(a),B(),c)", result.Result.ToString().Replace(" ", ""));
        }


        [Test]
        public void TestJsonList()
        {
            Parser<JsonToken> jsonParser = BuildEbnfJsonParser();
            ParseResult<JsonToken> result = jsonParser.Parse("[1,2,3,4]");
            Assert.False(result.IsError);
            Assert.IsAssignableFrom(typeof(List<object>), result.Result);
            Assert.AreEqual(4, ((List<object>)result.Result).Count);
            List<object> lsto = (List<object>)result.Result;
            Assert.AreEqual(new List<object> { 1, 2, 3, 4 }, lsto);
        }

        [Test]
        public void TestJsonObject()
        {
            Parser<JsonToken> jsonParser = BuildEbnfJsonParser();
            ParseResult<JsonToken> result = jsonParser.Parse("{\"one\":1,\"two\":2,\"three\":\"trois\" }");
            Assert.False(result.IsError);
            Assert.IsAssignableFrom(typeof(Dictionary<string, object>), result.Result);
            Assert.AreEqual(3, ((Dictionary<string, object>)result.Result).Count);
            Dictionary<string, object> dico = (Dictionary<string, object>)result.Result;
            Assert.AreEqual(1, dico["one"]);
            Assert.AreEqual(2, dico["two"]);
            Assert.AreEqual("trois", dico["three"]);
        }




    }
}
