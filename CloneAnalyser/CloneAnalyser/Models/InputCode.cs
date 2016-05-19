using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CloneAnalyser.Models
{

    public class Pattern
    {
        public float similarity { get; set; }
        public string source { get; set; }
        public string replacedEnvironmentSource { get; set; }
        public int startLine { get; set; }
        public int startPos { get; set; }
        public int endLine { get; set; }
        public int endPos { get; set; }

        public Pattern(float similarity, string source, string replacedEnvironmentSource, int startLine, int startPos, int endLine, int endPos)
        {
            this.similarity = similarity;
            this.source = source;
            this.replacedEnvironmentSource = replacedEnvironmentSource;
            this.startLine = startLine;
            this.startPos = startPos;
            this.endLine = endLine;
            this.endPos = endPos;
        }
    }

    public class AnalyseInput
    {
        public string Code { get; set; }
        public float similarityThreshold { get; set; }
        public bool exactMatch;
    }

    public class AnalyseResponse
    {
        public string ACT { get; set; }
        public Pattern[] patterns;

        public AnalyseResponse(string ACT, Pattern[] patterns)
        {
            this.ACT = ACT;
            this.patterns = patterns;
        }
    }

    public class StartMigrationInput
    {
        public int maxFilesCount { get; set; }
    }

    public class StartMigrationResponse
    {
        public long[] files;
    }

    public class MigrateInput
    {
        public long file { get; set; }
        public float similarityThreshold { get; set; }
        public int minTreeSize { get; set; }
        public bool exactMatch { get; set; }
    }

    public class MigrateResponse
    {
        public int charCount { get; set; }
        public int nodeCount { get; set; }
        public int dbCloneCount { get; set; }
        public int dbClusterCount { get; set; }
    }
}