using System;

namespace BCTECHERP.Models
{
    public class StudentChargeMaster
    {
        public int StudentChargeID { get; set; }
        public string ChargeName { get; set; }
        public decimal ChargeAmount { get; set; }
        public string ChargeDescription { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedDateTime { get; set; }
        public int CreatedByUserID { get; set; }
        public string CreatedDateFormatted { get; set; }
    }
}
