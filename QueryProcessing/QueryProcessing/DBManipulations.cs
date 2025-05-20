using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QueryProcessing
{
    //this class is made to remove some boilerplate code
    internal class DBManipulations
    {
        public static string connectionString = @"Data Source=..\..\..\..\..\db\metadata.db;Version=3";

        public delegate void readFunc(SQLiteDataReader reader);

        //execute the sql statements from given by the string
        //using the db file signified by the dbConnection
        public static void executeSQL(string sqlStatements)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(connection))
                {
                    command.CommandText = sqlStatements;
                    command.ExecuteNonQuery();
                }
                connection.Close();
            }
        }

        //reads tuples from a database 
        //sqlStatement should be a SELECT statement
        //supply a delegate function to decide what to do with each tuple
        public static void readTuples(string sqlStatement, readFunc f)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(connection))
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
                connection.Close();
            }
        }
        //without connectionOpen() and connectionClose()
        //useful for nesting sql operations
        //nesting with readTuples is not possible because you can only have one connection open for the DB
        public static void readTuplesNoConnection(SQLiteConnection connection, string sqlStatement, readFunc f)
        {
            using (SQLiteCommand command = new SQLiteCommand(connection))
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
        //same as readtuplenoconnection but for executesql
        public static void executeSQLNoConnection(SQLiteConnection connection, string sqlStatements)
        {
            using (SQLiteCommand command = new SQLiteCommand(connection))
            {
                command.CommandText = sqlStatements;
                command.ExecuteNonQuery();
            }
        }






        //example of how to use readTuples
        private int readTuplesExample()
        {
            int count = 0;
            DBManipulations.readTuples(@"SELECT COUNT(*) AS c FROM topk",
                delegate (SQLiteDataReader reader)
                {
                    count = reader.GetInt32(reader.GetOrdinal("c"));
                }
                );
            return count;
        }

        //example of how to use readTuplesNoConnection
        private int readTuplesNoConnectionExample()
        {
            int count = 0;
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                DBManipulations.readTuplesNoConnection(connection, @"SELECT COUNT(*) AS c FROM topk",
                delegate (SQLiteDataReader reader)
                {
                    count = reader.GetInt32(reader.GetOrdinal("c"));

                    //nesting is now possible
                    //no oneconnectionperdatabase error anymore with this
                    DBManipulations.readTuplesNoConnection(connection, @"SELECT COUNT(*) AS c FROM topk",
                    delegate (SQLiteDataReader reader)
                    {
                        count+= reader.GetInt32(reader.GetOrdinal("c"));
                    }
                    );
                }
                );
                connection.Close();
                return count;
            }
                
        }
    }
}

