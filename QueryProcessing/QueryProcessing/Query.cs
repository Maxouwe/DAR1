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
    class Query
    {
        public List<NumericalAttribute> numTerms { get; }
        public List<CategoricalAttribute> catTerms { get; }

        public Query(List<NumericalAttribute> nTerms, List<CategoricalAttribute> cTerms)
        {
            numTerms = nTerms;
            catTerms = cTerms;
        }

        //creates and fills an QFIDF score table for each attribute, and puts them in the database
        //for each value of attribute A in autompg we create a tuple having a field:
        //           -for that attribute value
        //           -for the qfidf similarity score for that attribute value t and the value specified in the query q
        //where qfidfsimilarity(t, q) = qfsimilarity(t, q) * idfsimilarity(t, q)
        public void createTables(string connectionString)
        {
            //the categorical QFIDF tables are already in the metadb, so just call the methods for creating the numerical QFIDF tables 
            foreach (NumericalAttribute term in numTerms)
            {
                term.createQFIDFTable(connectionString);
            }
        }
        public void removeTables(string connectionString)
        {
            foreach (NumericalAttribute term in numTerms)
            {
                term.deleteTables(connectionString);
            }
        }
    }
}
