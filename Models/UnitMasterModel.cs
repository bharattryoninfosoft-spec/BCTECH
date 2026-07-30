using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class UnitMasterModel
    {
        public int UnitID { get; set; }

        public string UnitName { get; set; }

        public string ShortName { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public int CreatedByUserID { get; set; }

        public bool IsDeleted { get; set; }
    }
}