using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloneParser
{
    public class VariablePartition
    {
        public KeyValuePair<string, Identifier> variable { get; set; }
        public List<List<ACT>> partitionList { get; set; }
        public List<int> variableMap { get; set; }

        private bool requiresNewList = true;

        public VariablePartition(KeyValuePair<string, Identifier> variable)
        {
            this.variable = variable;
            partitionList = new List<List<ACT>>();
            variableMap = new List<int>();
        }

        public void AddWriteACT(ACT act, bool sequential)
        {
            partitionList.Add(new List<ACT>(1));
            variableMap.Add(partitionList.Count - 1);
            partitionList[partitionList.Count - 1].Add(act);
            requiresNewList = true;
        }

        public void AddReadACT(ACT act, bool sequential)
        {
            if (requiresNewList || !sequential)
            {
                partitionList.Add(new List<ACT>(1));
                requiresNewList = !sequential;
            }
            variableMap.Add(partitionList.Count - 1);
            partitionList[partitionList.Count-1].Add(act);
        }

        public override string ToString()
        {
            string result = "";
            for (int j = 0; j < partitionList.Count; j++)
            {
                List<ACT> list = partitionList[j];
                if (list.Count == 1)
                {
                    result += " " + list[0].number.ToString();
                }
                else
                {
                    result += " (";
                    int i = 0;
                    foreach (ACT act in list)
                    {
                        if (i++ > 0) result += " ";
                        result += act.number.ToString();
                    }
                    result += ")";
                }
            }
            return result;
        }
    }
}
