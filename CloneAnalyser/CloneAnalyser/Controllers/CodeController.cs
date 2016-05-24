using CloneAnalyser.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using CloneParser;
using System.Web.Http.Description;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Entity.Validation;

namespace CloneAnalyser.Controllers
{
    /// <summary> Data structure for analysis results</summary>
    public class AnalyzeResult
    {
        public float similarity;
        public ACT inputACT;
        public ACT matchingACT;
        public CloneCluster matchingCluster;

        public AnalyzeResult(float similarity, ACT inputACT, ACT matchingACT, CloneCluster matchingCluster)
        {
            this.similarity = similarity;
            this.inputACT = inputACT;
            this.matchingACT = matchingACT;
            this.matchingCluster = matchingCluster;
        } 
    }

    public class CodeController : ApiController
    {
        private CloneAnalyserDBContextEntities db = new CloneAnalyserDBContextEntities();

        // -- ANALYSIS WORKFLOW --

        //POST: api/Code 
        /// <summary> Starts analysis workflow.</summary>
        /// <param name="inputCode"> Input data from user form</param>
        /// <returns> Returns analysis result</returns>
        [ResponseType(typeof(AnalyseResponse))]
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Code")]
        public IHttpActionResult PostCode(AnalyseInput inputCode)
        {
            ACT act = CloneParser.Program.ParseText(inputCode.Code);
            System.Diagnostics.Debug.WriteLine("Starting analysis workflow\nInput ACT:\n" + act.ToStringTree(" ", " ", "\n"));
            System.Diagnostics.Debug.WriteLine("tesst");

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            String succinct = act.ToStringTree("", "&nbsp;", "<br/>");

            Dictionary<string, ACT> dictionary = new Dictionary<string, ACT>();
            act.ExtractFunctions(dictionary);
            List<AnalyzeResult> results = new List<AnalyzeResult>();

            AnalyseFunction(act, inputCode.similarityThreshold, inputCode.exactMatch, results);

            Pattern[] patterns = new Pattern[results.Count];
            for (int i=0; i<results.Count; i++) {
                AnalyzeResult bestMatch = results[i];               
                Clone template = db.Clones.Find(bestMatch.matchingCluster.TemplateCloneId);

                string replacedSource = Program.ReplaceEnvironment(template.Source, bestMatch.matchingACT, bestMatch.inputACT);
                Pattern pattern = new Pattern(bestMatch.similarity,
                    template.Source,
                    replacedSource,
                    bestMatch.inputACT.sourceInfo.startLine,
                    bestMatch.inputACT.sourceInfo.startIndex,
                    bestMatch.inputACT.sourceInfo.endLine,
                    bestMatch.inputACT.sourceInfo.endIndex);
                patterns[i] = pattern;
            }
            AnalyseResponse inputCodeResponse = new AnalyseResponse(succinct, patterns);

            return Ok(inputCodeResponse);
        }

        /// <summary> Recursevly analyses subprogram</summary>
        /// <param name="act"> Subtree ACT</param>
        /// <param name="similarityThreshold"> User input value for similarity threshold</param>
        /// <param name="exactMatch"> If true only same buil-in function names are matched</param>
        /// <out name="results"> Accumulator list for anaysis results</param>
        private void AnalyseFunction(ACT act, float similarityThreshold, bool exactMatch, List<AnalyzeResult> results)
        {
            if (!act.isBadStartingPoint)
            {
                AnalyzeResult res = AnalyseSection(act, similarityThreshold, exactMatch);
                if (res != null)
                {
                    results.Add(res);
                }
            }
            foreach (ACT child in act.children)
            {
                if (child.nodeCount > 1)
                {
                    AnalyseFunction(child, similarityThreshold, exactMatch, results);
                }
            }
        }

        /// <summary> Analyses section of program</summary>
        /// <param name="act"> Subtree ACT</param>
        /// <param name="similarityThreshold"> User input value for similarity threshold</param>
        /// <param name="exactMatch"> If true only same buil-in function names are matched</param>
        /// <returns> Returns all analysis results for this subtree</param>
        private AnalyzeResult AnalyseSection(ACT act, float similarityThreshold, bool exactMatch)
        {
            List<AnalyzeResult> clusters = FindSimilarClusters(act, similarityThreshold, exactMatch);
            if (clusters.Count > 0)
            {
                AnalyzeResult bestResult = null;
                int bestMatch = 0;
                //Choose best matching cluster based on its clones count
                for (int i = 0; i < clusters.Count; i++)
                {
                    int count = clusters[i].matchingCluster.CloneCount;
                    if (bestMatch < count)
                    {
                        bestMatch = count;
                        bestResult = clusters[i];
                    }
                }
                return bestResult;
            }
            return null;
        }

