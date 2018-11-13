﻿using System;
using System.Text;
using csly.whileLang.model;
using csly.whileLang.parser;
using sly.parser;
using sly.parser.generator;
using Sigil;

namespace csly.whileLang.compiler
{
    public class WhileCompiler
    {
        private readonly Parser<WhileToken, WhileAST> whileParser;


        public WhileCompiler()
        {
            var parser = new WhileParser();
            var builder = new ParserBuilder<WhileToken, WhileAST>();
            var whileParserBuildResult = builder.BuildParser(parser, ParserType.EBNF_LL_RECURSIVE_DESCENT, "statement");
            whileParser = whileParserBuildResult.Result;
        }


        private string GetNameSpace(string id)
        {
            return $"NS{id.Replace("-", "")}";
        }

        private string GetClassName(string id)
        {
            return $"Class{id.Replace("-", "")}";
        }


        private string GetCSharpCode(string code, string id)
        {
            var classCode = new StringBuilder();
            classCode.AppendLine("using System;");
            classCode.AppendLine("using csly.whileLang.compiler;");
            classCode.AppendLine($"namespace {GetNameSpace(id)} {{");
            classCode.AppendLine($"     public class {GetClassName(id)} : WhileFunction {{");
            classCode.AppendLine("         public void Run() {");
            classCode.AppendLine(code);
            classCode.AppendLine("         }");
            classCode.AppendLine("      }");
            classCode.AppendLine("}");
            return classCode.ToString();
        }


        public string TranspileToCSharp(string whileCode)
        {
            string sharpCode = null;

            try
            {
                var result = whileParser.Parse(whileCode);
                if (result.IsOk)
                {
                    var ast = result.Result;

                    var checker = new SemanticChecker();

                    var context = checker.SemanticCheck(ast);

                    sharpCode = ast.Transpile(context);
                    sharpCode = GetCSharpCode(sharpCode, Guid.NewGuid().ToString());
                }
            }
            catch
            {
                sharpCode = null;
            }


            return sharpCode;
        }


        public Func<int> CompileToFunction(string whileCode)
        {
            Func<int> function = null;

            try
            {
                var result = whileParser.Parse(whileCode);
                if (result.IsOk)
                {
                    var ast = result.Result;

                    var checker = new SemanticChecker();

                    var context = checker.SemanticCheck(ast);

                    var emiter = Emit<Func<int>>.NewDynamicMethod("Method" + Guid.NewGuid());

                    emiter = ast.EmitByteCode(context, emiter);
                    //emiter.LoadConstant(42);                    
                    //emiter.Return();
                    function = emiter.CreateDelegate();
                    object res = function.Invoke();
                    ;
                }
            }
            catch
            {
                function = null;
            }


            return function;
        }
    }
}