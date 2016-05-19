using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Antlr4.Runtime.Misc;

//Used template from http://stackoverflow.com/questions/19350705/how-do-i-pretty-print-productions-and-line-numbers-using-antlr4
//Useful information about C# target https://theantlrguy.atlassian.net/wiki/display/ANTLR4/C%23+Target

namespace CloneParser
{
    class ECMAListener : ECMAScriptBaseListener
    {
        public ACT ACT { get; set; }

        //Private
        private List<String> ruleNames;
        private StringBuilder builder = new StringBuilder();
        private ACT LastACTNode = null;

        public ECMAListener(Parser parser)
        {
            this.ruleNames = parser.RuleNames.ToList<String>();
            ACT = null;
        }

        public ECMAListener(List<String> ruleNames)
        {
            this.ruleNames = ruleNames;
        }

        //public override void EnterEveryRule([NotNull] ParserRuleContext context)
        public override void VisitTerminal([NotNull] ITerminalNode node)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }
            //builder.Append(context.Payload.GetText());
            builder.Append(Utils.EscapeWhitespace(Trees.GetNodeText(node, ruleNames), false));
        }

        public override void VisitErrorNode([NotNull] IErrorNode node)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(Utils.EscapeWhitespace(Trees.GetNodeText(node, ruleNames), false));
        }

        public override void ExitEveryRule([NotNull] ParserRuleContext ctx)
        {
            if (ctx.ChildCount > 0)
            {
                /*
                IToken positionToken = ctx.Start;
                if (positionToken != null)
                {
                    builder.Append(" [line ");
                    builder.Append(positionToken.Line);
                    builder.Append(", offset ");
                    builder.Append(positionToken.StartIndex);
                    builder.Append(':');
                    builder.Append(positionToken.StopIndex);
                    builder.Append("])");
                }
                else
                {
                    builder.Append(')');
                }
                */
            }
        }
        /*
        private void AddACTRule(ACTtypes type)
        {

        }
        */
        public override String ToString()
        {
            return builder.ToString();
        }

        /* ---------                --------- */

        /* --------- Specific rules --------- */

        /* ---------                --------- */
        public override void EnterProgram([NotNull] ECMAScriptParser.ProgramContext context) {
            ACT = new ACT(new SourceInfo("", 0, 0, 0, 0), ACTtype.Program);
            LastACTNode = ACT;
        }

        /*
        public override void EnterSourceElement([NotNull] ECMAScriptParser.SourceElementContext context)
        {
            ACT node = new ACT(ACTtypes.Stmt);
            LastACTNode = node;
        }*/

    }
}
