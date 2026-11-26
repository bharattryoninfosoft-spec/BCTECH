using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class StudentDocumentMaster
    {

        public int StudentDocumentID { get; set; }

        public DateTime DateAndTime { get; set; }

        [Required(ErrorMessage = "Please Enter Fees Type")]
        [StringLength(200)]
        public string StudentDocumentTitle { get; set; }

        public int UserID { get; set; }

    }
}