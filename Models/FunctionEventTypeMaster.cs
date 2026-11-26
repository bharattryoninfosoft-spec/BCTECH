using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class FunctionEventTypeMaster
    {

        public int EventTypeID { get; set; }

        [Required(ErrorMessage = "Please Enter Book Category")]
        [StringLength(200)]
        public string EventTypeName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string CreatedDateTimes { get; set; }
        public int CreatedByUserID { get; set; }

    }
}