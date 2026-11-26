using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class FeeTermCondMaster
    {

        public int FeeTermCondID { get; set; }

        public string FeeTermCond { get; set; }

        public byte IsActive { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public int CreatedByUserID { get; set; }



    }
}