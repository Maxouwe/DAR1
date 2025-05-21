using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace QueryProcessing
{
    //this class pertains to the S(t, q) scores of all tuples with respect to queryvalues of the query
    internal class SimilarityScoreTable
    {
        public Dictionary<string, Attribute> terms { get; }
        public SimilarityScoreTable(List<Attribute> terms) 
        {
            this.terms = new Dictionary<string, Attribute>();
            foreach(Attribute a in terms)
            {
                this.terms[a.attributeName] = a;
            }
        }

        //creates a table on how each tuple scores in QFIDF similarity with the query
        public void createQFIDFSimilarityTable()
        {
            DBManipulations.executeSQL(@"CREATE TABLE qfidfsimilarity (
                                            id integer NOT NULL,
                                            mpgqfidfsim real,
                                            cylindersqfidfsim real,
                                            displacementqfidfsim real,
                                            horsepowerqfidfsim real,
                                            weightqfidfsim real,
                                            accelerationqfidfsim real,
                                            model_yearqfidfsim real,
                                            originqfidfsim real,
                                            brandqfidfsim real,
                                            modelqfidfsim real,
                                            typeqfidfsim real,
                                            PRIMARY KEY (id))"
            );

            //cant use readtuples or executeSQL here
            using (SQLiteConnection connection = new SQLiteConnection(DBManipulations.connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"SELECT * FROM autompg";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        //for each found tuple perform the callback function f
                        while (reader.Read())
                        {
                            //store current tuple and qfsimilarity info
                            int id = reader.GetInt32(0);
                            float[] qfidfsims = new float[11];

                            //for each integer/float attribute
                            for (int i = 1; i < 9; i++)
                            {
                                string attributeName = reader.GetName(i);
                                //if attribute is specified in the query
                                if (terms.ContainsKey(attributeName))
                                {
                                    float qfsim = terms[attributeName].calculateQFSimilarity(reader.GetFloat(i).ToString());
                                    float idfsim = terms[attributeName].calculateIDFSimilarity(reader.GetFloat(i).ToString());
                                    qfidfsims[i - 1] = qfsim * idfsim;
                                }
                                else
                                {
                                    qfidfsims[i - 1] = 0;
                                }
                            }
                            //for each text attribute
                            for (int i = 9; i < 12; i++)
                            {
                                string attributeName = reader.GetName(i);
                                //if attribute is specified in the query
                                if (terms.ContainsKey(attributeName))
                                {
                                    float qfsim = terms[attributeName].calculateQFSimilarity(reader.GetString(i));
                                    float idfsim = terms[attributeName].calculateIDFSimilarity(reader.GetString(i));
                                    qfidfsims[i - 1] = qfsim * idfsim;
                                }
                                else
                                {
                                    qfidfsims[i - 1] = 0;
                                }
                            }

                            using (SQLiteCommand command2 = new SQLiteCommand(connection))
                            {
                                command2.CommandText = String.Format(
                                @"
                        INSERT INTO qfidfsimilarity VALUES 
                        ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11})",
                                id,
                                qfidfsims[0],
                                qfidfsims[1],
                                qfidfsims[2],
                                qfidfsims[3],
                                qfidfsims[4],
                                qfidfsims[5],
                                qfidfsims[6],
                                qfidfsims[7],
                                qfidfsims[8],
                                qfidfsims[9],
                                qfidfsims[10]
                                );
                                command2.ExecuteNonQuery();
                            }
                        }
                    }
                }
                connection.Close();
            }
        }

        //deletes all similarity tables, needed if we want to do a new query
        public void deleteSimilarityTables()
        {

        }

        //creates extended table based on the QF scores of the missing attribute values of the query
        //use in case of too many answers see section 5 of paper
        public void createExtendedQFTable()
        {
            using (SQLiteConnection connection = new SQLiteConnection(DBManipulations.connectionString))
            {
                connection.Open();
                DBManipulations.executeSQLNoConnection(connection, @"CREATE TABLE extendedqfsum (
                                            id integer NOT NULL,
                                            extendedqfsum float,
                                            PRIMARY KEY (id))"
                );

                DBManipulations.readTuplesNoConnection(connection, @"SELECT * FROM autompg",
                    delegate (SQLiteDataReader reader)
                    {
                        //store current tuple and qfsimilarity info
                        int id = reader.GetInt32(0);
                        float qfValSum = 0;

                        //for each column in autompg
                        //if it that attribute is not mentioned in the query
                        //then add the qf value of the attribute value to extendedqf 
                        for (int i = 1; i < 9; i++)
                        {
                            string attributeName = reader.GetName(i);
                            float tupleVal = reader.GetFloat(i);

                            if (!terms.ContainsKey(attributeName))
                            {
                                //retrieve qf value from the qftable
                                DBManipulations.readTuplesNoConnection(connection,
                                    String.Format(@"SELECT qf FROM {0}qf WHERE {0} = {1}", attributeName, tupleVal),
                                    delegate (SQLiteDataReader reader2)
                                    {
                                        qfValSum+= reader2.GetFloat(0);
                                    }
                                    );
                            }
                        }

                        for (int i = 9; i < 12; i++)
                        {
                            string attributeName = reader.GetName(i);
                            string tupleVal = "'" + reader.GetString(i) + "'";

                            if (!terms.ContainsKey(attributeName))
                            {
                                //retrieve qf value from the qftable
                                DBManipulations.readTuplesNoConnection(connection,
                                    String.Format(@"SELECT qf FROM {0}qf WHERE {0} = {1}", attributeName, tupleVal),
                                    delegate (SQLiteDataReader reader2)
                                    {
                                        qfValSum+= reader2.GetFloat(0);
                                    }
                                    );
                            }
                        }
                        DBManipulations.executeSQLNoConnection(connection,
                            String.Format(
                                @"
                        INSERT INTO extendedqfsum VALUES 
                        ({0}, {1})",
                                id,
                                qfValSum
                                ));
                    }
                    );
                connection.Close();
            }
        }
    }
}
