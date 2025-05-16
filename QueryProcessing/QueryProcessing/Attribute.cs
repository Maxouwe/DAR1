using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.SQLite;
using QueryProcessing;

namespace QueryProcessing
{
    class NumericalAttribute
    {
        //h = sigma*n^(-0.2), taken from the paper
        public float h;
        public string attributeName { get; }
        public float queryValue { get; }
        //number of tuples in autompg, used internally for computations
        private int _numTuples;
        public NumericalAttribute(string name, float qval, int numTuples, string connectionString)
        {
            attributeName = name;
            queryValue = qval;
            _numTuples = numTuples;

            SQLiteUtilities.readTuples(connectionString, String.Format(@"SELECT bandwidth FROM {0}bandwidth", name),
                delegate (SQLiteDataReader reader)
                {
                    this.h = reader.GetFloat(reader.GetOrdinal("bandwidth"));
                }
                );


        }

        //creates and fills an QFIDF score table for each attribute, and puts them in the database
        //for each value of attribute A in autompg we create a tuple having a field:
        //           -for that attribute value
        //           -for the qfidf similarity score for that attribute value t and the value specified in the query q
        //where qfidfsimilarity(t, q) = qfsimilarity(t, q) * idfsimilarity(t, q)
        public void createQFIDFTable(string connectionString)
        {
            //join the QFSimilarityTable with the IDFSimilarityTable ON QFTable.this.attributeName = IDFTable.this.attributeName
            //and then multiply the columns

            //create the qfidf table
            SQLiteUtilities.executeSQL(connectionString, String.Format(@"CREATE TABLE {0}qfidf({0} real, qfidf real, PRIMARY KEY({0}))", attributeName));

            createIDFSimilarityTable(connectionString);

            //fill the qfidf table by selecting from the qf and idf table and multiplying the values for each attribute value
            SQLiteUtilities.executeSQL(connectionString,
                String.Format(@"INSERT INTO {0}qfidf SELECT {0}qf.{0}, {0}qf.qf * {0}idf.idf FROM {0}qf INNER JOIN {0}idf ON {0}qf.{0} = {0}idf.{0}", attributeName
                ));
        }

        //to be called at the end of a query
        //we drop de idf and qfidf tables because they are different for each query
        public void deleteTables(string connectionString)
        {
            SQLiteUtilities.executeSQL(connectionString, String.Format(@"DROP TABLE IF EXISTS {0}idf; DROP TABLE IF EXISTS {0}qfidf", attributeName));
        }

        //using calcTermIDF as IDF(q), calculate the IDFSimilarityTable
        //idfsimilarity(t, q) = e^(-0.5 * ((t - q)/h)^2) * IDF(q)
        private void createIDFSimilarityTable(string connectionString)
        {
            //Create the idf table
            SQLiteUtilities.executeSQL(connectionString, String.Format(@"CREATE TABLE {0}idf({0} real, idf real, PRIMARY KEY({0}))", attributeName));

            //calculate idf(q)
            float idfq = calcTermIDF(connectionString);

            //fill the table
            //the DISTINCT keyword is important, because each value for the attribute might occur more often in autompg
            //but the idf table only needs one idf per distinct value
            //prevents uniqueness constraint fails 
            SQLiteUtilities.executeSQL(connectionString,
                String.Format(@"INSERT INTO {0}idf SELECT {0}, EXP(-0.5 * POW( ({0}-{1})/{2} , 2)) * {3} FROM (SELECT DISTINCT {0} FROM autompg)", attributeName, queryValue, h, idfq));

        }

        //IDF(q) = log(_numTuples/SUM(e^((-0.5*(ti-t)/h)^2)
        //where ti are the distinct values for this attribute
        private float calcTermIDF(string connectionString)
        {
            float idfq = 0;

            SQLiteUtilities.readTuples(connectionString,
                String.Format(@"SELECT LOG({0}/SUM( EXP(-0.5 * POW( (val-{2})/{3} , 2 ) ) ) ) AS idfq FROM (SELECT DISTINCT {1} AS val FROM autompg)", _numTuples, attributeName, queryValue, h),
                delegate (SQLiteDataReader reader)
                {
                    try
                    {
                        idfq = reader.GetFloat(reader.GetOrdinal("idfq"));
                    }
                    catch
                    {
                        Console.WriteLine("please dont supply absurdly small or big values for a certain attribute");
                    }
                });
            return idfq;
        }
    }

    class CategoricalAttribute
    {
        public string queryValue { get; }
        public string attributeName { get; }
        public CategoricalAttribute(string name, string qval)
        {
            attributeName = name;
            queryValue = qval;
        }
    }
}

