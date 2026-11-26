using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class AdmissionTermCondMaster
    {
        public int AdmissionTermCondID { get; set; }
        
        [Required(ErrorMessage = "Please Enter Admission Term Cond")]
        [StringLength(200)]
        public string AdmissionTermCond { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public int CreatedByUserID { get; set; }

 

    }
}