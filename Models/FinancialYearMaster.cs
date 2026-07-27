using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class FinancialYearMaster
    {
        public int FinancialYearID { get; set; }

        public string FinancialYear { get; set; }

        public string AssessmentYear { get; set; }

        public string StartDate { get; set; }
        public string EndDate { get; set; }

        public bool IsDefault { get; set; }
    }
}