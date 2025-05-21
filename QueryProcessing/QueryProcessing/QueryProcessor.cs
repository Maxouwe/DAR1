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
        private SimilarityScoreTable _simTable;
        
        public QueryProcessor(List<Attribute> query, int k) 
        {
            _k = k;
            _query = query;
            _simTable = new SimilarityScoreTable(query);
        }

        //checks wether there are too many answers
        //we say there are too many tuples if there are threshold >=  tuples with the same score
        //scoreColumn is the name of the column in which the score of each tuple is held
        public bool isManyTuples(string tableName, string scoreColumn, int threshold)
        {
            //dictionary to keep count of each qfidf score
            Dictionary<float, int> countDict = new Dictionary<float, int>();
            
            //if there is a score in countDict that has threshold >= counts we return true
            bool tooMany = false;
            DBManipulations.readTuples(
                String.Format(@"SELECT {0} FROM {1}", scoreColumn, tableName),
                delegate (SQLiteDataReader reader)
                {
                    float score = reader.GetFloat(reader.GetOrdinal(scoreColumn));
                    if (countDict.ContainsKey(score))
                    {
                        countDict[score]++;
                        if (countDict[score] >= threshold) 
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


        
        //if there are too many tuples i.e. there are alot of ties in qfidfsimilarity score
        //then we do additional ranking by qf score of the missing attributes see section 5 of paper
        //the new topk is ranked by qfidfsimilarity 
        //and if the qfidfsimilarity is the same then rank by qf score of missing attributes
        public void rankByExtendedQF(string topKtable)
        {
            _simTable.createExtendedQFTable();

            using (SQLiteConnection connection = new SQLiteConnection(DBManipulations.connectionString))
            {
                connection.Open();

                DBManipulations.executeSQLNoConnection(connection,
                @"CREATE TABLE IF NOT EXISTS topKTemp1(
                    id int,
                    qfidfsum real,
                    extendedqfsum real,
                    PRIMARY KEY (id))
                ");

                DBManipulations.readTuplesNoConnection(connection,
                    String.Format(@"SELECT * FROM {0} INNER JOIN extendedqfsum ON extendedqfsum.id = {0}.id", topKtable),
                    delegate (SQLiteDataReader reader)
                    {
                        DBManipulations.executeSQLNoConnection(connection,
                            String.Format(@"INSERT INTO topKTemp1 VALUES ({0}, {1}, {2})",
                            reader.GetInt32(reader.GetOrdinal("id")),
                            reader.GetFloat(reader.GetOrdinal("qfidfsum")),
                            reader.GetFloat(reader.GetOrdinal("extendedqfsum"))
                            ));
                    }
                    );

                DBManipulations.executeSQLNoConnection(connection, @"DROP TABLE " + topKtable);
                DBManipulations.executeSQLNoConnection(connection,
                @"CREATE TABLE topK(
                    id int,
                    qfidfsum real,
                    extendedqfsum real,
                    PRIMARY KEY (id))
                ");


                //first order by qfidfsum and if qfidfsum is the same then order by extendedqfsum
                DBManipulations.readTuplesNoConnection(connection,
                    String.Format(@"SELECT * FROM topKTemp1 ORDER BY qfidfsum DESC, extendedqfsum DESC"),
                    delegate (SQLiteDataReader reader)
                    {
                        DBManipulations.executeSQLNoConnection(connection,
                            String.Format(@"INSERT INTO topK VALUES ({0}, {1}, {2})",
                            reader.GetInt32(reader.GetOrdinal("id")),
                            reader.GetFloat(reader.GetOrdinal("qfidfsum")),
                            reader.GetFloat(reader.GetOrdinal("extendedqfsum"))
                            ));
                    }
                    );

                
                DBManipulations.executeSQLNoConnection(connection, @"DROP TABLE topKTemp1");
                


                connection.Close();
            }
        }



    }
}
