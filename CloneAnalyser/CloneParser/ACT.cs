using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloneParser
{
    public enum ACTtype
    {
        //Structureal nodes
        Program = 48,
        Block = 49,
        Term = 50,

        //Process nodes
        If = 51,
        While = 52,
        Assign = 53,
        Call = 54,
        TryCatch = 55,
        Return = 56,
        Throw = 57,
        Continue = 61,
        Break = 62,
        Label = 63,

        //Temporary nodes
        Seq = 64,
        Ident = 65,
        Empty = 66,

        //function declaration
        Function = 67
    }

    public struct SourceInfo
    {
        public int startLine;
        public int startIndex;
        public int endLine;
        public int endIndex;
        public String source { get; private set; }

        public SourceInfo(String source, int startLine, int startIndex, int endLine, int endIndex)
        {
            this.source = source;
            this.startLine = startLine;
            this.startIndex = startIndex;
            this.endLine = endLine;
            this.endIndex = endIndex;
        }
    }

    public class ACT
    {
        public ACTtype type { get; set; }
        public List<ACT> children { get; private set; }
        public ACT parent { get; set; }
        public string value { get; set; }
        public int number { get; set; }
        public Dictionary<string, Identifier> readEnvironment { get; private set; }
        public Dictionary<string, Identifier> writeEnvironment { get; private set; }
        public List<Identifier> variables { get; set; }
        public int nodeCount { get; set; }
        public int processNodeCount { get; set; }
        public Dictionary<String, ACT> functionMap { get; private set; }
        public SourceInfo sourceInfo { get; private set; }
        public int matrixId { get; set; }
        public ACT matching { get; set; } //Used only in simple similarity
        public bool isBadStartingPoint { get; set; } //Bad tree for using as analysis startpoint

        //predefined
        public static ACT undefinedInitializer { get; private set; }
        public static ACTtype[] sequential { get; private set; }
        public static ACTtype[] hasValue { get; private set; }
        public static SourceInfo emptySourceInfo = new SourceInfo("", 0, 0, 0, 0);
        public static HashSet<String> builtInFunctions { get; set; }

        static ACT()
        {
            undefinedInitializer = new ACT(emptySourceInfo, ACTtype.Term);
            undefinedInitializer.value = "undefined";
            sequential = new ACTtype[] { ACTtype.Program, ACTtype.Block };
            hasValue = new ACTtype[] { ACTtype.Term, ACTtype.Call, ACTtype.Continue, ACTtype.Break, ACTtype.Function };

            String[] functions = { "[]", ".", "new", "delete", "void", "typeof", "in", "instanceof",
                "^", "~", "|", "||", "&", "&&", "<", "<<", ">", ">>", ">>>", "!>", "!<", "==", "===", "!=", "!==", "<=", ">=",
                "+", "-", "*", "/", "%", "!"};
            builtInFunctions = new HashSet<string>(functions);
        }

        public ACT(SourceInfo sourceInfo, ACTtype type, string value) : this(sourceInfo, type)
        {
            this.value = value;
        }

        public ACT(SourceInfo source, ACTtype type)
        {
            isBadStartingPoint = false;
            this.type = type;
            this.sourceInfo = source;
            this.sourceInfo = sourceInfo;
            children = new List<ACT>();
            readEnvironment = new Dictionary<string, Identifier>();
            writeEnvironment = new Dictionary<string, Identifier>();
            variables = new List<Identifier>();
            matching = null;
        }

        public KeyValuePair<string, Identifier>[] GetVariables()
        {
            KeyValuePair<string, Identifier>[] variables = new KeyValuePair<string, Identifier>[writeEnvironment.Count];
            int i = 0;
            foreach (KeyValuePair<string, Identifier> pair in writeEnvironment)
            {
                variables[i] = pair;
                i++;
            }
            return variables;
        }

        public void AddChildBlock(ACT child, bool isBadStartingPoint = false)
        {
            if (child == null)
            {
                Console.WriteLine("Warning! Translator tries to add a null children!");
                return;
            }
            ACT block = new ACT(child.sourceInfo, ACTtype.Block);
            block.isBadStartingPoint = isBadStartingPoint;

            block.parent = this;
            children.Add(block);
            block.AddChild(child);
        }

        public void AddChild(ACT child)
        {
            if (child == null)
            {
                Console.WriteLine("Warning! Translator tries to add a null children!");
                return;
            }
            
            switch (child.type)
            {
                case ACTtype.Seq:
                    foreach (ACT subChild in child.children)
                    {
                        subChild.parent = this;
                        AddChild(subChild);
                    }
                    break;
                case ACTtype.Empty:
                    return;
                default:
                    child.parent = this;
                    children.Add(child);
                    break;
            }
        }

        public void PostProcess()
        {
            OptimizeBlocks();
            Dictionary<string, Identifier> rootEnvironment = new Dictionary<string, Identifier>();
            int rootNum = 0;
            ResolveEnvironment(rootEnvironment, rootNum);

            //Dictionary<string, ACT> functionMap = new Dictionary<string, ACT>();
            //ExtractFunctions(functionMap);
        }

        private void OptimizeBlocks()
        {
            for (int i = 0; i < children.Count; i++)
            {
                ACT child = children[i];
                if (child != null)
                {
                    child.OptimizeBlocks();

                    if (child.type == ACTtype.Block)
                    {
                        if (child.children.Count == 1)
                        {
                            children[i] = child.children[0];
                            continue;
                        }
                        if (sequential.Contains(this.type))
                        {
                            children.RemoveAt(i);
                            foreach (ACT subChild in child.children)
                            {
                                children.Insert(i++, subChild);
                            }
                            i--;
                            continue;
                        }
                    }
                }
            }
        }

        public void ExtractFunctions(Dictionary<string, ACT> functionMap)
        {
            //Add dictionary reference to all nodes
            this.functionMap = functionMap;
            for (int i = 0; i < children.Count; i++)
            {
                ACT child = children[i];
                if (child != null)
                {
                    child.ExtractFunctions(functionMap);
                    if (child.type == ACTtype.Function)
                    {
                        functionMap.Add(child.value, child);

                        //Remove function declaration body
                        //children[i] = new ACT(child.sourceInfo, ACTtype.Function, child.value);
                    }
                }
            }
        }

        private int ResolveEnvironment(Dictionary<string, Identifier> rootEnvironment, int rootNumber)
        {
            this.number = rootNumber;
            this.nodeCount = 1;
            this.processNodeCount = 0;
            if (type != ACTtype.Block && type != ACTtype.Program && type != ACTtype.Term)
                this.processNodeCount = 1;

            for (int i = 0; i < children.Count; i++)
            {
                ACT child = children[i];
                rootNumber = child.ResolveEnvironment(rootEnvironment, rootNumber + 1);

                /*if (child.type != ACTtype.Function)
                {
                    nodeCount += child.nodeCount;
                    processNodeCount += child.processNodeCount;
                }
                else
                {
                    nodeCount++;
                    processNodeCount++;
                }*/
                nodeCount += child.nodeCount;
                processNodeCount += child.processNodeCount;

                //Assignment changes the value of it's first child
                if (type == ACTtype.Assign && i == 0)
                {
                    this.AddEnvironment(writeEnvironment, rootEnvironment, child.value, IdentifierType.Var);
                }
                //Term is reading the value of constant, variable or function
                else if (child.type == ACTtype.Term)
                {
                    this.AddEnvironment(readEnvironment, rootEnvironment, child.value, IdentifierType.Const);
                }
                else
                {
                    //All other nodes are propagating it's environment to the parent unless they are function declarations
                    /*if (child.type != ACTtype.Function)
                    {*/
                        this.MergeEnvironments(writeEnvironment, child.writeEnvironment);
                        this.MergeEnvironments(readEnvironment, child.readEnvironment);
                    //}
                }
            }
            UpdateVariables();
            return rootNumber;
        }

        private void UpdateVariables()
        {
            foreach (KeyValuePair<string, Identifier> pair in readEnvironment)
            {
                if (pair.Value.type == IdentifierType.Var)
                    variables.Add(pair.Value);
            }
            foreach (KeyValuePair<string, Identifier> pair in writeEnvironment)
            {
                if (pair.Value.type == IdentifierType.Var && !readEnvironment.ContainsKey(pair.Key))
                    variables.Add(pair.Value);
            }
        }

        // Adds all unique elements from e2 into e1
        public void MergeEnvironments(Dictionary<string, Identifier> e1, Dictionary<string, Identifier> e2)
        {
            foreach (KeyValuePair<string, Identifier> pair in e2)
            {
                if (!e1.ContainsKey(pair.Key))
                {
                    e1.Add(pair.Key, pair.Value);
                }
            }
        }
        // Adds string into e1 if unique
        public void AddEnvironment(Dictionary<string, Identifier> e1, Dictionary<string, Identifier> rootEnvironment, string s, IdentifierType type)
        {
            if (!e1.ContainsKey(s))
            {
                Identifier ident;
                if (rootEnvironment != null && rootEnvironment.ContainsKey(s))
                {
                    ident = rootEnvironment[s];
                    if (type == IdentifierType.Var && ident.type == IdentifierType.Const)
                        ident.type = type;
                }
                else
                {
                    ident = new Identifier(s, type);
                    if (rootEnvironment != null)
                        rootEnvironment.Add(s, ident);
                }
                e1.Add(s, ident);
            }
        }

        public String EnvironmentString()
        {
            String ret = "[";
            int i = 0;
            foreach (KeyValuePair<string, Identifier> pair in readEnvironment)
            {
                ret += pair.Value.ToString();
                if (i < readEnvironment.Count - 1)
                    ret += ",";
                i++;
            }
            ret += "|";
            i = 0;
            foreach (KeyValuePair<string, Identifier> pair in writeEnvironment)
            {
                ret += pair.Value.ToString();
                if (i < writeEnvironment.Count - 1)
                    ret += ",";
                i++;
            }
            ret += "]";
            return ret;
        }

        private String NumberToString()
        {
            if (number < 10)
                return number.ToString() + " ";
            return number.ToString();
        }

        public static ACT FromSuccinct(String succinct)
        {
            int len = succinct.Length;
            Stack<ACT> stack = new Stack<ACT>();
            ACT parent = null;
            ACT last = null;
            ACT first = null;

            int i = 0;
            while (i < len)
            {
                char c = succinct[i];
                if (c == '(')
                {
                    stack.Push(parent);
                    parent = last;
                }
                else if (c == ')')
                {
                    if (stack.Count > 0)
                    {
                        parent = stack.Pop();
                    }
                    else
                    {
                        break;
                    }
                }
                else if (c == ';')
                {
                    //skip
                }
                else
                {
                    if (Enum.IsDefined(typeof(ACTtype), (int)c)) {
                        ACTtype type = (ACTtype)c;
                        ACT act = null;
                        //if (type == ACTtype.Term)
                        if (Array.Exists(hasValue, element => element.Equals(type)))
                        {
                            string s = "";

                            while (i < len-1)
                            {
                                i++;
                                c = succinct[i];
                                if (c == '(' || c == ')' || c == ';')
                                {
                                    i--; //allow parsing the symbol
                                    break;
                                } 
                                s += c;
                            }
                            act = new ACT(emptySourceInfo, type, s);
                        }
                        else
                        {
                            act = new ACT(emptySourceInfo, type);
                        }

                        last = act;

                        if (parent != null)
                        {
                            parent.AddChild(act);
                        }

                        if (first == null)
                        {
                            first = act;
                            parent = act;
                        }
                    }
                }
                i++;
            }

            if (first == null)
                return new ACT(emptySourceInfo, ACTtype.Program);
            return first;
        }

        public String ToSuccinct()
        {
            String result = "";
            //if (type == ACTtype.Term)
            if (Array.Exists(hasValue, element => element.Equals(this.type)))
            {
                result += (Convert.ToChar(type)) + value;
            }
            else
            {
                result += (Convert.ToChar(type));
            }

            int cn = children.Count;
            if (cn > 0)
            {
                result += "(";
            }
            for (int i = 0; i < cn; i++)
            {
                ACT child = children[i];

                if (child != null)
                {
                    result += child.ToSuccinct();
                    if (i < cn - 1 && Array.Exists(hasValue, element => element.Equals(child.type)))
                    {
                        result += ";";
                    }
                }
            }
            if (cn > 0)
            {
                result += ")";
            }

            return result;
        }

        public void ToNodeList(List<ACT> list)
        {
            int cn = children.Count;
            if (cn > 0)
            {
                for (int i = 0; i < cn; i++)
                {
                    ACT child = children[i];
                    if (child != null)
                    {
                        list.Add(child);
                        child.ToNodeList(list);
                    }
                }
            }
        }

        public String ToString(string indent, string space, string lineBreak)
        {
            String result = ToStringTree(indent, space, lineBreak);

            if (this.functionMap != null)
            {
                foreach (KeyValuePair<String, ACT> pair in this.functionMap)
                {
                    result += lineBreak + lineBreak + "Function: " + pair.Key + lineBreak;
                    result += pair.Value.ToStringTree(indent, space, lineBreak);
                }
            }

            return result;
        }

        public String ToStringTree(string indent, string space, string lineBreak)
        {
            String result = NumberToString() + indent;
            if (type == ACTtype.Term)
            {
                result += value;
            }
            else
            {
                result += type.ToString() + space + value + space + EnvironmentString();
            }

            int cn = children.Count;

            if (cn > 0)
            {
                indent += space + space;
                for (int i = 0; i < cn; i++)
                {
                    ACT child = children[i];
                    if (child != null)
                    {
                        result += lineBreak + child.ToStringTree(indent, space, lineBreak);
                    }
                }
            }

            return result;
        }

        public bool IsEqual(ACT other)
        {
            if (type != other.type)
            {
                return false;
            }
            return true;
        }

        public ACT ListGetEqual(List<ACT> list)
        {
            foreach (ACT act in list)
            {
                if (IsEqual(act))
                    return act;
            }
            return null;
        }
    }
}
