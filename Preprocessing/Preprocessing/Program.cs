using System.Collections;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.IO;
using System.Runtime.CompilerServices;

namespace Preprocessing
{
    internal class Program
    {
        static string metaDBConnectionString = @"Data Source=..\..\..\..\..\db\metadata.db;Version=3";

        //useful for reducing boilerplate code
        public delegate void readFunc(SQLiteDataReader reader);

        static void Main(string[] args)
        {
            Console.WriteLine("this might take a few minutes");
            Console.WriteLine("processing...");
            //make sure to delete the previous metadatabase
            if (File.Exists(@"..\..\..\..\..\db\metadata.db"))
            {
                File.Delete(@"..\..\..\..\..\db\metadata.db");
            }

            //create a new metadatabase
            SQLiteConnection.CreateFile(@"..\..\..\..\..\db\metadata.db");

            //make a connection with the database
            SQLiteConnection metaConnection = new SQLiteConnection(metaDBConnectionString);
            metaConnection.Open();

            //load the autompg table
            executeSQL(metaConnection, File.ReadAllText(@"..\..\..\..\..\db\autompg.sql"));

            //instantiate all qf and idf tables 
            executeSQL(metaConnection, File.ReadAllText(@"..\..\..\..\..\db\metadb.txt"));

            //execute all load instructions from metaTableLoadInstructions.txt
            //i.e. fill all idf-tables with data (both categorical and numerical)
            executeSQL(metaConnection, File.ReadAllText(@"..\..\..\..\..\db\metaload.txt"));

            //calculate all jacquard coefficients
            //from the workload
            createJacquardTables(metaConnection);

            //fill all qf table with data (categorical and numerical)
            //by parsing and analyzing the workload.txt file
            calculateQFs(metaConnection);

            

            //close the database connection
            metaConnection.Close();
            Console.WriteLine("Done");
            Console.ReadLine();
        }

        //fill all qf table with data (categorical and numerical)
        //by parsing and analyzing the workload.txt file
        static void calculateQFs(SQLiteConnection connection)
        {
            string[] lines = File.ReadLines(@"..\..\..\..\..\db\workload.txt").ToArray();

            float RQFMax = 0;

            //make dictionary so we can look up currently known RQF of an attribute by the attribute name
            Dictionary<string, float> RQFs = new Dictionary<string, float>();

            //find the rqf for each <attribute, value> pair
            //and put them in a dictionary like this <"attribute=value", rqf(value)>
            for (int i = 2; i < lines.Length; i++)
            {
                if (lines[i] != "")
                {

                    string[] line = lines[i].Trim().Split('=');

                    int freq = Int32.Parse(line[0].Split(' ')[0]);


                    for (int j = 0; j < line.Length - 1; j++)
                    {
                        string[] subline = line[j].Trim().Split(' ');

                        string key = subline[subline.Length - 1] + "=" + line[j + 1].Split('\'')[1];


                        if (RQFs.ContainsKey(key))
                        {
                            RQFs[key] = RQFs[key] + freq;
                        }
                        else
                        {
                            RQFs[key] = freq;
                        }

                        if (RQFs[key] > RQFMax)
                        {
                            RQFMax = (int)RQFs[key];
                        }
                    }
                }
            }

            //fill all qf tables with data for each attribute
            populateQFTable(connection, "brand", RQFs, RQFMax);
            populateQFTable(connection, "model", RQFs, RQFMax);
            populateQFTable(connection, "type", RQFs, RQFMax);
            populateQFTable(connection, "origin", RQFs, RQFMax);
            populateQFTable(connection, "mpg", RQFs, RQFMax);
            populateQFTable(connection, "cylinders", RQFs, RQFMax);
            populateQFTable(connection, "displacement", RQFs, RQFMax);
            populateQFTable(connection, "horsepower", RQFs, RQFMax);
            populateQFTable(connection, "weight", RQFs, RQFMax);
            populateQFTable(connection, "acceleration", RQFs, RQFMax);
            populateQFTable(connection, "model_year", RQFs, RQFMax);
            
        }

        //fill every <attribute-name>QF table
        //given the RQF dictionary and maximumRQF
        static void populateQFTable(SQLiteConnection connection, string attribute, Dictionary<string, float> RQFs, float RQFMax)
        {
            readTuples(connection,
                String.Format(@"SELECT {0} FROM autompg GROUP BY {0}", attribute),
                delegate (SQLiteDataReader reader)
                {
                    string val;
                    try
                    {
                        //try to get string value of the attribute
                        val = reader.GetString(reader.GetOrdinal(attribute));
                    }
                    catch
                    {
                        //otherwise get the float value of the attribute
                        val = reader.GetFloat(reader.GetOrdinal(attribute)).ToString();
                    }

                    //format the attribute value so we can look it up in our rqf dictionary
                    string key = attribute + "=" + val;

                    if (RQFs.ContainsKey(key))
                    {
                        //if the attribute value has been mentioned in the workload
                        executeSQL(connection, String.Format(@"INSERT INTO {0}qf VALUES({1}, {2})", attribute, "\'" + val + "\'", (RQFs[key] + 1) / (RQFMax + 1)));
                    }
                    else
                    {
                        //otherwise we have RQF(attribute=value) = 0
                        executeSQL(connection, String.Format(@"INSERT INTO {0}qf VALUES({1}, {2})", attribute, "\'" + val + "\'", 1 / (RQFMax + 1)));
                    }
                }
                );
        }

