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

        
        public void rankByQFIDF()
        {
            _simTable.createQFIDFSimilarityTable();
            using (SQLiteConnection connection = new SQLiteConnection(DBManipulations.connectionString))
            {
                connection.Open();
                DBManipulations.executeSQLNoConnection(connection,
                @"CREATE TABLE topKTemp(
                    id int,
                    qfidfsum real,
                    PRIMARY KEY (id))
                ");

                DBManipulations.readTuplesNoConnection(
                    connection,
                    @"SELECT * FROM qfidfsimilarity",
                    delegate (SQLiteDataReader reader)
                    {
                        float qfidfsum = 0;
                        for (int i = 1; i < 12; i++)
                        {
                            qfidfsum += reader.GetFloat(i);
                        }
                        DBManipulations.executeSQLNoConnection(
                            connection,
                            String.Format(@"INSERT INTO topKTemp VALUES ({0}, {1})", reader.GetInt32(0), qfidfsum)
                            );
                    }
                    );
                DBManipulations.executeSQLNoConnection(connection,
                @"CREATE TABLE topK(
                    id int,
                    qfidfsum real,
                    PRIMARY KEY (id))
                ");

                DBManipulations.readTuplesNoConnection(
                    connection,
                    String.Format(@"SELECT * FROM topKTemp ORDER BY qfidfsum DESC"),
                    delegate (SQLiteDataReader reader)
                    {

                        DBManipulations.executeSQLNoConnection(
                            connection,
                            String.Format(@"INSERT INTO topK VALUES ({0}, {1})", reader.GetInt32(0), reader.GetFloat(1))
                            );
                    }
                    );

                DBManipulations.executeSQLNoConnection(connection, "DROP TABLE topKTemp");
                connection.Close();
            }

        }

        //if there are too many tuples i.e. there are alot of ties in qfidfsimilarity score
        //then we do additional ranking by qf score of the missing attributes see section 5 of paper
        //the new topk is ranked by qfidfsimilarity 
        //and if the qfidfsimilarity is the same then rank by qf score of missing attributes
        public void rankByExtendedQF()
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
                    @"SELECT * FROM topK INNER JOIN extendedqfsum ON extendedqfsum.id = topK.id",
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

                DBManipulations.executeSQLNoConnection(connection, @"DROP TABLE topK");
                DBManipulations.executeSQLNoConnection(connection,
                @"CREATE TABLE topK(
                    id int,
                    qfidfsum real,
                    extendedqfsum real,
                    PRIMARY KEY (id))
                ");


                //first order by qfidfsum and if qfidfsum is the same then order by extendedqfsum
                DBManipulations.readTuplesNoConnection(connection,
                    String.Format(@"SELECT * FROM topKTemp1 ORDER BY qfidfsum DESC, extendedqfsum DESC LIMIT {0}", _k),
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
