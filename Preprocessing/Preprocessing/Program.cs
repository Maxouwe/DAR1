using System.Collections;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.IO;
using System.Runtime.CompilerServices;

namespace Preprocessing
{
    internal class Program
    {
        static string metaConnectionString = @"Data Source=..\..\..\..\..\db\meta.db;Version=3";

        //delegate function type that we can use as callback function for reading tuples from a table
        public delegate void readFunc(SQLiteDataReader reader);

        static void Main(string[] args)
        {
            //delete metadatabase als die er nog is van een vorige keer
            if (File.Exists(@"..\..\..\..\..\db\meta.db"))
            {
                File.Delete(@"..\..\..\..\..\db\meta.db");
            }

            //create the database files
            SQLiteConnection.CreateFile(@"..\..\..\..\..\db\meta.db");

            //make a connection with the database
            SQLiteConnection metaConnection = new SQLiteConnection(metaConnectionString);
            metaConnection.Open();

            //put the filled autompg table into the metadb
            executeSQL(metaConnection, File.ReadAllText(@"..\..\..\..\..\db\autompg.sql"));

            //create all qf and idf tables in the metadb
            executeSQL(metaConnection, File.ReadAllText(@"..\..\..\..\..\db\metadb.txt"));
         
            //execute the sql instructions from metaload.txt
            //this file fills all categorical and numerical idf table
            executeSQL(metaConnection, File.ReadAllText(@"..\..\..\..\..\db\metaload.txt"));

            //calculate all QF values from the workload
            collectQF(metaConnection);

            metaConnection.Close();
        }

        //finds all QFs and puts them in the table specified through the connection parameter
        static void collectQF(SQLiteConnection connection)
        {
            string[] lines = File.ReadLines(@"..\..\..\..\..\db\workload.txt").ToArray();

            float RQFMax = 0;

            //make dictionary so we can look up currently known RQF of an attribute by the attribute name
            Dictionary<string, float> RQFs = new Dictionary<string, float>();

            //parse all lines
            //and put it in the dictionary with format ["attribute=value", rqf(value)]
            //for each attribute value v: if we find v in an AND clause we add the number(foud at the beginning of the line) to rqf(v)
            //at the same time we keep track of the current rqfmax
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


            //fill all qf tables
            //categorical
            fillQFTable(connection, "brand", RQFs, RQFMax);
            fillQFTable(connection, "model", RQFs, RQFMax);
            fillQFTable(connection, "type", RQFs, RQFMax);
            fillQFTable(connection, "origin", RQFs, RQFMax);
            //numerical
            fillQFTable(connection, "mpg", RQFs, RQFMax);
            fillQFTable(connection, "cylinders", RQFs, RQFMax);
            fillQFTable(connection, "displacement", RQFs, RQFMax);
            fillQFTable(connection, "horsepower", RQFs, RQFMax);
            fillQFTable(connection, "weight", RQFs, RQFMax);
            fillQFTable(connection, "acceleration", RQFs, RQFMax);
            fillQFTable(connection, "model_year", RQFs, RQFMax);
            
        }

        //fill QF table for attribute based on the RQF and RQFMax
        static void fillQFTable(SQLiteConnection connection, string attribute, Dictionary<string, float> RQFs, float RQFMax)
        {
            readTuples(connection,
                String.Format(@"SELECT {0} FROM autompg GROUP BY {0}", attribute),
                delegate (SQLiteDataReader reader)
                {
                    string val;
                    try
                    {
                        val = reader.GetString(reader.GetOrdinal(attribute));
                    }
                    catch
                    {
                        val = reader.GetFloat(reader.GetOrdinal(attribute)).ToString();
                    }
                    string key = attribute + "=" + val;
                    if (RQFs.ContainsKey(key))
                    {
                        executeSQL(connection, String.Format(@"INSERT INTO {0}qf VALUES({1}, {2})", attribute, "\'" + val + "\'", (RQFs[key] + 1) / (RQFMax + 1)));
                    }
                    else
                    {
                        executeSQL(connection, String.Format(@"INSERT INTO {0}qf VALUES({1}, {2})", attribute, "\'" + val + "\'", 1 / (RQFMax + 1)));
                    }
                }
                );
        }

        //execute the sql statements from given by the string
        //using the db file signified by the dbConnection
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
        //use the function f to decide what to do with each tuple
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