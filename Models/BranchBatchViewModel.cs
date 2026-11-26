using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class BranchBatchViewModel
    {
        public int BatchID { get; set; }

        [Required(ErrorMessage = "Please select Branch")]
        public int BranchID { get; set; }

        public string BranchName { get; set; }

        [Required(ErrorMessage = "Batch Title is required")]
        public string BatchTitle { get; set; }

        public TimeSpan? BatchTimeStart { get; set; }

        public TimeSpan? BatchTimeEnd { get; set; }

        public string BatchTimeDuration { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public int CreatedByUserID { get; set; }
    }
}