using System.Data;
using System.Data.Common;
using System.Data.SQLite;

namespace QueryProcessing
{
    internal class Program
    {
        
        static int numTuples;
        static string[] attributes = { "mpg", "cylinders", "displacement", "horsepower", "weight", "acceleration", "model_year", "origin", "brand", "model", "type" };

        static void Main(string[] args)
        {
            //example code for creating attributes and calculating qfidf scores
            DBManipulations.executeSQL("DROP TABLE qfidfsimilarity");
            List<Attribute> attributes = new List<Attribute>();
            
            attributes.Add(new NumericalAttribute("cylinders", "8"));
            attributes.Add(new NumericalAttribute("horsepower", "60.5"));
            attributes.Add(new CategoricalAttribute("type", "'coupe'"));
            attributes.Add(new CategoricalAttribute("origin", "2"));
            attributes.Add(new CategoricalAttribute("model", "'1131 deluxe sedan'"));
            SimilarityScoreTable simT = new SimilarityScoreTable(attributes);
            simT.createQFIDFSimilarityTable();
        }
        static QueryProcessor? parseInput(string input)
        {
            int k = 10;

            string[] subqueries = input.Trim().Split(',');

            List<Attribute> terms = new List<Attribute>();

            for (int i = 0; i < subqueries.Length; i++)
            {
                string attribute = subqueries[i].Split('=')[0].Trim();
                string value = subqueries[i].Trim().Split('=')[1].Trim();

                if (value[value.Length - 1] == ';')
                {
                    value = value.Substring(0, value.Length - 1);
                }

                int l = 0;
                if (attribute == "k")
                {
                    k = Int32.Parse(value);
                }

                else if (!attributes.Contains(attribute))
                {
                    return null;
                }

                //if the value is not in quotes i.e. it is a numerical attribute
                else if (value[0] != '\'')
                {
                    terms.Add(new NumericalAttribute(attribute, value));
                }

                //if categorical attribute
                else
                {
                    terms.Add(new CategoricalAttribute(attribute, value));
                }
            }

            return new QueryProcessor(new Query(terms), k);
        }


    }
}