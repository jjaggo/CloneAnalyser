using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloneParser
{
    public class PosetGraph
    {
        //The maximum number of convergence steps to prevent cycles
        public static int maxConvergeSteps = 16;
        public int conversionCount = 0;
        public bool exactMatch { get; set; }

        public int nodeCount { get; set; }
        public byte[,] adjacencyMatrix { get; set; }
        public List<int>[] inDegreeNodeList { get; set; }
        public List<int>[] outDegreeNodeList { get; set; }

        public float[,] nodeSimilarity { get; set; }
        public float[,] outNodeSimilarity { get; set; }
        public float[,] inNodeSimilarity { get; set; }

        private KeyValuePair<string, Identifier>[] variables = null;
        private List<int>[] variableUsages = null;
        private int matrixElementNr;
        private List<ACT> matrixElements;

        /// <summary> Constructor sets up the graph</summary>
        /// <param name="act"> ACT used for constructing graph</param>
        public PosetGraph(ACT act)
        {
            variables = act.GetVariables();
            nodeCount = act.processNodeCount;
            variableUsages = new List<int>[variables.Length];
            for (int i = 0; i < variables.Length; i++)
                variableUsages[i] = new List<int>();

            adjacencyMatrix = new byte[nodeCount, nodeCount];
            inDegreeNodeList = new List<int>[nodeCount];
            outDegreeNodeList = new List<int>[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                inDegreeNodeList[i] = new List<int>();
                outDegreeNodeList[i] = new List<int>();
            }

            matrixElementNr = 0;
            matrixElements = new List<ACT>();
            ProcessACT(act, null);
        }

        /// <summary> Traverses the tree and construct's the graph</summary>
        /// <param name="act"> ACT that is getting traversed</param>
        /// <param name="parent"> previous ACT node traversed</param>
        public void ProcessACT(ACT act, ACT parent)
        {
            ACT newParent = parent;
            if (act.type != ACTtype.Program && act.type != ACTtype.Block && act.type != ACTtype.Term)
            {
                matrixElements.Add(act);
                act.matrixId = matrixElements.Count - 1;
                if (parent != null)
                {
                    inDegreeNodeList[parent.matrixId].Add(matrixElementNr);
                    outDegreeNodeList[matrixElementNr].Add(parent.matrixId);
                }
                for (int i = 0; i < variables.Length; i++)
                {
                    KeyValuePair<string, Identifier> variable = variables[i];
                    if (act.writeEnvironment.Contains(variable))
                    {
                        foreach (int usage in variableUsages[i])
                        {
                            adjacencyMatrix[usage, matrixElementNr] = 1;
                            inDegreeNodeList[usage].Add(matrixElementNr);
                            outDegreeNodeList[matrixElementNr].Add(usage);
                        }
                        //if writeEnvironment contains it then clear and add new
                        variableUsages[i].Clear();
                        variableUsages[i].Add(matrixElementNr);
                    }
                    else if (act.readEnvironment.Contains(variable))
                    {
                        foreach (int usage in variableUsages[i])
                        {
                            adjacencyMatrix[usage, matrixElementNr] = 1;
                            inDegreeNodeList[usage].Add(matrixElementNr);
                            outDegreeNodeList[matrixElementNr].Add(usage);
                        }
                        variableUsages[i].Add(matrixElementNr);
                    }
                }
                newParent = act;
                matrixElementNr++;
            }
            foreach (ACT child in act.children)
            {
                ProcessACT(child, newParent);
            }
        }

        /// <summary> Builds node index list for the graph</summary>
        /// <returns> List of integers from 0 to nodeCount</returns>
        public List<int> GetNodeList()
        {
            List<int> nodeList = new List<int>();
            for (int i = 0; i < nodeCount; i++)
            {
                nodeList.Add(i);
            }
            return nodeList;
        }

        /// <summary> Builds similarity matrix for this and other graph</summary>
        /// <param name="graph"> Other graph to use for similarity matrix</param>
        public void InitSimilarityMatrices(PosetGraph graph2)
        {
            int size1 = this.nodeCount;
            int size2 = graph2.nodeCount;
            nodeSimilarity = new float[size1, size2];
            inNodeSimilarity = new float[size1, size2];
            outNodeSimilarity = new float[size1, size2];

            for (int i=0; i<size1; i++)
            {
                for (int j = 0; j<size2; j++)
                {
                    int maxDegree = Math.Max(inDegreeNodeList[i].Count(), graph2.inDegreeNodeList[j].Count());
                    inNodeSimilarity[i, j] = maxDegree != 0 ? Math.Min(inDegreeNodeList[i].Count(), graph2.inDegreeNodeList[j].Count()) / (float)maxDegree : 0f;
                    maxDegree = Math.Max(outDegreeNodeList[i].Count(), graph2.outDegreeNodeList[j].Count());
                    outNodeSimilarity[i, j] = maxDegree != 0 ? Math.Min(outDegreeNodeList[i].Count(), graph2.outDegreeNodeList[j].Count()) / (float)maxDegree : 0f;
                    nodeSimilarity[i, j] = (inNodeSimilarity[i, j] + outNodeSimilarity[i, j]) / 2f;
                }
            }
        }

        /// <summary> Converges similarity matrix</summary>
        /// <param name="graph"> Other graph used for similarity matrix</param>
        public void measureSimilarity(PosetGraph graph2)
        {
            float epsilon = 0.000001f;
            int size1 = this.nodeCount;
            int size2 = graph2.nodeCount;

            int maxSize = Math.Max(size1, size2);
            float maxDifference = 0f;
            bool converged = false;
            int convergeStep = 0;

            while (!converged && convergeStep < maxConvergeSteps)
            {
                convergeStep++;
                maxDifference = 0f;
                for (int i=0; i<size1; i++)
                {
                    for (int j=0; j<size2; j++)
                    {
                        //If types doesn't match, the similarity is 0
                        if (matrixElements[i].type != graph2.matrixElements[j].type)
                        {
                            inNodeSimilarity[i, j] = 0f;
                            outNodeSimilarity[i, j] = 0f;
                            nodeSimilarity[i, j] = 0f;
                            continue;
                        }
                        float similaritySum = 0f;
                        int deg1 = inDegreeNodeList[i].Count();
                        int deg2 = graph2.inDegreeNodeList[j].Count();
                        int maxDegree = Math.Max(deg1, deg2);
                        int minDegree = Math.Min(deg1, deg2);

                        float inSimilarity;

                        if (maxDegree == 0)
                        {
                            inSimilarity = 1f;
                        }
                        else
                        {
                            if (deg1 <= deg2)
                            {
                                similaritySum += EnumerationFunction(inDegreeNodeList[i], graph2.inDegreeNodeList[j], 0, graph2);
                            }
                            else
                            {
                                similaritySum += EnumerationFunction(graph2.inDegreeNodeList[j], inDegreeNodeList[i], 1, graph2);
                            }
                            inSimilarity = similaritySum / (float)maxDegree;
                        }
                        inNodeSimilarity[i, j] = inSimilarity;

                        similaritySum = 0f;
                        deg1 = outDegreeNodeList[i].Count();
                        deg2 = graph2.outDegreeNodeList[j].Count();
                        maxDegree = Math.Max(deg1, deg2);
                        minDegree = Math.Min(deg1, deg2);
                        float outSimilarity;
                        if (maxDegree == 0)
                        {
                            outSimilarity = 1f;
                        }
                        else
                        {
                            if (deg1 <= deg2)
                            {
                                similaritySum += EnumerationFunction(outDegreeNodeList[i], graph2.outDegreeNodeList[j], 0, graph2);
                            }
                            else
                            {
                                similaritySum += EnumerationFunction(graph2.outDegreeNodeList[j], outDegreeNodeList[i], 1, graph2);
                            }
                            outSimilarity = similaritySum / (float)maxDegree;
                        }
                        outNodeSimilarity[i, j] = outSimilarity;

                        float similarity = (inSimilarity + outSimilarity) / 2f;
                        if (Math.Abs(nodeSimilarity[i,j] - similarity) > maxDifference)
                        {
                            maxDifference = Math.Abs(nodeSimilarity[i, j] - similarity);
                        }
                        nodeSimilarity[i, j] = similarity;
                    }
                }
                conversionCount++;
                if (maxDifference < epsilon)
                {
                    converged = true;
                }
            }
        }

        /// <summary> Enumerates similarity between two nodes</summary>
        /// <param name="nodesA"> Node A neighbour list</param>
        /// <param name="nodesB"> Node B neighbour list</param>
        /// <param name="graphNr"> Determines whether nodeA is from the current graph or vice versa</param>
        /// <param name="graph2"> The other graph</param>
        /// <returns> Returns similarity in range 0..1</returns>
        public float EnumerationFunction(List<int> nodesA, List<int> nodesB, int graphNr, PosetGraph graph2)
        {
            //If either one of the nodes doesn't have neighbours they are different
            if (nodesA.Count == 0 || nodesB.Count == 0)
                return 0f;

            float similaritySum = 0.0f;
            float[] maxValues = new float[nodesA.Count];
            int[] maxNodes = new int[nodesA.Count];
            //Find maximal similarity matching
            for (int i=0; i<nodesA.Count; i++)
            {
                int node = nodesA[i];
                float max = 0;
                int maxnode = -1;
                //Find maximal similarity node
                foreach (int node2 in nodesB)
                {
                    ACT a1 = graphNr == 0 ? matrixElements[node] : graph2.matrixElements[node];
                    ACT a2 = graphNr == 0 ? graph2.matrixElements[node2] : matrixElements[node2];
                    //If types does not match
                    if (a1.type != a2.type)
                        continue;
                    //If built in function calls does not match
                    if (exactMatch && a1.type == ACTtype.Call && (ACT.builtInFunctions.Contains(a1.value) || ACT.builtInFunctions.Contains(a2.value)) && a1.value != a2.value)
                        continue;

                    float sim = graphNr == 0 ? nodeSimilarity[node, node2] : nodeSimilarity[node2, node];
                    if (max < sim)
                    {
                        max = sim;
                        maxnode = node2;
                    }
                    max = max < sim ? sim : max;
                }
                maxValues[i] = max;
                maxNodes[i] = maxnode;
            }

            for (int i=0; i<nodesA.Count; i++)
            {
                float value = maxValues[i];
                similaritySum += value;
            }

            return similaritySum;
        }

        /// <summary> Get's similarity between this and other graph, measureSimilarity is prequisite</summary>
        /// <param name="graph2"> </param>
        /// <returns> Returns similarity from 0..1</returns>
        public float GetSimilarity(PosetGraph graph2)
        {
            //If either one of those graphs have no nodes, the similarity is 0
            if (nodeCount == 0 || graph2.nodeCount == 0)
                return 0f;

            float finalGraphSimilarity = 0f;
            conversionCount = 0;
            measureSimilarity(graph2);

            if (nodeCount < graph2.nodeCount)
            {
                finalGraphSimilarity = EnumerationFunction(GetNodeList(), graph2.GetNodeList(), 0, graph2) / graph2.nodeCount;
            }
            else
            {
                finalGraphSimilarity = EnumerationFunction(graph2.GetNodeList(), GetNodeList(), 1, graph2) / nodeCount;
            }
            return finalGraphSimilarity;
        }

        /// <summary> Converts adjaceny matrix to string</summary>
        /// <returns> Graph adjacency matrix representation</returns>
        public override string ToString()
        {
            string s = "Poset Graph nodes:" + nodeCount + "\n";
            for (int i=0; i<nodeCount; i++)
            {
                for (int j=0; j<nodeCount; j++)
                {
                    s += adjacencyMatrix[i, j];
                }
                s += "\n";
            }
            return s;
        }

        /// <summary> Converts similarity matrix to string</summary>
        /// <param name="graph2"> Other graph of this similarity matrix</param>
        /// <returns> Similarity matrix representation</returns>
        public string SimilarityMatrixToString(PosetGraph graph2)
        {
            string s = "Similarity matrix:\n";
            int size1 = this.nodeCount;
            int size2 = graph2.nodeCount;

            for (int i = 0; i < size1; i++)
            {
                for (int j = 0; j < size2; j++)
                {
                    string sa = ((int)(nodeSimilarity[i, j] * 1000f) / 1000f).ToString();
                    while (sa.Length < 6)
                    {
                        //Add 6 character gap
                        sa += " ";
                    }
                    s += sa;
                }
                s += "\n";
            }
            s += "\n";
            return s;
        }

    }
}
