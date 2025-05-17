using System.Collections;
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
            executeSQL(metaConnection, File.ReadAllText(@"..\..\..\..\..\db\metaTableDefinitions.txt"));
         
            //execute all load instructions from metaTableLoadInstructions.txt
            //i.e. fill all idf-tables with data (both categorical and numerical)
            executeSQL(metaConnection, File.ReadAllText(@"..\..\..\..\..\db\metaTableLoadInstructions.txt"));

            //fill all qf table with data (categorical and numerical)
            //by parsing and analyzing the workload.txt file
            calculateQFs(metaConnection);

            //close the database connection
            metaConnection.Close();
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