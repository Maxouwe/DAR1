using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace QueryProcessing
{
    //contains the S(t, q) scores of all tuples with respect to queryvalues of the query
    //this is for just one attribute
    internal class SimilarityScoreTable
    {
        public Dictionary<string, Attribute> terms { get; }
        public SimilarityScoreTable(List<Attribute> terms) 
        {
            foreach(Attribute a in terms)
            {
                this.terms[a.attributeName] = a;
            }
        }

        //creates a table on how each tuple scores in QF similarity with the query
        public void createQFSimilarityTable()
        {
            DBManipulations.executeSQL(@"CREATE TABLE qfsimilarity(
                                            id integer NOT NULL,
                                            mpg real,
                                            mpgqfsim real,
                                            cylinders integer,
                                            cylindersqfsim real,
                                            displacement real,
                                            displacementqfsim real,
                                            horsepower real,
                                            horsepowerqfsim real,
                                            weight real,
                                            weightqfsim real,
                                            acceleration real,
                                            accelerationqfsim real,
                                            model_year integer,
                                            model_yearqfsim real,
                                            origin integer,
                                            originqfsim real,
                                            brand text, 
                                            brandqfsim real,
                                            model text,
                                            modelqfsim real,
                                            type text,
                                            typeqfsim real,
                                            PRIMARY KEY (id)"
            );
            //for each tuple in autompg calculate the qf similarities for each attribute 
            //and total qf similarity score of that tuple
            //and add the new tuple to the qfsimilarity table
            //check for all existing attributes wether they are avaible in the dict
            //if yes the use
            //something like Attribute.calculateQFSimilarity(reader.GetValue(reader.GetOrdinal(a.attributeName)).ToString());
            //otherwise qf = 0
            

        }

        //deletes all similarity tables, needed if we want to do a new query
        public void deleteSimilarityTables()
        {

        }
    }
}
