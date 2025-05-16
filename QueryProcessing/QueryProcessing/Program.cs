using System.Data;
using System.Data.Common;
using System.Data.SQLite;

namespace QueryProcessing
{
    internal class Program
    {
        static string connectionString = @"Data Source=..\..\..\..\..\db\meta.db;Version=3";
        static int numTuples;
        static string[] attributes = { "mpg", "cylinders", "displacement", "horsepower", "weight", "acceleration", "model_year", "origin", "brand", "model", "type" };

        static void Main(string[] args)
        {

            //for a demo, comment runProgram() and uncomment runDemo()
            //runProgram();


            runDemo();

        }
        static void runProgram()
        {
            //get the amount of tuples(needed for multiple calculations)
            SQLiteUtilities.readTuples(connectionString,
                @"SELECT * FROM numtuples",
                delegate (SQLiteDataReader reader)
                {
                    numTuples = reader.GetInt32(reader.GetOrdinal("num"));
                }
                );



            while (true)
            {
                Console.WriteLine("Enter query according to the format");


                QueryProcessor processor = parseInput(Console.ReadLine());

                //input contained attributes that are not in autompg
                if (processor == null)
                {
                    Console.WriteLine("Only enter attributes that are in the table");
                }

                else
                {
                    processQuery(processor);
                }

            }
        }
        static void runDemo()
        {
            //get the amount of tuples(needed for multiple calculations)
            SQLiteUtilities.readTuples(connectionString,
                @"SELECT * FROM numtuples",
                delegate (SQLiteDataReader reader)
                {
                    numTuples = reader.GetInt32(reader.GetOrdinal("num"));
                }
                );

            QueryProcessor processor = parseInput(@"k = 6, brand = 'oldsmobile', type = 'sedan', mpg = 40;");
            processQuery(processor);

            Console.WriteLine("as you can see the topk is ordered by the sum of qfidf(t, q) over all query attributes");
            Console.WriteLine("Maybe you notice the first one has mpg = 32 and the second one has mpg =39");
            Console.WriteLine("Eventhough 39 is closer to 40 than 32");
            Console.WriteLine("but this is because the value of 32 is present in the workload and 39 isnt");
            Console.WriteLine("So the higher qf score of 32 leads to a higher qfidf score than that of 39 \n");
            Console.WriteLine("Press enter for the next input");
            Console.ReadLine();

            processor = parseInput(@"k = 5, type = 'coupe';");
            processQuery(processor);

            Console.WriteLine("in this case only type is specified, so the qfidf score will be the same for every found tuple");
            Console.WriteLine("this means we need to use an additional ranking and order by the score \n");
            Console.WriteLine("Press enter for the next input");
            Console.ReadLine();

            processor = parseInput(@"k = 5, model_year = 68;");
            processQuery(processor);

            Console.WriteLine("we know that there are no cars with model_year = 68");
            Console.WriteLine("So instead cars with the closest year will be returned, which is 70");
            Console.WriteLine("Because these cars score the highest in qfidf similarity\n");
            Console.WriteLine("Press enter for the next input");
            Console.ReadLine();

            processor = parseInput(@"k = 3, brand = 'ferrari', acceleration = 20;");
            processQuery(processor);

            Console.WriteLine("there are no ferrari's, so it will search only by the acceleration term");
            Console.WriteLine("and it returned the tuples with the most similar acceleration\n");
            Console.WriteLine("Press enter for the next input");
            Console.ReadLine();

            processor = parseInput(@"k = 3, brand = 'mazda', type = 'convertible', mpg = 20, horsepower = 100;");
            processQuery(processor);

            Console.WriteLine("there are no convertible mazdas");
            Console.WriteLine("after seeing that, the algorithm searched just by brand + numerical attributes, leaving out type");
            Console.WriteLine("So it returned all mazdas with the most similar numerical attributes (taking into account also the qf score)");
            Console.WriteLine("End of demo");
            Console.ReadLine();
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

        static void processQuery(QueryProcessor processor)
        {
            processor.deleteTables();
            processor.findTopK();

            SQLiteUtilities.readTuples(connectionString,
            @"SELECT * FROM topk",
            delegate (SQLiteDataReader reader)
            {
                int numColumns = reader.GetColumnSchema().Count;
                object[] values = new object[numColumns];
                reader.GetValues(values);
                for (int i = 0; i < values.Length; i++)
                {
                    Console.WriteLine(reader.GetName(i) + "=" + values[i]);
                }
                Console.WriteLine('\n');
            });

            processor.deleteTables();
        }

    }
}