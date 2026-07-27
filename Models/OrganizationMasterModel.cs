using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class OrganizationMasterModel
    {
        public int OrganizationID { get; set; }
        public string OrganizationName { get; set; }
        public string RegistrationNo { get; set; }
        public string OfficeAddress { get; set; }
        public string OfficeState { get; set; }
        public string OfficeCity { get; set; }
        public string OfficeMobileNo { get; set; }
        public string OfficialEmail { get; set; }
        public string OrganizationLogo { get; set; }
        public string OwnerName { get; set; }
        public string OwnerMobileNo { get; set; }
        public string OwnerEmail { get; set; }
    }
}