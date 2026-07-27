using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class EnquirySourceMaster
    {
        public int EnquirySourceID { get; set; }
        public DateTime DateAndTime { get; set; }
        [Required(ErrorMessage = "Please Enter Enquiry Source.")]
        [StringLength(200)]
        public string EnquirySourceType { get; set; }
        public int UserID { get; set; }
    }
}