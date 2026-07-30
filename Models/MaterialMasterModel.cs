using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class MaterialMasterModel
    {
        public int MaterialID { get; set; }

        public int MaterialCategoryID { get; set; }
        public string CategoryName { get; set; }

        public string PeriodName { get; set; }

        public int UnitID { get; set; }
        public string UnitName { get; set; }

        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }

        public string Brand { get; set; }
        public string ModelNo { get; set; }
        public string Description { get; set; }

        public decimal PurchasePrice { get; set; }
        public decimal IssuePrice { get; set; }

        public decimal MinimumStock { get; set; }
        public decimal ReOrderLevel { get; set; }

        public string Barcode { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int CreatedByUserID { get; set; }

        public DateTime? EditedDateTime { get; set; }
        public int? EditedByUserID { get; set; }

        public bool IsDeleted { get; set; }
    }
}