        // -- MIGRATON WORKFLOW --

        // POST: api/MigrationFiles.
        /// <summary>  Starts migration workflow</summary>
        /// <param name="inputMigration"> Migration input data from user form</param>
        /// <returns> Returns list of migratable files</param>
        [ResponseType(typeof(StartMigrationResponse))]
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Migration")]
        public IHttpActionResult Migration(StartMigrationInput inputMigration)
        {
            System.Diagnostics.Debug.WriteLine("Migration files");
            StartMigrationResponse response = new StartMigrationResponse();
            List<long> files = new List<long>();

            using (SqlConnection connection = new SqlConnection(
                "Data Source = BETAMAX; Initial Catalog = ReposDatabase2011; MultipleActiveResultSets = True; Uid=CloneAnalyser; password=CA4jjaggo;"))
            {
                try
                {
                    connection.Open();
                    System.Diagnostics.Debug.WriteLine("BETAMAX connection");

                    //Select all javascript files from database
                    string codeQuery = "SELECT MAX(RevisionId) " +
                        "FROM dbo.VCSTextFileRevision " +
                        "INNER JOIN dbo.VCSFileRevision ON VCSTextFileRevision.RevisionId = VCSFileRevision.Id " +
                        "WHERE (VCSFileRevision.ExtensionId = 238) AND VCSTextFileRevision.ContentsU <> '' AND VCSTextFileRevision.LinesOfCode > 1 " +
                        "GROUP BY FileId;"; 

                    using (SqlCommand command = new SqlCommand(codeQuery, connection))
                    using (SqlDataReader reader = command.ExecuteReader())
                    { 
                        while (reader.Read())
                        {
                            files.Add(reader.GetInt64(0));
                        }
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("Exception +- " + e.GetType() + " " + e.Message);
                    return this.BadRequest();
                }
            }
            RemoveAllData();

            response.files = files.ToArray();
            return Ok(response);
        }


        // POST: api/Migrate
        /// <summary> Starts single file migration</summary
        /// <param name="inputMigration"> Migratable file id and user data from input form</param>
        /// <returns> If migration was successful or not</returns>
        [ResponseType(typeof(MigrateResponse))]
        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/Migrate")]
        public IHttpActionResult MigrateCode(MigrateInput inputMigrate)
        {
            System.Diagnostics.Debug.WriteLine("Starting code migration from BETAMAX, clusterSimilarity: " + inputMigrate.file);
            int minTreeSize = inputMigrate.minTreeSize;
            int count = 0;
            int charCount = 0;
            int nodeCount = 0;

            using (SqlConnection connection = new SqlConnection(
                "Data Source = BETAMAX; Initial Catalog = ReposDatabase2011; MultipleActiveResultSets = True; Uid=CloneAnalyser; password=CA4jjaggo;"))
            {
                try
                {
                    connection.Open();
                    System.Diagnostics.Debug.WriteLine("BETAMAX connection");

                    //Select file data with given ID
                    string codeQuery = "SELECT RevisionId, FileId, LinesOfCode, Date, Comment, Alias, ContentsU " +
                    "FROM dbo.VCSTextFileRevision " +
                    "INNER JOIN dbo.VCSFileRevision ON VCSTextFileRevision.RevisionId = VCSFileRevision.Id " +
                    "WHERE VCSTextFileRevision.RevisionId = " + inputMigrate.file + ";"; 

                    using (SqlCommand command = new SqlCommand(codeQuery, connection))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try {
                                string code = reader.GetString(6);
                                charCount = code.Length;

                                ACT act = CloneParser.Program.ParseText(code);
                                nodeCount = act.nodeCount;
                                
                                MigrateFunction(act, inputMigrate.similarityThreshold, minTreeSize, inputMigrate.exactMatch);
                            }
                            catch (System.NullReferenceException)
                            {
                                System.Diagnostics.Debug.WriteLine("NullReference exception");
                            }
                            catch (System.ArgumentException)
                            {
                                System.Diagnostics.Debug.WriteLine("Argument exception");
                            }
                            finally
                            {
                                count++;
                            }
                        }
                    }
                }
                catch (DbEntityValidationException e)
                {
                    System.Diagnostics.Debug.WriteLine("Validation Exception +- " + e.GetType() + " " + e.Message);
                    return this.BadRequest();
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("Exception +- " + e.GetType() + " " + e.Message);
                    return this.BadRequest();
                }

            }

            MigrateResponse response = new MigrateResponse();
            response.charCount = charCount;
            response.nodeCount = nodeCount;
            response.dbCloneCount = db.Clones.Count();
            response.dbClusterCount = db.CloneClusters.Count();

            return Ok(response);
        }

