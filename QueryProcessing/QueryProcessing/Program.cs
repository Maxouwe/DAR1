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

            //runDemo();
            
            while (true)
            {
                runProgram();
            }

        }
        static void runProgram()
        {
            Console.WriteLine("make sure to stick to the input format");
            deleteSimilarityTables();
            Console.WriteLine("please enter your query");
            QueryProcessor processor = parseInput(Console.ReadLine());
            while(processor == null)
            {
                Console.WriteLine("please enter a valid query");
                processor = parseInput(Console.ReadLine());
            }
            Console.WriteLine("processing...");
            processor.rankByQFIDF();
            processor.rankByExtendedQF();
            retrieveTuples();

            Console.WriteLine("press enter for your next query");
            Console.ReadLine();
        }
        static void deleteSimilarityTables()
        {
            DBManipulations.executeSQL("DROP TABLE IF EXISTS qfidfsimilarity");
            DBManipulations.executeSQL("DROP TABLE IF EXISTS extendedqfsum");
            DBManipulations.executeSQL("DROP TABLE IF EXISTS topK");
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

            return new QueryProcessor(terms, k);
        }

        static void retrieveTuples()
        {
            DBManipulations.readTuples(
                @"SELECT topK.id, mpg, cylinders, displacement, horsepower, weight, acceleration, model_year, origin, brand, model, type, qfidfsum, extendedqfsum
                    FROM topK
                    INNER JOIN autompg ON topK.id = autompg.id",
                delegate (SQLiteDataReader reader)
                {
                    Console.WriteLine("-----------------------------------------------------------------------------");
                    for (int i = 0; i < 13; i++)
                    {
                        Console.Write(reader.GetValue(i) + "|");
                    }
                    Console.WriteLine(reader.GetValue(13));
                }
                );
            Console.WriteLine("-----------------------------------------------------------------------------");
        }

        static void runDemo()
        {
            Console.WriteLine("Processing...");
            //example1 zero tuples
            deleteSimilarityTables();
            List<Attribute> attributes = new List<Attribute>();
            attributes.Add(new CategoricalAttribute("brand", "'nissan'"));
            QueryProcessor processor = new QueryProcessor(attributes, 5);
            processor.rankByQFIDF();
            processor.rankByExtendedQF();
            Console.WriteLine("We query brand = nissan");
            Console.WriteLine("There is only one nissan in the database");
            Console.WriteLine("This is an example when there are too little answers (we consider it a zero answers case)");
            Console.WriteLine("We used the jacquard coefficient to determine similar cars");
            Console.WriteLine("This way we are able to retrieve other cars similar to nissan");
            retrieveTuples();
            Console.WriteLine("Press enter to go to the next example");
            Console.ReadLine();
            Console.WriteLine("Processing...");
            Console.WriteLine("");

            //example2 zero tuples
            deleteSimilarityTables();
            attributes = new List<Attribute>();
            attributes.Add(new CategoricalAttribute("brand", "'Spijker'"));
            processor = new QueryProcessor(attributes, 5);
            processor.rankByQFIDF();
            processor.rankByExtendedQF();
            Console.WriteLine("We query brand = Spijker");
            Console.WriteLine("Unfortunately there are no Spijker cars in the database");
            Console.WriteLine("So this is again a case of zero answers");
            Console.WriteLine("The jacquard coefficient does not help because Spijker cars do not appear in the workload");
            Console.WriteLine("But because of our extendedqf method we are able to still retrieve cars");
            Console.WriteLine("In this case we retrieve the most popular cars according to the workload");
            retrieveTuples();
            Console.WriteLine("Press enter to go to the next example");
            Console.ReadLine();
            Console.WriteLine("Processing...");
            Console.WriteLine("");

            //example many tuples
            deleteSimilarityTables();
            attributes = new List<Attribute>();
            attributes.Add(new CategoricalAttribute("type", "'sedan'"));
            attributes.Add(new NumericalAttribute("cylinders", "6"));
            attributes.Add(new NumericalAttribute("mpg", "22"));
            processor = new QueryProcessor(attributes, 5);
            processor.rankByQFIDF();
            processor.rankByExtendedQF();
            Console.WriteLine("We query type = sedan AND cyclinders = 6 AND mpg = 22");
            Console.WriteLine("There are alot of sedans in the database");
            Console.WriteLine("And they all have the same score (second to last number of each tuple)");
            Console.WriteLine("We applied the extendedqf method from the paper...");
            Console.WriteLine("to rank between cars that have the same score.");
            Console.WriteLine("This additional score is the last number of each tuple");
            Console.WriteLine("You can see that we first rank by original score");
            Console.WriteLine("And then if score is the same we rank by extendedqf");
            retrieveTuples();
            Console.WriteLine("End of demo");
            Console.ReadLine();

        }
    }

}