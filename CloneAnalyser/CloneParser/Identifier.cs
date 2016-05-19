using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloneParser
{
    public enum IdentifierType
    {
        Const,
        Var,
        Func
    }

    public class Identifier
    {
        public string name { get; set; }
        public int mapping { get; set; }
        public IdentifierType type { get; set; }

        public Identifier(string name, IdentifierType type)
        {
            if (name.Length <= 0)
                return;

            this.name = name;
            this.type = IdentifierType.Const;
            int firstCode = (int)(name.ToLower()[0]);
            /*if (firstCode == 36 || firstCode == 95 || firstCode >= 61 && firstCode <= 122) {
                this.type = IdentifierType.Func;
            }*/
            this.type = type;
        }
        
        public void SetType(IdentifierType type)
        {
            this.type = type;
        }

        public override string ToString()
        {
            char typeChar = type.ToString().ToLower()[0];
            //return typeChar + "_" + name + ":" + mapping;
            return typeChar + "_" + name;
            //return name;
        }
    }
}
