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
    abstract class Attribute
    {
        public string attributeName { get; }

        //even numerical queryvalues are strings yes
        public string _queryValue { get; }

        //bools saying wether qf,idf have been calculated
        protected bool qfCalculated = false;
        protected bool idfCalculated = false;

        //containing the qf and idf values, not the qf,idf similarities
        //just the qf(queryvalue) and idf(queryvalue) values
        protected float qf;
        protected float idf;

        public Attribute(string name, string queryValue, string dbConnectionString)
        {
            attributeName = name;
            _queryValue = queryValue;
        }

        protected abstract void calculateQF();
        public abstract float getQF(); 
        protected abstract void calculateIDF();
        public abstract float getIDF();

    }
    class NumericalAttribute : Attribute
    {
        public NumericalAttribute(string name, string qval, string dbString) : base(name, qval, dbString) { }

        protected override void calculateQF()
        {
            //try to get qf value of queryvalue from the qf table
            bool qfValFound = false;
            float qf;

            DBManipulations.readTuples(
                String.Format("@SELECT qf, COUNT(*) AS c FROM {0}qf WHERE {0} = {1}", attributeName, _queryValue),
                delegate (SQLiteDataReader reader)
                {
                    //if there exists a tuple with value = queryvalue
                    if (reader.GetInt32(reader.GetOrdinal("c")) == 1)
                    {
                        qfValFound = true;
                        qf = reader.GetFloat(reader.GetOrdinal("qf"));
                    }
                }
                );

            //if not then get two nearest values l and u, l < queryValue < u and their qf values
            DBManipulations.readTuples(
                String.Format("@SELECT qf, COUNT(*) AS c FROM {0}qf WHERE {0} = {1}", attributeName, _queryValue),
                delegate (SQLiteDataReader reader)
                {
                    //if there exists a tuple with value = queryvalue
                    if (reader.GetInt32(reader.GetOrdinal("c")) == 1)
                    {
                        qfValFound = true;
                        qf = reader.GetFloat(reader.GetOrdinal("qf"));
                    }
                }
                );
            //then interpolate between these two qf values
            qfCalculated = true;
        }

        public float getQF()
        {
            if (!qfCalculated)
            {
                calculateQF();
                return qf;
            }
            else
            {
                return qf;
            }
        }

        protected override void calculateIDF()
        {
            //if queryvalue exists in db then get idf from idf table
            //if not then get two nearest values l and u, l < queryValue < u and their idf values
            //then interpolate between these two idf values
            idfCalculated = true;
        }

        public float getIDF()
        {
            if (!idfCalculated)
            {
                calculateIDF();
                return idf;
            }
            else
            {
                return idf;
            }
        }
    }

    class CategoricalAttribute : Attribute
    {
        public CategoricalAttribute(string name, string qval, string dbString) : base(name, qval, dbString) { }
    }
}