        //create a table for each attribute
        //where each entry contains a attributevalue combination and its jacquard coefficient
        public static void createJacquardTables(SQLiteConnection connection)
        {
            //only brand and type are inside IN queries in the workload
            executeSQL(connection,
                @"CREATE TABLE wbrand(
                    brand text,
                    inqueryid int
                    )"
                );

            executeSQL(connection,
                @"CREATE TABLE wtype(
                    type text,
                    inqueryid int
                    )"
                );
            executeSQL(connection,
                @"CREATE TABLE inquerycombinationsbrand(
                    brand1 text,
                    brand2 text,
                    UNIQUE(brand1, brand2)
                    )"
                );
            executeSQL(connection,
                @"CREATE TABLE inquerycombinationstype(
                    type1 text,
                    type2 text,
                    UNIQUE(type1, type2)
                    )"
                );
            string[] lines = File.ReadLines(@"..\..\..\..\..\db\workload.txt").ToArray();


            //keep record of inquery id
            int INQID = 1;
          
            for (int i = 2; i < lines.Length; i++)
            {
                if (lines[i] != "")
                {
                    string[] line = lines[i].Trim().Split(' ');

                    //if there is an inquery
                    if (line.Contains<string>("IN"))
                    {
                        int a = 0;
                        string[] beforeBracket = lines[i].Trim().Split('(')[a].Trim().Split(' ');
                        if (beforeBracket[beforeBracket.Length-1] == "COUNT")
                        {
                            a = 1;
                            beforeBracket = lines[i].Trim().Split('(')[a].Trim().Split(' ');
                        }
                        string attribute = beforeBracket[beforeBracket.Length - 2];

                        //get everything inside the brackets of the inquery
                        string inquery = lines[i].Trim().Split('(')[a + 1].Trim().Split(')')[0];
                        //get all elements of inquery
                        string[] INQelements = inquery.Trim().Split(',');


                        //add each element to the corresponding wtable together with inquery id
                        //add each element combination to the inquery combination table
                        for (int j = 0; j < INQelements.Length; j++)
                        {
                            
                            executeSQL(
                                connection,
                                String.Format(@"INSERT INTO w{0} VALUES ({1}, {2})", attribute, INQelements[j], INQID)
                                );

                            for (int k = j + 1; k < INQelements.Length; k++)
                            {
                                executeSQL(
                                    connection,
                                    String.Format(@"INSERT OR IGNORE INTO inquerycombinations{0} VALUES ({1}, {2})", attribute, INQelements[j], INQelements[k])
                                    );
                            }
                        }
                        INQID++;
                    }
                    
                }
                
            }

            //jacquard brand
            executeSQL(
                connection,
                @"CREATE TABLE jacquardbrand(
                    brand1 text,
                    brand2 text,
                    jcoef real
                )"
                );

            readTuples(
                connection,
                @"SELECT * FROM inquerycombinationsbrand",
                delegate (SQLiteDataReader reader)
                {
                    string brand1 = "'" + reader.GetString(0) + "'";
                    executeSQL(
                        connection,
                        @"CREATE TABLE IF NOT EXISTS temp1(
                            INQID int,
                            UNIQUE(INQID)
                        )"
                        );

                    readTuples(
                        connection,
                        String.Format(@"SELECT * FROM wbrand WHERE brand = {0}", brand1),
                        delegate(SQLiteDataReader reader2)
                        {
                            executeSQL(
                                connection,
                                String.Format(@"INSERT INTO temp1 VALUES ({0})", reader2.GetInt32(1))
                                );
                        }
                        );

                    string brand2 = "'" + reader.GetString(1) + "'";
                    executeSQL(
                        connection,
                        @"CREATE TABLE IF NOT EXISTS temp2(
                            INQID int,
                            UNIQUE(INQID)
                        )"
                        );

                    readTuples(
                        connection,
                        String.Format(@"SELECT * FROM wbrand WHERE brand = {0}", brand2),
                        delegate (SQLiteDataReader reader3)
                        {
                            executeSQL(
                                connection,
                                String.Format(@"INSERT INTO temp2 VALUES ({0})", reader3.GetInt32(1))
                                );
                        }
                        );

                    //get size of INTERSECTION(W(t), W(q))
                    int intersectionSize = 1;
                    readTuples(
                        connection,
                        @"SELECT COUNT(*) FROM temp1 INNER JOIN temp2 ON temp1.INQID = temp2.INQID",
                        delegate (SQLiteDataReader reader4)
                        {
                            intersectionSize = reader4.GetInt32(0);
                        }
                        );
                    //get size of UNION(W(t), W(q))
                    int unionSize = 1;
                    readTuples(
                        connection,
                        @"SELECT COUNT(DISTINCT INQID) FROM (SELECT * FROM temp1 UNION SELECT * FROM temp2)",
                        delegate (SQLiteDataReader reader4)
                        {
                            unionSize = reader4.GetInt32(0);
                        }
                        );
                    executeSQL(
                        connection,
                        String.Format(@"INSERT INTO jacquardbrand VALUES ({0}, {1}, {2})", 
                        brand1, 
                        brand2, 
                        (float)intersectionSize/(float)unionSize)
                        );

                    executeSQL(
                        connection,
                        @"DELETE FROM temp1"
                        );
                    executeSQL(
                        connection,
                        @"DELETE FROM temp2"
                        );
                }
                );

            //jacquard type
            executeSQL(
                connection,
                @"CREATE TABLE jacquardtype(
                    type1 text,
                    type2 text,
                    jcoef real
                )"
                );

            readTuples(
                connection,
                @"SELECT * FROM inquerycombinationstype",
                delegate (SQLiteDataReader reader)
                {
                    string type1 = "'" + reader.GetString(0) + "'";
                    executeSQL(
                        connection,
                        @"CREATE TABLE IF NOT EXISTS temp1(
                            INQID int,
                            UNIQUE(INQID)
                        )"
                        );

                    readTuples(
                        connection,
                        String.Format(@"SELECT * FROM wtype WHERE type = {0}", type1),
                        delegate (SQLiteDataReader reader2)
                        {
                            executeSQL(
                                connection,
                                String.Format(@"INSERT INTO temp1 VALUES ({0})", reader2.GetInt32(1))
                                );
                        }
                        );

                    string type2 = "'" + reader.GetString(1) + "'";
                    executeSQL(
                        connection,
                        @"CREATE TABLE IF NOT EXISTS temp2(
                            INQID int,
                            UNIQUE(INQID)
                        )"
                        );

                    readTuples(
                        connection,
                        String.Format(@"SELECT * FROM wtype WHERE type = {0}", type2),
                        delegate (SQLiteDataReader reader3)
                        {
                            executeSQL(
                                connection,
                                String.Format(@"INSERT INTO temp2 VALUES ({0})", reader3.GetInt32(1))
                                );
                        }
                        );

                    //get size of INTERSECTION(W(t), W(q))
                    int intersectionSize = 1;
                    readTuples(
                        connection,
                        @"SELECT COUNT(*) FROM temp1 INNER JOIN temp2 ON temp1.INQID = temp2.INQID",
                        delegate (SQLiteDataReader reader4)
                        {
                            intersectionSize = reader4.GetInt32(0);
                        }
                        );
                    //get size of UNION(W(t), W(q))
                    int unionSize = 1;
                    readTuples(
                        connection,
                        @"SELECT COUNT(DISTINCT INQID) FROM (SELECT * FROM temp1 UNION SELECT * FROM temp2)",
                        delegate (SQLiteDataReader reader4)
                        {
                            unionSize = reader4.GetInt32(0);
                        }
                        );
                    executeSQL(
                        connection,
                        String.Format(@"INSERT INTO jacquardtype VALUES ({0}, {1}, {2})",
                        type1,
                        type2,
                        (float)intersectionSize / (float)unionSize)
                        );
                    executeSQL(
                        connection,
                        @"DELETE FROM temp1"
                        );
                    executeSQL(
                        connection,
                        @"DELETE FROM temp2"
                        );
                }
                );
            executeSQL(
                connection,
                @"DROP TABLE temp1;
                  DROP TABLE temp2;
                  DROP TABLE inquerycombinationsbrand;
                  DROP TABLE inquerycombinationstype;
                  DROP TABLE wbrand;
                  DROP TABLE wtype;
                "
                );
        }
        //executes a string of sql statements
        static void executeSQL(SQLiteConnection dbConnection, string sqlStatements)
        {
            using (SQLiteCommand command = new SQLiteCommand(dbConnection))
            {
                command.CommandText = sqlStatements;
                command.ExecuteNonQuery();
            }
        }

        //reads tuples from a database 
        //sqlStatement should be a SELECT statement
        //on each tuple we perform calback function f
        static void readTuples(SQLiteConnection dbConnection, string sqlStatement, readFunc f)
        {
            using (SQLiteCommand command = new SQLiteCommand(dbConnection))
            {
                command.CommandText = sqlStatement;
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    //for each found tuple perform the callback function f
                    while (reader.Read())
                    {
                        f(reader);
                    }
                }
            }
        }

        

    }
}