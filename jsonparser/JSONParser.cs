﻿using sly.lexer;
using System.Linq;
using sly.parser.generator;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;


namespace jsonparser
{


    public static class DictionaryExtensionMethods
    {
        public static void Merge<TKey, TValue>(this Dictionary<TKey, TValue> me, Dictionary<TKey, TValue> merge)
        {
            foreach (var item in merge)
            {
                me[item.Key] = item.Value;
            }
        }
    }

    public class JSONParser
    {



        [LexerConfigurationAttribute]
        public  ILexer<JsonToken> BuildJsonLexer(ILexer<JsonToken> lexer)
        {                  
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.DOUBLE, "[0-9]+\\.[0-9]+"));
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.INT, "[0-9]+"));            
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.STRING, "(\\\")([^(\\\")]*)(\\\")"));
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.BOOLEAN, "(true|false)"));
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.NULL, "(null)"));
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.ACCG, "{"));
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.ACCD, "}"));
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.CROG, "\\["));
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.CROD, "\\]"));
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.COLON, ":"));
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.COMMA, ","));
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.WS, "[ \\t]+", true));
            lexer.AddDefinition(new TokenDefinition<JsonToken>(JsonToken.EOL, "[\\n\\r]+", true, true));
            return lexer;
        }


        #region root

        [Reduction("root : value")]
        public  object Root(object value)
        {
            return value;
        }


        #endregion

        #region VALUE

        [Reduction("value : STRING")]
        public  object StringValue(Token<JsonToken> stringToken)
        {
            return stringToken.StringWithoutQuotes;
        }

        [Reduction("value : INT")]
        public  object IntValue(Token<JsonToken> intToken)
        {
            return intToken.IntValue;
        }

        [Reduction("value : DOUBLE")]
        public object DoubleValue(Token<JsonToken> doubleToken)
        {
            double dbl = double.MinValue;
            try
            {
                string[] doubleParts = doubleToken.Value.Split('.');
                dbl = double.Parse(doubleParts[0]);
                if (doubleParts.Length > 1)
                {
                    double decimalPart = double.Parse(doubleParts[1]);
                    for (int i = 0; i < doubleParts[1].Length; i++)
                    {
                        decimalPart = decimalPart / 10.0;
                    }
                    dbl += decimalPart;
                }
            }
            catch (Exception e)
            {
                dbl = double.MinValue;
            }

            return dbl;
        }

        [Reduction("value : BOOLEAN")]
        public  object BooleanValue(Token<JsonToken> boolToken)
        {
            return bool.Parse(boolToken.Value);
        }

        [Reduction("value : NULL")]
        public  object NullValue(object forget)
        {
            return null;
        }

        [Reduction("value : object")]
        public  object ObjectValue(object value)
        {
            return value;
        }

        [Reduction("value: list")]
        public  object ListValue(List<object> list)
        {
            return list;
        }

        #endregion

        #region OBJECT

        [Reduction("object: ACCG ACCD")]
        public  object EmptyObjectValue(object accg , object accd)
        {
            return new Dictionary<string, object>();
        }

        [Reduction("object: ACCG members ACCD")]
        public  object AttributesObjectValue(object accg ,Dictionary<string,object> members, object accd)
        {
            return members;
        }


        #endregion
        #region LIST

        [Reduction("list: CROG CROD")]
        public  object EmptyList(object crog , object crod)
        {
            return new List<object>();
        }

        [Reduction("list: CROG listElements CROD")]
        public  object List(object crog ,List<object> elements, object crod)
        {
            return elements;
        }


        [Reduction("listElements: value COMMA listElements")]
        public  object ListElementsMany(object value, object comma, List<object> tail)
        {
            List<object> elements = new List<object>() { value};
            elements.AddRange(tail);
            return elements;
        }

        [Reduction("listElements: value")]
        public  object ListElementsOne(object element)
        {
            return new List<object>() { element };
        }


        #endregion

        #region PROPERTIES

        [Reduction("property: STRING COLON value")]
        public  object property(Token<JsonToken> key, object colon, object value)
        {
            return new KeyValuePair<string, object>(key.StringWithoutQuotes, value);
        }

       
        [Reduction("members : property COMMA members")]
        public  object ManyMembers(KeyValuePair<string, object> pair, object comma, Dictionary<string, object> tail)
        {
            Dictionary<string, object> members = new Dictionary<string, object>();
            members[pair.Key] = pair.Value;
            members.Merge(tail);
            return members;
        }

        [Reduction("members : property")]
        public  object SingleMember(KeyValuePair<string, object> pair)
        {
            Dictionary<string, object> members = new Dictionary<string, object>();
            members.Add(pair.Key, pair.Value);
            return members;
        }

        #endregion




    }
}
