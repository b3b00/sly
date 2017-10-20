﻿using sly.parser.syntax;
using sly.lexer;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using sly.parser.generator;

namespace sly.parser.llparser
{


    public class RecursiveDescentSyntaxParser<T,OUT> : ISyntaxParser<T> where T : struct
    {
        public ParserConfiguration<T,OUT> Configuration { get; set; }

        public string StartingNonTerminal { get; set; }


        public RecursiveDescentSyntaxParser(ParserConfiguration<T,OUT> configuration, string startingNonTerminal)
        {
            Configuration = configuration;
            StartingNonTerminal = startingNonTerminal;
            InitializeStartingTokens(Configuration, startingNonTerminal);
        }

        #region STARTING_TOKENS


        protected virtual void InitializeStartingTokens(ParserConfiguration<T,OUT> configuration, string root)
        {


            Dictionary<string, NonTerminal<T>> nts = configuration.NonTerminals;


            InitStartingTokensForNonTerminal(nts, root);
            foreach (NonTerminal<T> nt in nts.Values)
            {
                foreach (Rule<T> rule in nt.Rules)
                {
                    if (rule.PossibleLeadingTokens == null || rule.PossibleLeadingTokens.Count == 0)
                    {
                        InitStartingTokensForRule(nts, rule);
                    }
                }
            }
        }

        protected virtual void InitStartingTokensForNonTerminal(Dictionary<string, NonTerminal<T>> nonTerminals, string name)
        {
            if (nonTerminals.ContainsKey(name))
            {
                NonTerminal<T> nt = nonTerminals[name];
                nt.Rules.ForEach(r => InitStartingTokensForRule(nonTerminals, r));
            }
            return;
        }

        protected virtual void InitStartingTokensForRule(Dictionary<string, NonTerminal<T>> nonTerminals, Rule<T> rule)
        {
            if (rule.PossibleLeadingTokens == null || rule.PossibleLeadingTokens.Count == 0)
            {
                rule.PossibleLeadingTokens = new List<T>();
                if (rule.Clauses.Count > 0)
                {
                    IClause<T> first = rule.Clauses[0];
                    if (first is TerminalClause<T>)
                    {
                        TerminalClause<T> term = first as TerminalClause<T>;
                        rule.PossibleLeadingTokens.Add(term.ExpectedToken);
                        rule.PossibleLeadingTokens = rule.PossibleLeadingTokens.Distinct<T>().ToList<T>();
                    }
                    else
                    {
                        NonTerminalClause<T> nonterm = first as NonTerminalClause<T>;
                        InitStartingTokensForNonTerminal(nonTerminals, nonterm.NonTerminalName);
                        if (nonTerminals.ContainsKey(nonterm.NonTerminalName))
                        {
                            NonTerminal<T> firstNonTerminal = nonTerminals[nonterm.NonTerminalName];
                            firstNonTerminal.Rules.ForEach(r =>
                            {
                                rule.PossibleLeadingTokens.AddRange(r.PossibleLeadingTokens);
                            });
                            rule.PossibleLeadingTokens = rule.PossibleLeadingTokens.Distinct<T>().ToList<T>();
                        }
                        else
                        {
                            ;
                        }
                    }
                }

            }
        }

        #endregion

