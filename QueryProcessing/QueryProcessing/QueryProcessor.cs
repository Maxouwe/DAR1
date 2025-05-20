using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.SQLite;
using System.Data.SqlClient;


namespace QueryProcessing
{

    class QueryProcessor
    {
        private List<Attribute> _query;
        private int _k;

        public QueryProcessor(List<Attribute> query, int k) 
        {
            _k = k;
            _query = query;
        }
       
        public void CalculateTopK()
        {
            _similarityTable.createQFIDFSimilarityTable();

            //New table for keeping track of the scores 
            DBManipulations.executeSQL(@"DROP TABLE IF EXISTS attribute_scores");
            DBManipulations.executeSQL(@"CREATE TABLE attribute_scores (
        attribute_name TEXT PRIMARY KEY,
        total_score REAL
    )");

            //Builds the SQL querie for every attribute needed
            List<string> queryParts = new List<string>();
            foreach (var attr in _query)
            {
                string part = $"SELECT '{attr.attributeName}' as attribute_name, " +
                             $"SUM({attr.attributeName}qfidfsim) as total_score " +
                             "FROM qfidfsimilarity";
                queryParts.Add(part);
            }

            string attributeScoresQuery = string.Join("\nUNION ALL\n", queryParts) +
                                "\nORDER BY total_score DESC";

            DBManipulations.executeSQL(attributeScoresQuery);

            //Top-k table
            DBManipulations.executeSQL(@"DROP TABLE IF EXISTS topk");
            DBManipulations.executeSQL(@"CREATE TABLE topk (
        id INTEGER PRIMARY KEY,
        total_score REAL
    )");

            //Summing up all the scores
            List<string> sumParts = new List<string>();
            foreach (var attr in _query)
            {
                sumParts.Add($"{attr.attributeName}qfidfsim");
            }
            string sumExpression = string.Join(" + ", sumParts);

            //Top-k 
            string topKQuery = $@"
        INSERT INTO topk (id, total_score)
        SELECT id, ({sumExpression}) as total_score
        FROM qfidfsimilarity
        ORDER BY total_score DESC
        LIMIT {_k}";

            DBManipulations.executeSQL(topKQuery);
        }
    }
}