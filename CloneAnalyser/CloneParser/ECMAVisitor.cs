using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace CloneParser
{
    class ECMAVisitor : ECMAScriptBaseVisitor<ACT>
    {
        private CommonTokenStream tokens;

        public ECMAVisitor(CommonTokenStream tokens)
        {
            this.tokens = tokens;
        }

        /// <summary> Creates source info based on given source interval.</summary>
        /// <param name="sourceInterval"> Interval of source lexems</param>
        /// <returns> Source info data</returns>
        private SourceInfo GetSourceInfo(Interval sourceInterval)
        {
            StringBuilder sb = new StringBuilder();
            int prevType = -1;
            bool functionBodyElimination = false; //Whenever next function declaration is discovered, following brace will be removed
            int functionBraceBalance = 0; //Keeps track of the running function brace balance
            int braceBalance = 0; //Keeps track of all brace balance
            bool lineTerminator = false;
            for (int i = sourceInterval.a; i <= sourceInterval.b; i++)
            {
                IToken token = tokens.Get(i);
                int type = token.Type;

                //Add node text
                if (type != ECMAScriptLexer.WhiteSpaces && 
                    type != ECMAScriptLexer.SingleLineComment && 
                    type != ECMAScriptLexer.MultiLineComment)
                {
                    if (type == ECMAScriptLexer.LineTerminator)
                    {
                        if (!lineTerminator)
                        {
                            sb.Append("\n");
                            for (int j=0; j<braceBalance; j++)
                            {
                                sb.Append("\t");
                            }
                            lineTerminator = true;
                        }
                        continue;
                    }
                    lineTerminator = false;

                    //Close brace handling
                    if (type == ECMAScriptLexer.CloseBrace)
                    {
                        if (functionBodyElimination)
                        {
                            functionBraceBalance--;
                            if (functionBraceBalance == 0)
                            {
                                functionBodyElimination = false;
                            }
                        }
                        braceBalance--;
                    }
                    //Token text handling
                    if (!functionBodyElimination || functionBraceBalance == 0)
                    {
                        sb.Append(token.Text);
                    }
                    //Open brace handling
                    if (type == ECMAScriptLexer.OpenBrace)
                    {
                        if (functionBodyElimination)
                        {
                            functionBraceBalance++;
                        }
                        braceBalance++;
                    }
                    //Function handling
                    if (type == ECMAScriptLexer.Function)
                    {
                        //functionBodyElimination = true;
                    }
                }
                //Add single whitespace if it existed before
                else if (prevType > 0 &&
                    prevType != ECMAScriptLexer.WhiteSpaces &&
                    prevType != ECMAScriptLexer.LineTerminator &&
                    prevType != ECMAScriptLexer.SingleLineComment &&
                    prevType != ECMAScriptLexer.MultiLineComment
                    )
                {
                    if (!functionBodyElimination || functionBraceBalance == 0)
                    {
                        sb.Append(" ");
                    }
                }
                prevType = type;
            }

            IToken startToken = tokens.Get(sourceInterval.a);
            IToken endToken = tokens.Get(sourceInterval.b);
            return new SourceInfo(sb.ToString(), startToken.Line, startToken.StartIndex, endToken.Line, endToken.StopIndex);
        }

        // Visitor pattern for the concrete syntax tree translation //

        public override ACT VisitProgram([NotNull] ECMAScriptParser.ProgramContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Program);

            int i = 0;

            if (context.sourceElements() != null)
            {
                IParseTree child = context.sourceElements().GetChild(i);
                while (child != null)
                {
                    ACT res = Visit(child);
                    node.AddChild(res);
                    child = context.sourceElements().GetChild(++i);
                }
            }

            return node;
        }

        public override ACT VisitTerminal(ITerminalNode node)
        {
            IToken startToken = tokens.Get(node.SourceInterval.a);
            IToken endToken = tokens.Get(node.SourceInterval.b);
            SourceInfo source = new SourceInfo(node.GetText(), startToken.Line, startToken.StartIndex, endToken.Line, endToken.StopIndex);
            ACT term = new ACT(source, ACTtype.Term);
            term.value = node.GetText();

            return term;
        }

        public override ACT VisitFunctionDeclaration([NotNull] ECMAScriptParser.FunctionDeclarationContext context)
        {
            String functionName = context.Identifier() == null ? "" : Visit(context.Identifier()).value;
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Function, functionName);
            
            if (context.formalParameterList() != null)
            {
                node.AddChildBlock(Visit(context.formalParameterList()), true);
            }
            node.AddChildBlock(Visit(context.functionBody()));

            return node;
        }

        public override ACT VisitFormalParameterList([NotNull] ECMAScriptParser.FormalParameterListContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);

            foreach (var identifier in context.Identifier())
            {
                ACT child = Visit(identifier);
                node.AddChild(child);
            }

            return node;
        }

        public override ACT VisitExpressionSequence([NotNull] ECMAScriptParser.ExpressionSequenceContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);

            ECMAScriptParser.SingleExpressionContext[] expressions = context.singleExpression();
            for (int i = 0; i < expressions.Length; i++)
            {
                ACT child = Visit(expressions[i]);
                node.AddChild(child);
            }

            return node;
        }

        //-- Expressions
        #region
        public override ACT VisitFunctionExpression([NotNull] ECMAScriptParser.FunctionExpressionContext context)
        {
            String functionName = context.Identifier() == null ? "" : Visit(context.Identifier()).value;
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Function, functionName);

            if (context.formalParameterList() != null)
            {
                node.AddChildBlock(Visit(context.formalParameterList()), true);
            }
            node.AddChildBlock(Visit(context.functionBody()));

            return node;
        }

        public override ACT VisitMemberIndexExpression([NotNull] ECMAScriptParser.MemberIndexExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "[]");
            node.AddChild(Visit(context.singleExpression()));
            node.AddChildBlock(Visit(context.expressionSequence()));
            return node;
        }

        public override ACT VisitMemberDotExpression([NotNull] ECMAScriptParser.MemberDotExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, ".");
            node.AddChild(Visit(context.singleExpression()));
            node.AddChildBlock(Visit(context.identifierName()));
            return node;
        }

        public override ACT VisitArgumentsExpression([NotNull] ECMAScriptParser.ArgumentsExpressionContext context)
        {
            //TODO
            return base.VisitArgumentsExpression(context);
        }

        public override ACT VisitNewExpression([NotNull] ECMAScriptParser.NewExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "new");
            node.AddChild(Visit(context.singleExpression()));
            if (context.arguments() != null)
                //Maby switch to AccChildBlock later?
                node.AddChild(Visit(context.arguments()));
            return node;
        }

        public override ACT VisitPostIncrementExpression([NotNull] ECMAScriptParser.PostIncrementExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Assign);
            ACT single = Visit(context.singleExpression());
            node.AddChild(single);

            ACT call = new ACT(source, ACTtype.Call, "+");
            ACT single2 = Visit(context.singleExpression());
            call.AddChild(single2);
            call.AddChild(new ACT(source, ACTtype.Term, "1"));

            node.AddChild(call);
            return node;
        }

        public override ACT VisitPostDecreaseExpression([NotNull] ECMAScriptParser.PostDecreaseExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Assign);
            ACT single = Visit(context.singleExpression());
            node.AddChild(single);

            ACT call = new ACT(source, ACTtype.Call, "-");
            ACT single2 = Visit(context.singleExpression());
            call.AddChild(single2);
            call.AddChild(new ACT(source, ACTtype.Term, "1"));

            node.AddChild(call);
            return node;
        }

        public override ACT VisitDeleteExpression([NotNull] ECMAScriptParser.DeleteExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "delete");
            node.AddChild(Visit(context.singleExpression()));
            return node;
        }

        public override ACT VisitVoidExpression([NotNull] ECMAScriptParser.VoidExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "void");
            node.AddChild(Visit(context.singleExpression()));
            return node;
        }

        public override ACT VisitTypeofExpression([NotNull] ECMAScriptParser.TypeofExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "typeof");
            node.AddChild(Visit(context.singleExpression()));
            return node;
        }

        public override ACT VisitPreIncrementExpression([NotNull] ECMAScriptParser.PreIncrementExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Assign);
            ACT single = Visit(context.singleExpression());
            node.AddChild(single);

            ACT call = new ACT(source, ACTtype.Call, "+");
            ACT single2 = Visit(context.singleExpression());
            call.AddChild(single2);
            call.AddChild(new ACT(source, ACTtype.Term, "1"));

            node.AddChild(call);
            return node;
        }

        public override ACT VisitPreDecreaseExpression([NotNull] ECMAScriptParser.PreDecreaseExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Assign);
            ACT single = Visit(context.singleExpression());
            node.AddChild(single);

            ACT call = new ACT(source, ACTtype.Call, "-");
            ACT single2 = Visit(context.singleExpression());
            call.AddChild(single2);
            call.AddChild(new ACT(source, ACTtype.Term, "1"));

            node.AddChild(call);
            return node;
        }

        public override ACT VisitUnaryPlusExpression([NotNull] ECMAScriptParser.UnaryPlusExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "+");
            node.AddChild(Visit(context.singleExpression()));
            return node;
        }

        public override ACT VisitUnaryMinusExpression([NotNull] ECMAScriptParser.UnaryMinusExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "-");
            node.AddChild(Visit(context.singleExpression()));
            return node;
        }

        public override ACT VisitBitNotExpression([NotNull] ECMAScriptParser.BitNotExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "~");
            node.AddChild(Visit(context.singleExpression()));
            return node;
        }

        public override ACT VisitNotExpression([NotNull] ECMAScriptParser.NotExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "!");
            node.AddChild(Visit(context.singleExpression()));
            return node;
        }

        public override ACT VisitMultiplicativeExpression([NotNull] ECMAScriptParser.MultiplicativeExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, Visit(context.children[1]).value);
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            return node;
        }

        public override ACT VisitAdditiveExpression([NotNull] ECMAScriptParser.AdditiveExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, Visit(context.children[1]).value);
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            return node;
        }

        public override ACT VisitBitShiftExpression([NotNull] ECMAScriptParser.BitShiftExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, Visit(context.children[1]).value);
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            return node;
        }

        public override ACT VisitRelationalExpression([NotNull] ECMAScriptParser.RelationalExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, Visit(context.children[1]).value);
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            return node;
        }

        public override ACT VisitInstanceofExpression([NotNull] ECMAScriptParser.InstanceofExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "instanceof");
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            return node;
        }

        public override ACT VisitInExpression([NotNull] ECMAScriptParser.InExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "in");
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            return node;
        }

        public override ACT VisitEqualityExpression([NotNull] ECMAScriptParser.EqualityExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, Visit(context.children[1]).value);
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            return node;
        }

        public override ACT VisitBitAndExpression([NotNull] ECMAScriptParser.BitAndExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "&");
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            return node;
        }

        public override ACT VisitBitXOrExpression([NotNull] ECMAScriptParser.BitXOrExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "^");
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            return node;
        }

        public override ACT VisitBitOrExpression([NotNull] ECMAScriptParser.BitOrExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "|");
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            return node;
        }

        public override ACT VisitLogicalAndExpression([NotNull] ECMAScriptParser.LogicalAndExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "&&");
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            return node;
        }

        public override ACT VisitLogicalOrExpression([NotNull] ECMAScriptParser.LogicalOrExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "||");
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            return node;
        }

        public override ACT VisitTernaryExpression([NotNull] ECMAScriptParser.TernaryExpressionContext context)
        {
            //-- TODO -- Does not work correctly, should find parent and inserts those into 1 and 2
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.If);
            node.AddChild(Visit(context.singleExpression()[0]));
            node.AddChild(Visit(context.singleExpression()[1]));
            node.AddChild(Visit(context.singleExpression()[2]));

            return node;
        }

        public override ACT VisitAssignmentOperatorExpression([NotNull] ECMAScriptParser.AssignmentOperatorExpressionContext context)
        {
            ACT res;
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Assign);
            ACT single = Visit(context.singleExpression());
            node.AddChild(single);

            string value = Visit(context.assignmentOperator()).value;
            value = value.Remove(value.Length - 1);
            ACT call = new ACT(source, ACTtype.Call, value);

            ACT expressions = Visit(context.expressionSequence());
            node.AddChild(call);
            if (expressions.children.Count <= 1)
            {
                res = node;
                call.AddChild(single);
                call.AddChild(expressions);
            }
            else
            {
                res = expressions;
                call.AddChild(single);
                call.AddChild(expressions.children[0]);
                expressions.children[0] = node;
            }

            return res;
        }

        public override ACT VisitThisExpression([NotNull] ECMAScriptParser.ThisExpressionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Term, "This");
            return node;
        }

        public override ACT VisitIdentifierExpression([NotNull] ECMAScriptParser.IdentifierExpressionContext context)
        {
            return base.VisitIdentifierExpression(context);
        }

        public override ACT VisitLiteralExpression([NotNull] ECMAScriptParser.LiteralExpressionContext context)
        {
            return base.VisitLiteralExpression(context);
        }

        public override ACT VisitArrayLiteralExpression([NotNull] ECMAScriptParser.ArrayLiteralExpressionContext context)
        {
            return base.VisitArrayLiteralExpression(context);
        }

        public override ACT VisitObjectLiteralExpression([NotNull] ECMAScriptParser.ObjectLiteralExpressionContext context)
        {
            return base.VisitObjectLiteralExpression(context);
        }

        public override ACT VisitParenthesizedExpression([NotNull] ECMAScriptParser.ParenthesizedExpressionContext context)
        {
            return base.VisitParenthesizedExpression(context);
        }
        #endregion

        //-- variable declarations

        #region
        public override ACT VisitVariableDeclarationList([NotNull] ECMAScriptParser.VariableDeclarationListContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);

            ECMAScriptParser.VariableDeclarationContext[] declarations = context.variableDeclaration();
            for (int i=0; i < declarations.Length; i++)
            {           
                ACT res = Visit(declarations[i]);
                node.AddChild(res);   
            }

            return node;
        }

        public override ACT VisitVariableDeclaration([NotNull] ECMAScriptParser.VariableDeclarationContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Assign);
            node.AddChild(Visit(context.Identifier()));
            if (context.initialiser() != null)
            {
                node.AddChild(Visit(context.initialiser()));
            }
            else
            {
                node.AddChild(ACT.undefinedInitializer);
            }
            return node;
        }

        // -- All expressionts --
        public override ACT VisitAssignmentExpression([NotNull] ECMAScriptParser.AssignmentExpressionContext context)
        {
            ACT res;
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Assign);
            node.AddChild(Visit(context.singleExpression()));

            ACT expressions = Visit(context.expressionSequence());
            if (expressions.children.Count <= 1)
            {
                res = node;
                res.AddChild(expressions);
            }
            else
            {
                res = expressions;
                node.AddChild(expressions.children[0]);
                expressions.children[0] = node;
            }

            return res;
        }
        #endregion

        //---- Case ----
        #region
        public override ACT VisitCaseBlock([NotNull] ECMAScriptParser.CaseBlockContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);

            ACT defaultClause = null;

            for (int i = 0; i < context.ChildCount; i++)
            {
                IParseTree child = context.GetChild(i);

                Type childType = child.GetType();
                if (childType.Equals(typeof(CloneParser.ECMAScriptParser.CaseClausesContext)))
                {
                    node.AddChild(Visit(child));
                }
                else if (childType.Equals(typeof(CloneParser.ECMAScriptParser.DefaultClauseContext)))
                {
                    defaultClause = Visit(child);
                }
            }

            if (defaultClause != null)
            {
                node.AddChildBlock(defaultClause);
            }

            return node;
        }

        public override ACT VisitCaseClauses([NotNull] ECMAScriptParser.CaseClausesContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);

            foreach (var clause in context.caseClause())
            {
                node.AddChild(Visit(clause));
            }

            return node;
        }

        public override ACT VisitCaseClause([NotNull] ECMAScriptParser.CaseClauseContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.If);
            node.AddChild(Visit(context.expressionSequence()));
            if (context.statementList() != null)
            {
                node.AddChildBlock(Visit(context.statementList()));
            }
            return node;
        }

        public override ACT VisitDefaultClause([NotNull] ECMAScriptParser.DefaultClauseContext context)
        {
            return Visit(context.statementList());
        }
        #endregion

        // catch/finally
        #region
        public override ACT VisitCatchProduction([NotNull] ECMAScriptParser.CatchProductionContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);
            node.AddChild(Visit(context.Identifier()));
            node.AddChild(Visit(context.block()));
            return node;
        }

        public override ACT VisitFinallyProduction([NotNull] ECMAScriptParser.FinallyProductionContext context)
        {
            return Visit(context.block());
        }
        #endregion

        //---- Statements ----
        #region
        public override ACT VisitStatementList([NotNull] ECMAScriptParser.StatementListContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);

            foreach (var statement in context.statement())
            {
                node.AddChild(Visit(statement));
            }

            return node;
        }

        public override ACT VisitBlock([NotNull] ECMAScriptParser.BlockContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Block);
            if (context.statementList() != null)
            {
                node.AddChild(Visit(context.statementList()));
            }
            return node;
        }

        public override ACT VisitVariableStatement([NotNull] ECMAScriptParser.VariableStatementContext context)
        {
            return Visit(context.variableDeclarationList());
        }

        public override ACT VisitEmptyStatement([NotNull] ECMAScriptParser.EmptyStatementContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            return new ACT(source, ACTtype.Empty);
        }

        public override ACT VisitExpressionStatement([NotNull] ECMAScriptParser.ExpressionStatementContext context)
        {
            return Visit(context.expressionSequence());
        }

        public override ACT VisitIfStatement([NotNull] ECMAScriptParser.IfStatementContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.If);
            ACT expression = Visit(context.expressionSequence());
            node.AddChild(expression);

            ECMAScriptParser.StatementContext[] statements = context.statement();
            if (statements.Length == 0)
            {
                node.AddChild(new ACT(source, ACTtype.Block));
            }
            else if (statements.Length == 1)
            {
                node.AddChild(Visit(statements[0]));
            }
            else
            {
                node.AddChild(Visit(statements[0]));
                node.AddChild(Visit(statements[1]));
            }
            
            return node;
        }

        public override ACT VisitIterationStatement([NotNull] ECMAScriptParser.IterationStatementContext context)
        {
            return base.VisitIterationStatement(context);
        }

        public override ACT VisitContinueStatement([NotNull] ECMAScriptParser.ContinueStatementContext context)
        {
            //TODO: Should also parse lable if exists
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Continue);

            if (context.Identifier() != null)
            {
                node.value = Visit(context.Identifier()).value;
            }

            return node;
        }

        public override ACT VisitBreakStatement([NotNull] ECMAScriptParser.BreakStatementContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Break);

            if (context.Identifier() != null)
            {
                node.value = Visit(context.Identifier()).value;
            }

            return node;
        }

        public override ACT VisitReturnStatement([NotNull] ECMAScriptParser.ReturnStatementContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Return);

            if (context.expressionSequence() != null)
            {
                node.AddChild(Visit(context.expressionSequence()));
            }

            return node;
        }

        public override ACT VisitWithStatement([NotNull] ECMAScriptParser.WithStatementContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);
            node.AddChild(Visit(context.expressionSequence()));
            node.AddChild(Visit(context.statement()));
            return node;
        }

        public override ACT VisitLabelledStatement([NotNull] ECMAScriptParser.LabelledStatementContext context)
        {
            //TODO: Should also parse lable if exists
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Label);
            node.AddChild(Visit(context.statement()));
            return node;
        }

        public override ACT VisitSwitchStatement([NotNull] ECMAScriptParser.SwitchStatementContext context)
        {
            ACT expression = Visit(context.expressionSequence());
            ACT caseBlock = Visit(context.caseBlock());

            SourceInfo source = GetSourceInfo(context.SourceInterval);
            if (caseBlock.children.Count == 0)
                return new ACT(source, ACTtype.Empty);
            /*
            node.AddChild(expression);
            node.AddChild(caseBlock);
            */
            ACT node = caseBlock.children[0];
            for (int i=1; i < caseBlock.children.Count; i++)
            {
                caseBlock.children[i - 1].AddChild(caseBlock.children[i]);
            }

            return node;
        }

        public override ACT VisitThrowStatement([NotNull] ECMAScriptParser.ThrowStatementContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Throw);
            if (context.expressionSequence() != null)
            {
                node.AddChild(Visit(context.expressionSequence()));
            }
            return node;
        }

        public override ACT VisitTryStatement([NotNull] ECMAScriptParser.TryStatementContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);
            ACT trycatch = new ACT(source, ACTtype.TryCatch);

            trycatch.AddChild(Visit(context.block()));
            if (context.catchProduction() != null)
            {
                trycatch.AddChild(Visit(context.catchProduction()));
            }
            if (context.finallyProduction() != null)
            {
                node.AddChild(Visit(context.finallyProduction()));
            }

            node.AddChild(trycatch);
            return node;
        }

        public override ACT VisitDebuggerStatement([NotNull] ECMAScriptParser.DebuggerStatementContext context)
        {
            //Do nothing
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            return new ACT(source, ACTtype.Empty);
        }
        #endregion

        //--- Iteration statements ----

        #region
        public override ACT VisitDoStatement([NotNull] ECMAScriptParser.DoStatementContext context)
        {
            //string source = TokensToString(context.GetTokens);
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);
            ACT whilenode = new ACT(source, ACTtype.While);
            ACT statement = Visit(context.statement());
            node.AddChild(statement);
            node.AddChild(whilenode);
            whilenode.AddChildBlock(Visit(context.expressionSequence()));
            whilenode.AddChild(statement);
            return node;
        }

        private void TokensToString(ITerminalNode[] tokens)
        {

        }

        public override ACT VisitWhileStatement([NotNull] ECMAScriptParser.WhileStatementContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT whilenode = new ACT(source, ACTtype.While);
            ACT statement = Visit(context.statement());
            whilenode.AddChildBlock(Visit(context.expressionSequence()));
            whilenode.AddChild(statement);
            return whilenode;
        }

        public override ACT VisitForStatement([NotNull] ECMAScriptParser.ForStatementContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);
            ACT whilenode = new ACT(source, ACTtype.While);
            ACT statement = Visit(context.statement());

            ACT[] exps = new ACT[3];
            int expi = 0;
            foreach (var child in context.children)
            {
                Type childType = child.GetType();
                if (child.GetText() == ";")
                {
                    expi++;
                }
                if (childType.Equals(typeof(CloneParser.ECMAScriptParser.ExpressionSequenceContext)))
                {
                    exps[expi] = Visit(child);
                }
            }

            if (exps[0] != null)
            {
                node.AddChild(exps[0]);
            }
            node.AddChild(whilenode);
            if (exps[1] != null)
            {
                whilenode.AddChildBlock(exps[1]);
            }
            else
            {
                whilenode.AddChild(new ACT(source, ACTtype.Block));
            }
            ACT bodyblock = new ACT(source, ACTtype.Block);
            bodyblock.AddChild(statement);
            if (exps[2] != null)
            {
                bodyblock.AddChild(exps[2]);
            }
            whilenode.AddChild(bodyblock);

            return node;
        }

        public override ACT VisitForVarStatement([NotNull] ECMAScriptParser.ForVarStatementContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);
            ACT whilenode = new ACT(source, ACTtype.While);
            ACT statement = Visit(context.statement());

            ACT[] exps = new ACT[2];
            int expi = -1;
            foreach (var child in context.children)
            {
                Type childType = child.GetType();
                if (child.GetText() == ";")
                {
                    expi++;
                }
                if (childType.Equals(typeof(CloneParser.ECMAScriptParser.ExpressionSequenceContext)))
                {
                    exps[expi] = Visit(child);
                }
            }

            node.AddChild(Visit(context.variableDeclarationList()));

            node.AddChild(whilenode);
            if (exps[0] != null)
            {
                whilenode.AddChildBlock(exps[0]);
            }
            else
            {
                whilenode.AddChild(new ACT(source, ACTtype.Block));
            }
            ACT bodyblock = new ACT(source, ACTtype.Block);
            bodyblock.AddChild(statement);
            if (exps[1] != null)
            {
                bodyblock.AddChild(exps[1]);
            }
            whilenode.AddChild(bodyblock);

            return node;
        }

        public override ACT VisitForInStatement([NotNull] ECMAScriptParser.ForInStatementContext context)
        {
            //-- TODO
            return base.VisitForInStatement(context);
        }

        public override ACT VisitForVarInStatement([NotNull] ECMAScriptParser.ForVarInStatementContext context)
        {
            //-- TODO
            return base.VisitForVarInStatement(context);
        }
        #endregion

        public override ACT VisitElementList([NotNull] ECMAScriptParser.ElementListContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);

            foreach (var identifier in context.children)
            {
                ACT child = Visit(identifier);
                node.AddChild(child);
            }

            return node;
        }

        public override ACT VisitArgumentList([NotNull] ECMAScriptParser.ArgumentListContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);

            foreach (var identifier in context.children)
            {
                ACT child = Visit(identifier);
                node.AddChild(child);
            }

            return node;
        }

        public override ACT VisitArrayLiteral([NotNull] ECMAScriptParser.ArrayLiteralContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Call, "[]");
            if (context.elementList() != null)
            {
                node.AddChildBlock(Visit(context.elementList()));
            }

            return base.VisitArrayLiteral(context);
        }

        public override ACT VisitFunctionBody([NotNull] ECMAScriptParser.FunctionBodyContext context)
        {
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            ACT node = new ACT(source, ACTtype.Seq);

            int i = 0;

            if (context.sourceElements() != null)
            {
                IParseTree child = context.sourceElements().GetChild(i);
                while (child != null)
                {
                    ACT res = Visit(child);
                    node.AddChild(res);
                    child = context.sourceElements().GetChild(++i);
                }
            }

            return node;
        }

        public override ACT VisitStatement([NotNull] ECMAScriptParser.StatementContext context)
        {
            if (context.ChildCount > 0)
            {
                return Visit(context.children[0]);
            }
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            return new ACT(source, ACTtype.Empty);
        }

        public override ACT VisitElision([NotNull] ECMAScriptParser.ElisionContext context)
        {
            //Elision is additional commas between elements, we are ignoring them
            SourceInfo source = GetSourceInfo(context.SourceInterval);
            return new ACT(source, ACTtype.Empty);
        }
    }
}
