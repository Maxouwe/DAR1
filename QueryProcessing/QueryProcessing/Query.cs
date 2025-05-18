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
        public List<Attribute> terms { get; }

        public Query(List<Attribute> terms)
        {
            this.terms = terms;
        }
    }
}