        #region parsing

    
        public SyntaxParseResult<T> Parse(IList<Token<T>> tokens, string startingNonTerminal = null)
        {
            string start = startingNonTerminal ?? StartingNonTerminal;
            Dictionary<string, NonTerminal<T>> NonTerminals = Configuration.NonTerminals;
            List<UnexpectedTokenSyntaxError<T>> errors = new List<UnexpectedTokenSyntaxError<T>>();
            NonTerminal<T> nt = NonTerminals[start];

            List<Rule<T>> rules = nt.Rules.Where<Rule<T>>(r => r.PossibleLeadingTokens.Contains(tokens[0].TokenID)).ToList<Rule<T>>();

            List<SyntaxParseResult<T>> rs = new List<SyntaxParseResult<T>>();
            foreach (Rule<T> rule in rules)
            {
                SyntaxParseResult<T> r = Parse(tokens, rule, 0, start);                
                rs.Add(r);                
            }
            SyntaxParseResult<T> result = null;


            if (rs.Count > 0)
            {
                result = rs.Find(r => r.IsEnded && !r.IsError);

                if (result == null)
                {
                    List<int> endingPositions = rs.Select(r => r.EndingPosition).ToList<int>();
                    int lastposition = endingPositions.Max();
                    List<SyntaxParseResult<T>> furtherResults = rs.Where<SyntaxParseResult<T>>(r => r.EndingPosition == lastposition).ToList<SyntaxParseResult<T>>();
                    errors.Add(new UnexpectedTokenSyntaxError<T>(tokens[lastposition], null));
                    furtherResults.ForEach(r =>
                    {
                        if (r.Errors != null)
                        {
                            errors.AddRange(r.Errors);
                        }
                    });
                }

            }
            if (result == null)
            {
                result = new SyntaxParseResult<T>();
                errors.Sort();
                if (errors.Count > 0)
                {
                    List<UnexpectedTokenSyntaxError<T>> singleError = new List<UnexpectedTokenSyntaxError<T>>() { errors[errors.Count() - 1] };
                    result.Errors = singleError;
                }
                else
                {
                    result.Errors = errors;
                }
                result.IsError = true;
            }
            return result;

        }


        public virtual SyntaxParseResult<T> Parse(IList<Token<T>> tokens, Rule<T> rule, int position, string nonTerminalName)
        {
            int currentPosition = position;
            List<UnexpectedTokenSyntaxError<T>> errors = new List<UnexpectedTokenSyntaxError<T>>();
            bool isError = false;
            List<ISyntaxNode<T>> children = new List<ISyntaxNode<T>>();
            if (rule.PossibleLeadingTokens.Contains(tokens[position].TokenID))
            {
                if (rule.Clauses != null && rule.Clauses.Count > 0)
                {
                    children = new List<ISyntaxNode<T>>();
                    foreach (IClause<T> clause in rule.Clauses)
                    {
                        if (clause is TerminalClause<T>)
                        {
                            SyntaxParseResult<T> termRes = ParseTerminal(tokens, clause as TerminalClause<T>, currentPosition);
                            if (!termRes.IsError)
                            {
                                children.Add(termRes.Root);
                                currentPosition = termRes.EndingPosition;
                            }
                            else
                            {
                                Token<T> tok = tokens[currentPosition];
                                errors.Add(new UnexpectedTokenSyntaxError<T>(tok, ((TerminalClause<T>)clause).ExpectedToken));
                            }
                            isError = isError || termRes.IsError;
                        }
                        else if (clause is NonTerminalClause<T>)
                        {
                            SyntaxParseResult<T> nonTerminalResult =
                                ParseNonTerminal(tokens, clause as NonTerminalClause<T>, currentPosition);
                            if (!nonTerminalResult.IsError)
                            {
                                children.Add(nonTerminalResult.Root);
                                currentPosition = nonTerminalResult.EndingPosition;
                                if (nonTerminalResult.Errors != null && nonTerminalResult.Errors.Any())
                                {
                                    errors.AddRange(nonTerminalResult.Errors);
                                }
                            }
                            else
                            {
                                errors.AddRange(nonTerminalResult.Errors);
                            }
                            isError = isError || nonTerminalResult.IsError;
                            // TODO
                        }
                        else
                        {
                            ;
                        }
                        if (isError)
                        {
                            break;
                        }
                    }
                }
            }

            SyntaxParseResult<T> result = new SyntaxParseResult<T>();
            result.IsError = isError;
            result.Errors = errors;
            result.EndingPosition = currentPosition;
            if (!isError)
            {
                
                SyntaxNode<T> node = new SyntaxNode<T>(nonTerminalName + "__" + rule.Key, children);                
                node = ManageExpressionRules(rule, node);
                result.Root =  node;
                result.IsEnded = currentPosition >= tokens.Count - 1
                                || currentPosition == tokens.Count - 2 && tokens[tokens.Count - 1].TokenID.Equals(default(T));
                if (rule.IsExpressionRule)
                {

                }
            }

            return result;
        }

