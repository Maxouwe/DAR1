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

        //checks wether there are too many answers
        //we say there are too many tuples if there are k/2 >=  tuples with the same score
        //scoreColumn is the name of the column in which the score of each tuple is held
        public bool isManyTuples(string tableName, string scoreColumn)
        {
            //dictionary to keep count of each qfidf score
            Dictionary<float, int> countDict = new Dictionary<float, int>();
            
            //if there is a score in countDict that has k/2 >= counts we return true
            bool tooMany = false;
            DBManipulations.readTuples(
                String.Format(@"SELECT {0} FROM {1}", scoreColumn, tableName),
                delegate (SQLiteDataReader reader)
                {
                    float score = reader.GetFloat(reader.GetOrdinal(scoreColumn));
                    if (countDict.ContainsKey(score))
                    {
                        countDict[score]++;
                        if (countDict[score] >= _k/2) 
                        {
                            tooMany = true;
                        }
                    }
                    else
                    {
                        countDict[score] = 1;
                    }
                }
                );
            return tooMany;
        }

        //checks wether there are zeroTuples
        //we say there are zero tuples if there are less than k tuples in the answer
        public bool isZeroTuples(string tableName)
        {
            //if there is a qfidfscore in countDict that has k/2 >= counts we return true
            bool zeroTuples = false;
            DBManipulations.readTuples(
                String.Format(@"SELECT COUNT(*) FROM {0}", tableName),
                delegate (SQLiteDataReader reader)
                {
                    if (reader.GetInt32(0) < _k)
                    {
                        zeroTuples = true;
                    }
                }
                );
            return zeroTuples;
        }

        //if too many tuples use extendedqf table to break ties see section 5 of paper
        //we want that the ranking between different equivalence classes stays the same
        //but that within the same equivalence class we rerank the tuple
        //i.e. if we have 5 tuples with a qfidf similarity of 5, 5, 3, 3, 1
        //Then we want that 5 stays above 3 and 1 and 3 stays above 1 in the ranking
        //but for exmaple that the two tuples 5 and 5 are reranked with eachother
        public void rankByExtendedQF()
        {
            //first create extendedqfsum table
            //where we have id, qfidfsum, extendedqfsum
            //from extendedqf table and ordered by extendedqfsum

            //then do this
            //where id = tuple id
            //a = qfidfsum
            //b = extendedqfsum
            //A = autompg
            //B = topk table
            //C = extendedqf table
            //result = new topk

            /*CREATE TABLE result(
            id int,
            a int,
            b int
            );

            INSERT INTO result
            SELECT A.id, Q.a, Q.b FROM
            (SELECT B.a, C.b FROM B
            INNER JOIN C ON B.a = C.a) AS Q
            INNER JOIN A ON Q.a = A.a AND Q.b = A.b
            */
        }

    }
}
