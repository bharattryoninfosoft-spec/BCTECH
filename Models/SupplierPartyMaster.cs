using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class SupplierPartyMaster
    {
        public int PartyID { get; set; }
        public string PartyType { get; set; }
        public string CompanyName { get; set; }
        public string ContactPerson { get; set; }
        public string MobileNo { get; set; }
        public string AlternateMobileNo { get; set; }
        public string EmailAddress { get; set; }
        public string GSTNo { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Pincode { get; set; }
        public string BankName { get; set; }
        public string AccountNo { get; set; }
        public string IFSCCode { get; set; }
        public decimal OpeningBalance { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public int CreatedByUserID { get; set; }
    }
}