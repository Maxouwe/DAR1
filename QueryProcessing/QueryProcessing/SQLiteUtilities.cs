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
    internal class SQLiteUtilities
    {
        public delegate void readFunc(SQLiteDataReader reader);

        //execute the sql statements from given by the string
        //using the db file signified by the dbConnection
        public static void executeSQL(string connectionString, string sqlStatements)
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
        public static void readTuples(string connectionString, string sqlStatement, readFunc f)
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
    }
}

