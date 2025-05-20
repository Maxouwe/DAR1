using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.SQLite;
using System.Data.SqlClient;


namespace QueryProcessing
{

    class QueryProcessor
    {
        private List<Attribute> _query;
        private int _k;

        public QueryProcessor(List<Attribute> query, int k) 
        {
            _k = k;
            _query = query;
        }
       
    }
}
