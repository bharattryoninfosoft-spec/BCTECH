using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class BranchMaster
    {
        public int BranchID { get; set; }

        [Required]
        public int OrganizationID { get; set; }

        [Required(ErrorMessage = "Branch Code is required.")]
        [StringLength(100)]
        public string BranchCode { get; set; }

        [Required(ErrorMessage = "Branch Name is required.")]
        [StringLength(200)]
        public string BranchName { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        public string Address { get; set; }

        [Required]
        [StringLength(100)]
        public string State { get; set; }

        [Required]
        [StringLength(100)]
        public string City { get; set; }

        [StringLength(12)]
        public string Pincode { get; set; }

        [StringLength(100)]
        public string Latitude { get; set; }

        [StringLength(100)]
        public string Longitude { get; set; }

        [Required]
        [StringLength(20)]
        public string MobileNo { get; set; }

        [StringLength(20)]
        public string AlternateMobileNo { get; set; }

        [EmailAddress]
        [StringLength(200)]
        public string EmailAddress { get; set; }

        [StringLength(100)]
        public string BankName { get; set; }

        [StringLength(100)]
        public string BankAccountNo { get; set; }

        [StringLength(100)]
        public string IFSCCode { get; set; }

        [StringLength(100)]
        public string UPIID { get; set; }

        public int BranchHeadUserID { get; set; }

        public bool IsHeadOffice { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public int CreatedByUserID { get; set; }

        public DateTime? EditedDateTime { get; set; }

        public int? EditedByUserID { get; set; }
    }
}