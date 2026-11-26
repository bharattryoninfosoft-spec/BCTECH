using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class FunctionEventMaster
    {
        public int FunctionEventID { get; set; }
        public int BranchID { get; set; }
        public string BranchName { get; set; }

        [Required(ErrorMessage = "Please Enter Event Code")]
        [StringLength(50)]
        public string EventCode { get; set; }
        
        [Required(ErrorMessage = "Please Enter Event Title")]
        [StringLength(500)]
        public string EventTitle { get; set; }
        public string EventDates { get; set; }
        public string EventStartTimes { get; set; }
        public string EventEndTimes { get; set; }
        public DateTime? EventDate { get; set; }
        public TimeSpan? EventStartTime { get; set; }
        public TimeSpan? EventEndTime { get; set; }
        public string EventVenue { get; set; }

        public string EventDescription { get; set; }
        public int EventTypeID { get; set; }
        public string EventTypeName { get; set; }
        public string EventthumbnailImage { get; set; }

        public bool IsPublished { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string CreatedDateTimes { get; set; }
        public int CreatedByUserID { get; set; }
        public string EditEncryptedID { get; set; }
        public string ViewEncryptedID { get; set; }
    }
}