        /// <summary> Clears recommencation model</summary>
        private void RemoveAllData()
        {
            db.Database.ExecuteSqlCommand("delete from Clone");
            db.Database.ExecuteSqlCommand("delete from CloneCluster");
        }

        /// <summary> Recursevly migrates subprogram</summary>
        /// <param name="act"> Subtree ACT</param>
        /// <param name="similarityThreshold"> User input value for similarity threshold</param>
        /// <param name="minTreeSize"> User input value for minimal tree size to be migrated</param>
        /// <param name="exactMatch"> If true only same buil-in function names are matched</param>
        private void MigrateFunction(ACT act, float similarityThreshold, int minTreeSize, bool exactMatch)
        {
            //act.type != ACTtype.Function &&
            if (act.children.Count != 0 && act.nodeCount >= minTreeSize) {
                if (!act.isBadStartingPoint)
                    MigrateSection(act, null, similarityThreshold, exactMatch);
                foreach (ACT child in act.children)
                {
                    if (child.nodeCount > 1)
                    {
                        MigrateFunction(child, similarityThreshold, minTreeSize, exactMatch);
                    }
                }
            }
        }

        /// <summary> Finds all similar clusters for given subrree</summary>
        /// <param name="act"> Subtree ACT</param>
        /// <param name="similarityThreshold"> User input value for similarity threshold</param>
        /// <param name="exactMatch"> If true only same buil-in function names are matched</param>
        private List<AnalyzeResult> FindSimilarClusters(ACT act, float similarityThreshold, bool exactMatch)
        {
            var list = new List<AnalyzeResult>();
            CloneCluster cluster = null;
            foreach (var cloneCluster in db.CloneClusters)
            {
                //cloneCluster.TemplateCloneId
                Clone template = db.Clones.Find(cloneCluster.TemplateCloneId);
               
                if (template != null)
                {
                    //Skip pairs with too big node count difference
                    int nodes1 = act.nodeCount;
                    int nodes2 = (int)template.Nodes;
                    if (nodes1 == 0 || nodes2 == 0 || Math.Max(nodes1, nodes2) / Math.Min(nodes1, nodes2) < similarityThreshold)
                    {
                        continue;
                    }
                    //Start processing the template ACT
                    ACT templateACT = ACT.FromSuccinct(template.ACT);
                    templateACT.PostProcess();

                    //System.Diagnostics.Debug.WriteLine("Checking similarity:\n  " + act.ToStringTree("  ", " ", "\n") + "\n  " + templateACT.ToStringTree("  ", " ", "\n"));
                    try {
                        float sim = 0f;

                        sim = CloneParser.Program.ComputeSimilarity(templateACT, act, exactMatch);                  

                        if (sim > similarityThreshold)
                        {
                            cluster = cloneCluster;
                            list.Add(new AnalyzeResult(sim, act, templateACT, cluster));
                        }

                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine("Exception: " + e);
                    }
                }
            }
            //Order list by similarity
            list.OrderByDescending(o => o.similarity);

            return list;
        }

        /// <summary> Analyses section of program</summary>
        /// <param name="act"> Subtree ACT</param>
        /// <param name="parent"> Parent node for given subtree</param>
        /// <param name="similarityThreshold"> User input value for similarity threshold</param>
        /// <param name="exactMatch"> If true only same buil-in function names are matched</param>
        /// <returns> Returns all analysis results for this subtree</param>
        private void MigrateSection(ACT act, ACT parent, float similarityThreshold, bool exactMatch)
        {
            List<AnalyzeResult> clusters;
            try {
                clusters = FindSimilarClusters(act, similarityThreshold, exactMatch);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Exception while migrating section: " + e);
                return;
            }
            
            CloneCluster cluster = null;
            bool setTemplate = false;
            if (clusters.Count > 0)
            {
                //Add result to existing cluster
                AnalyzeResult res = clusters[0];
                cluster = res.matchingCluster;
            } else {
                //Add new cluster
                cluster = new CloneCluster();
                db.CloneClusters.Add(cluster);
                setTemplate = true;
            }

            try { db.SaveChanges(); }
            catch (DbUpdateException) { throw; }

            //Save clone
            Clone clone = new Clone();
            clone.Nodes = act.processNodeCount;
            clone.Source = act.sourceInfo.source;

            clone.ACT = act.ToSuccinct();
            clone.CloneClusterId = cluster.Id;
            cluster.CloneCount++;
            db.Clones.Add(clone);

            try { db.SaveChanges(); }
            catch (DbUpdateException) { throw; }

            //Add new cloneCluster template
            if (setTemplate)
            {
                cluster.TemplateCloneId = clone.Id;
                try { db.SaveChanges(); }
                catch (DbUpdateException) { throw; }
            }
        }
    }
}
