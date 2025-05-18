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

        }
        static QueryProcessor? parseInput(string input)
        {
            int k = 10;

            string[] subqueries = input.Trim().Split(',');

            List<NumericalAttribute> numTerms = new List<NumericalAttribute>();
            List<CategoricalAttribute> catTerms = new List<CategoricalAttribute>();

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
                    numTerms.Add(new NumericalAttribute(attribute, float.Parse(value), numTuples, connectionString));
                }

                //if categorical attribute
                else
                {
                    catTerms.Add(new CategoricalAttribute(attribute, value));
                }
            }

            return new QueryProcessor(new Query(numTerms, catTerms), k, connectionString);
        }


    }
}