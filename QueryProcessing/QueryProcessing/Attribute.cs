using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.SQLite;
using QueryProcessing;
using System.Runtime.CompilerServices;
using System.Data.Common;

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

        public Attribute(string name, string queryValue)
        {
            attributeName = name;
            _queryValue = queryValue;
        }

        protected abstract void calculateQF();
        public abstract float getQF(); 
        protected abstract void calculateIDF();
        public abstract float getIDF();

        //given de attribute value of a tuple from the database return the qfsimilarityscore
        public abstract float calculateQFSimilarity(string tupleAttributeVal);

        //given de attribute value of a tuple from the database return the idfsimilarityscore
        public abstract float calculateIDFSimilarity(string tupleAttributeVal);
    }
    class NumericalAttribute : Attribute
    {
        public NumericalAttribute(string name, string qval) : base(name, qval) { }

        //implements formula (3)
        public override float calculateIDFSimilarity(string tupleAttributeVal)
        {
            return (float)Math.Exp(-0.5 * Math.Pow(double.Parse(tupleAttributeVal) - double.Parse(_queryValue), 2)) * getIDF();
        }
        //implements formula (3) where IDF(q) is replaced by QF(q)
        public override float calculateQFSimilarity(string tupleAttributeVal)
        {
            return (float)Math.Exp(-0.5*Math.Pow(double.Parse(tupleAttributeVal) -double.Parse(_queryValue), 2)) * getQF();
        }
        protected override void calculateQF()
        {
            //try to get qf value of queryvalue from the qf table
            bool valFound = false;
    
            DBManipulations.readTuples(
                String.Format(@"SELECT qf FROM {0}qf WHERE {0} = {1}", attributeName, _queryValue),
                delegate (SQLiteDataReader reader)
                {
                    //if there exists a tuple with value = queryvalue
                    if (reader.GetFloat(reader.GetOrdinal("qf")) != null)
                    {
                        valFound = true;
                        qf = reader.GetFloat(reader.GetOrdinal("qf"));
                    }
                }
                );

            //if not then get two nearest values l and u to interpolate qf(queryValue)
            if (!valFound)
            {
                ((float, float), (float, float)) linePoints = findLinePointsQF();

                qf = interpolate(linePoints.Item1.Item1, linePoints.Item1.Item2, linePoints.Item2.Item1, linePoints.Item2.Item2);
                qf = Math.Max(qf, 0);
                qf = Math.Min(qf, 1);
            }
            qfCalculated = true;
        }

        public override float getQF()
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
        //finds two nearest values l and u to queryvalue used for interpolation of qf(queryval)
        private ((float, float), (float, float)) findLinePointsQF()
        {
            (float, float)[] lu = new (float, float)[2];
            int luInd = 0;
            //find two closest values to queryvalue
            DBManipulations.readTuples(
            String.Format(
                @"SELECT {0}, qf, abs({1} - {0}) AS dif FROM {0}qf ORDER BY dif LIMIT 2",
                attributeName, _queryValue),
            delegate (SQLiteDataReader reader)
            {
                //even if attribute value is integer GetFloat() still works, should be fine
                if (reader.GetFloat(reader.GetOrdinal(attributeName)) != null)
                {
                    lu[luInd] = (reader.GetFloat(reader.GetOrdinal(attributeName)), reader.GetFloat(reader.GetOrdinal("qf")));
                    luInd++;
                }
            }
            );

            return (lu[0], lu[1]);
        }

        //finds two nearest values l and u to queryvalue used for interpolation of idf(queryval)
        private ((float, float), (float, float)) findLinePointsIDF()
        {
            (float, float)[] lu = new (float, float)[2];
            int luInd = 0;
            //find two closest values to queryvalue
            DBManipulations.readTuples(
            String.Format(
                @"SELECT {0}, idf, abs({1} - {0}) AS dif FROM {0}idf ORDER BY dif LIMIT 2",
                attributeName, _queryValue),
            delegate (SQLiteDataReader reader)
            {
                //even is attribute value is integer GetFloat() still works, should be fine
                if (reader.GetFloat(reader.GetOrdinal(attributeName)) != null)
                {
                    //even if attribute value is integer GetFloat() still works, should be fine
                    if (reader.GetFloat(reader.GetOrdinal(attributeName)) != null)
                    {
                        lu[luInd] = (reader.GetFloat(reader.GetOrdinal(attributeName)), reader.GetFloat(reader.GetOrdinal("idf")));
                        luInd++;
                    }

                }
            }
            );

            return (lu[0], lu[1]);
        }
        //linear interpolation of f(queryval) between two points
        //(l, fL) and (u, fU)
        //both used for f = QF and f = IDF
        private float interpolate(float l, float fL, float u, float fU)
        {
            //y = ax + b
            //where QF(x) = y
            float a = 0;
            float b = 0;
            if (fL == fU)
            {
                return fL;
            }
            else
            {
                a = (fU - fL) / (u - l);
                b = fL - a * l;
                return a * float.Parse(_queryValue) + b;
            }
        }
        protected override void calculateIDF()
        {
            //try to get idf value of queryvalue from the idf table
            bool valFound = false;

            DBManipulations.readTuples(
                String.Format(@"SELECT idf FROM {0}idf WHERE {0} = {1}", attributeName, _queryValue),
                delegate (SQLiteDataReader reader)
                {
                    //if there exists a tuple with value = queryvalue
                    if (reader.GetFloat(reader.GetOrdinal("idf")) != null)
                    {
                        valFound = true;
                        idf = reader.GetFloat(reader.GetOrdinal("idf"));
                    }
                }
                );

            //if not then get two nearest values l and u to interpolate idf(queryValue) values
            if (!valFound)
            {
                ((float, float), (float, float)) linePoints = findLinePointsIDF();

                idf = interpolate(linePoints.Item1.Item1, linePoints.Item1.Item2, linePoints.Item2.Item1, linePoints.Item2.Item2);
                
                idf = Math.Max(idf, 0);
            }
            idfCalculated = true;
        }

        public override float getIDF()
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
        public CategoricalAttribute(string name, string qval) : base(name, qval) { }

        public override float calculateIDFSimilarity(string tupleAttributeVal)
        {
            //if t != q then qfsim(t, q) will be 0 so we dont need to take that into account here also
            //but also if t != q and we want to weigh the jacquard coefficient together with qf (formula 6)
            //then we dont want idf to be 0 because then J(t,q)*QF*IDF will be 0
            //when t!=q we want J(t,q)*QF(t, q) < QF(t, q) when t == q
            //returning the idf will scale things correctly
            return getIDF();
        }

        public override float calculateQFSimilarity(string tupleAttributeVal)
        {
            if (attributeName != "origin")
            {
                if ("'" + tupleAttributeVal + "'" == _queryValue)
                {
                    return getQF();
                }
                else
                {
                    //if the t != q then calculate formula (6) from paper
                    return getJacquardCoef(tupleAttributeVal)*getQF();
                }
            }
            else
            {
                if (tupleAttributeVal == _queryValue)
                {
                    return getQF();
                }
                else
                {
                    //if the t != q then calculate formula (6) from paper
                    return getJacquardCoef(tupleAttributeVal) * getQF();
                }
            }
        }
        
        private float getJacquardCoef(string tupleAttributeVal)
        {
            //only brand and type have jc because
            //only they are mentioned in IN clauses in the workload
            if(attributeName == "brand" || attributeName == "type")
            {
                float jacques = 0;
                DBManipulations.readTuples(
                    String.Format(@"SELECT jcoef FROM jacquard{0} WHERE 
                                    ({0}1 = {1} AND {0}2 = {2})
                                    OR
                                    ({0}1 = {2} AND {0}2 = {1})", 
                    attributeName,
                    _queryValue,
                    "'" + tupleAttributeVal + "'"),
                    delegate (SQLiteDataReader reader)
                    {
                        jacques = reader.GetFloat(0);
                    }
                    );
                return jacques;
            }
            else
            {
                return 0;
            }
        }

        public override float getQF()
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

        protected override void calculateQF()
        {
            DBManipulations.readTuples(
                String.Format(@"SELECT qf FROM {0}qf WHERE {0} = {1}", attributeName, _queryValue),
                delegate (SQLiteDataReader reader)
                {
                    //if there exists a tuple with value = queryvalue
                    if (reader.GetFloat(reader.GetOrdinal("qf")) != null)
                    {
                        qf = reader.GetFloat(reader.GetOrdinal("qf"));
                    }
                    else
                    {
                        //lowest possible qf for this workload
                        qf = 0.00390625f;
                    }
                }
                );
        }
        public override float getIDF()
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

        protected override void calculateIDF()
        {
            DBManipulations.readTuples(
                String.Format(@"SELECT idf FROM {0}idf WHERE {0} = {1}", attributeName, _queryValue),
                delegate (SQLiteDataReader reader)
                {
                    //if there exists a tuple with value = queryvalue
                    if (reader.GetFloat(reader.GetOrdinal("idf")) != null)
                    {
                        idf = reader.GetFloat(reader.GetOrdinal("idf"));
                    }
                    else
                    {
                        //not important because when comparing to tuples the idf-similarity will be 0 regarding this attribute
                        //because it appears there are no tuples with attribute value = queryvalue
                        idf = 2.40654018f;
                    }
                }
                );
        }
    }
}

