using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloneParser
{
    public class Program
    {
        /// <summary> Prints concrete syntax tree</summary>
        /// <param name="node"> Root node for concrete syntax tree</param>
        /// <param name="indent"> Indentation accumulator</param>
        private static void PrintParseTree(IParseTree node, string indent)
        {
            Console.WriteLine(indent + node.Payload.ToString());
            
            int cn = node.ChildCount;
            if (cn > 0)
            {
                indent += "  ";
                for (int i=0; i< cn; i++)
                {
                    PrintParseTree(node.GetChild(i), indent);
                }
            }
        }

        /// <summary> Parses antlr stream</summary>
        /// <param name="stream"> Input stream to be parsed</param>
        /// <returns> Returns corresponding abstract code tree root node</returns>
        private static ACT ParseAntlrStream(AntlrInputStream stream)
        {
            ECMAVisitor visitor;
            ECMAScriptParser.ProgramContext parseTree;
            try {
                ECMAScriptLexer lexer = new ECMAScriptLexer(stream);
                CommonTokenStream tokens = new CommonTokenStream(lexer);
                ECMAScriptParser parser = new ECMAScriptParser(tokens);

                parser.BuildParseTree = true;
                parseTree = parser.program();

                //Console.WriteLine(parseTree.ToString());
                visitor = new ECMAVisitor(tokens);
            }
            catch (System.NullReferenceException e)
            {
                Console.WriteLine("AntlrException: " + e);
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine("AntlrException: " + e);
                return null;
            }
            ACT act = visitor.Visit(parseTree);
            act.PostProcess();
            return act;
        }

        /// <summary> Parses streamreader</summary>
        /// <param name="stream"> Input streamreader/param>
        /// <returns> Returns corresponding abstract code tree root node</returns>
        public static ACT ParseStream(StreamReader stream)
        {
            return ParseAntlrStream(new AntlrInputStream(stream));
        }

        /// <summary> Parses text in string/summary>
        /// <param name="text"> Input text</param>
        /// <returns> Returns corresponding abstract code tree root node</returns>
        public static ACT ParseText(string text)
        {
            return ParseAntlrStream(new AntlrInputStream(text));
        }

        /// <summary> Computes similarity</summary>
        /// <param name="act1"> First abstract code tree</param>
        /// <param name="act2"> Second abstract code tree</param>
        /// <param name="exactMatch"> If true then only built-in functions with same name are matched</param>
        /// <returns> Returns similarity in range 0..1</returns>
        public static float ComputeSimilarity(ACT act1, ACT act2, bool exactMatch)
        {
            if (act1 != null && act2 != null)
            {
                PosetGraph posetG1 = new PosetGraph(act1);
                PosetGraph posetG2 = new PosetGraph(act2);
                posetG1.exactMatch = exactMatch;
                posetG2.exactMatch = exactMatch;
                posetG1.InitSimilarityMatrices(posetG2);
                return posetG1.GetSimilarity(posetG2);
            }
            return 0f;
        }

        /// <summary> Replaces environment names in source code</summary>
        /// <param name="source"> Initial source code</param>
        /// <param name="act1"> ACT which environment is replaced</param>
        /// <param name="act2"> ACT which environment is used for replacementt</param>
        /// <returns> Returns new source code with corresponding replacements</returns>
        public static string ReplaceEnvironment(string source, ACT act1, ACT act2)
        {
            string result = source;
            //Replace write environment
            List<KeyValuePair<string, Identifier>> env1 = act1.writeEnvironment.ToList();
            List<KeyValuePair<string, Identifier>> env2 = act2.writeEnvironment.ToList();
            int j = 0;
            for (int i=0; i<env1.Count && j<env2.Count; i++)
            {
                result = ReplaceEnvironmentString(result, env1[i].Key, env2[j].Key);
                j++;
            }
            //Replace read environment
            env1 = act1.readEnvironment.ToList();
            env2 = act2.readEnvironment.ToList();
            j = 0;
            for (int i = 0; i < env1.Count && j < env2.Count; i++)
            {
                result = ReplaceEnvironmentString(result, env1[i].Key, env2[j].Key);
                j++;
            }
            return result;
        }


        /// <summary> Replaces all words in source code with matching name</summary>
        /// <param name="source"> Initial source code</param>
        /// <param name="var"> Original variable name in environemnt</param>
        /// <param name="newvar"> Replaced variable name in environment</param>
        /// <returns> Returns new source code with corresponding replacements</returns>
        private static string ReplaceEnvironmentString(String source, String var, String newvar)
        {
            if (var.Length < 1) return source;
            String result = "";
            int start = 0;
            //While looks through all substrings
            while (source.Contains(var))
            {
                int i = source.IndexOf(var);
                result += source.Substring(start, i - start);
                //Check previous and next character and determine whether to replace or not
                char prevChar = i > 0 ? source[i-1] : ' ';
                char nextChar = i + var.Length < source.Length ? source[i + var.Length] : ' ';
                if (Char.IsLetterOrDigit(prevChar) || prevChar == '_' ||
                    Char.IsLetterOrDigit(nextChar) || nextChar == '_') {
                    result += var;
                } else {
                    result += newvar;
                }
                //Remove the beginning part from source
                if (i + var.Length < source.Length)
                {
                    source = source.Substring(i + var.Length);
                }
                else
                {
                    source = "";
                }
            }
            //Whatever is left add to the result
            result += source;
            return result;
        }

        /// <summary> Method for alternative node matching based similarity</summary>
        /// <param name="act1"> First abstract code tree</param>
        /// <param name="act2"> Second abstract code tree</param>
        /// <returns> Returns similarity in range 0 to 1</returns>
        public static float ComputeSimpleSimilarity(ACT act1, ACT act2)
        {
            if (act1 != null && act2 != null)
            {
                List<ACT> list1 = new List<ACT>();
                act1.ToNodeList(list1);
                list1 = list1.OrderBy(o => o.type).ToList();
                List<ACT> list2 = new List<ACT>();
                act2.ToNodeList(list2);
                list2 = list2.OrderBy(o => o.type).ToList();
                float maxNodeCount = Math.Max(list1.Count, list2.Count);
                float similarityCounter = 0f;

                //Matching algorithm
                int i = 0;
                int j = 0;
                while (i < list1.Count && j < list2.Count)
                {
                    ACT element = list1[i];
                    ACT element2 = list2[j];
                    //Align element2 group
                    while (element2.type < element.type && j < (list2.Count-1))
                    {
                        j++;
                        element2 = list2[j];    
                    }
                    //Match within same type
                    int jlocal = j;
                    float maxSimilarity = 0f;
                    int maxj = -1;
                    while (element2.type == element.type)
                    {
                        float similarity = NodeSimilarity(element, element2);
                        if (element2.matching == null && similarity > maxSimilarity)
                        {
                            maxSimilarity = similarity;
                            maxj = jlocal;
                        }
                        if (jlocal < (list2.Count-1))
                        {
                            jlocal++;
                            element2 = list2[jlocal];
                        }
                        else
                        {
                            break;
                        }                     
                    }
                    //All matching typed element2 have been compared
                    if (maxj >= 0)
                    {
                        element2 = list2[maxj];
                        element2.matching = element;
                        //Console.WriteLine("e:" + element2.type + "" + element2.value + "" + element.value);
                        //element.matching = element2;
                        similarityCounter += maxSimilarity;
                    }
                    i++;
                }
                Console.WriteLine(similarityCounter);
                return similarityCounter / maxNodeCount;
            }
            return 0f;
        }

        /// <summary> Two nodes similarity function for simple similarity</summary>
        /// <param name="act1"> First abstract code tree</param>
        /// <param name="act2"> Second abstract code tree</param>
        /// <returns> Returns always 1</returns>
        static float NodeSimilarity(ACT act1, ACT act2)
        {
            //More sophisticated function could be used in future
            return 1f;
        }

        /// <summary> Main method for testing application as console application</summary>
        static void Main(string[] args)
        {
            StreamReader stream = new StreamReader("testprogram.js");
            StreamReader stream2 = new StreamReader("testprogram2.js");

            ACT act = ParseStream(stream);
            ACT act2 = ParseStream(stream2);

            if (act != null && act2 != null)
            {
                PosetGraph posetG = new PosetGraph(act);
                PosetGraph posetG2 = new PosetGraph(act2);

                Console.WriteLine("ACT(1):");
                Console.WriteLine(act.ToString("", " ", "\n"));
                Console.WriteLine(posetG.ToString());

                Console.WriteLine("\nACT(2):");
                Console.WriteLine(act2.ToString("", " ", "\n"));
                Console.WriteLine(posetG2.ToString());

                posetG.InitSimilarityMatrices(posetG2);
                posetG.GetSimilarity(posetG2);

                Console.WriteLine(posetG.SimilarityMatrixToString(posetG2));
                Console.WriteLine("Similarity:" + posetG.GetSimilarity(posetG2));
                Console.WriteLine("Simple Similarity:" + ComputeSimpleSimilarity(act, act2));
            }
            Console.WriteLine("Press any key to continue!");
            Console.ReadKey();
        }

    }
}