        protected SyntaxNode<T> ManageExpressionRules(Rule<T> rule, SyntaxNode<T> node)
        {
            int operatorIndex = -1;
            if (rule.IsExpressionRule)
            {
                
                if (node.Children.Count == 2)
                {
                    operatorIndex = 1;
                }
                else if (node.Children.Count == 2)
                {
                    operatorIndex = 0;
                }
                if (operatorIndex > 0)
                {
                    if (node.Children[operatorIndex] is SyntaxLeaf<T> operatorNode)
                    {
                        if (operatorNode != null)
                        {
                            if (rule.VisitorMethods != null && rule.VisitorMethods.Any())
                            {
                                if (rule.VisitorMethods.ContainsKey(operatorNode.Token.TokenID))
                                {
                                    var visitor = rule.VisitorMethods[operatorNode.Token.TokenID];
                                    node.visitor = visitor;
                                }
                            }
                        }
                    }
                }
            }
            return node;
                
        }

        public SyntaxParseResult<T> ParseTerminal(IList<Token<T>> tokens, TerminalClause<T> term, int position)
        {
            SyntaxParseResult<T> result = new SyntaxParseResult<T>();
            result.IsError = !term.Check(tokens[position].TokenID);
            result.EndingPosition = !result.IsError ? position + 1 : position;
            result.Root = new SyntaxLeaf<T>(tokens[position]);
            return result;
        }


        public SyntaxParseResult<T> ParseNonTerminal(IList<Token<T>> tokens, NonTerminalClause<T> nonTermClause, int currentPosition)
        {

            if (nonTermClause.NonTerminalName == "members")
            {
                ;
            }

            NonTerminal<T> nt = Configuration.NonTerminals[nonTermClause.NonTerminalName];
            bool found = false;
            bool isError = false;
            List<UnexpectedTokenSyntaxError<T>> errors = new List<UnexpectedTokenSyntaxError<T>>();

            int i = 0;

            List<T> allAcceptableTokens = new List<T>();
            nt.Rules.ForEach(r =>
            {
                if (r != null && r.PossibleLeadingTokens != null)
                {
                    allAcceptableTokens.AddRange(r.PossibleLeadingTokens);
                }
                else
                {
                    ;
                }
            });
            allAcceptableTokens = allAcceptableTokens.Distinct<T>().ToList<T>();

            List<Rule<T>> rules = nt.Rules
                .Where<Rule<T>>(r => r.PossibleLeadingTokens.Contains(tokens[currentPosition].TokenID) || r.MayBeEmpty)
                .ToList<Rule<T>>();

            if (rules.Count == 0)
            {
                isError = true;
                errors.Add(new UnexpectedTokenSyntaxError<T>(tokens[currentPosition],
                    allAcceptableTokens.ToArray<T>()));
            }

            List<UnexpectedTokenSyntaxError<T>> innerRuleErrors = new List<UnexpectedTokenSyntaxError<T>>();
            SyntaxParseResult<T> okResult = null;
            int greaterIndex = 0;
            bool allRulesInError = true;
            while (!found && i < rules.Count)
            {
                Rule<T> innerrule = rules[i];
                SyntaxParseResult<T> innerRuleRes = Parse(tokens, innerrule, currentPosition, nonTermClause.NonTerminalName);
                if (!innerRuleRes.IsError && okResult == null)
                {
                    okResult = innerRuleRes;
                    found = true;
                    currentPosition = innerRuleRes.EndingPosition;
                }
                bool other = greaterIndex == 0 && innerRuleRes.EndingPosition == 0;
                if ((innerRuleRes.EndingPosition > greaterIndex && innerRuleRes.Errors != null &&
                     innerRuleRes.Errors.Any()) || other)
                {
                    greaterIndex = innerRuleRes.EndingPosition;
                    innerRuleErrors.Clear();
                    innerRuleErrors.AddRange(innerRuleRes.Errors);
                }
                allRulesInError = allRulesInError && innerRuleRes.IsError;
                i++;
            }
            isError = isError || allRulesInError;
            errors.AddRange(innerRuleErrors);

            SyntaxParseResult<T> result = new SyntaxParseResult<T>();
            result.Errors = errors;
            if (okResult != null)
            {
                result.Root = okResult.Root;
                result.IsError = false;
                result.EndingPosition = okResult.EndingPosition;
                result.IsEnded = okResult.IsEnded;

                result.Errors = errors;
            }
            else
            {
                result.IsError = true;
                result.Errors = errors;
                result.EndingPosition = greaterIndex;
            }
            return result;
        }

        #endregion

    }
}
