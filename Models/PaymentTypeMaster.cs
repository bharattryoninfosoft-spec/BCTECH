using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class PaymentTypeMaster
    {
        public int PaymentTypeID { get; set; }

        public DateTime DateAndTime { get; set; }

        [Required(ErrorMessage = "Please Enter Payment Type")]
        [StringLength(200)]
        public string PaymentTypeTitle { get; set; }

        public int UserID { get; set; }
    }
}
