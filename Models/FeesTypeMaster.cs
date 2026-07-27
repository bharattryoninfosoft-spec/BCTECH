using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class FeesTypeMaster
    {
        public int FeesTypeID { get; set; }

        public DateTime DateAndTime { get; set; }

        [Required(ErrorMessage = "Please Enter Fees Type")]
        [StringLength(200)]
        public string FeesTypeTitle { get; set; }

        public int UserID { get; set; }
    }
}