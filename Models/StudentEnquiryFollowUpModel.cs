using System;
using System.ComponentModel.DataAnnotations;

namespace BCTechERP.Models
{
    /// <summary>
    /// Represents student enquiry follow-up records and communication details.
    /// </summary>
    public class StudentEnquiryFollowUpModel
    {
        public int StudentEnquiryFollowUpID { get; set; }

        public int StudentEnquiryID { get; set; }

        [Required(ErrorMessage = "Follow-Up Date is required")]
        public string FollowUpDate { get; set; }

        public string NextFollowUpDate { get; set; }

        [Required(ErrorMessage = "Follow-Up Type is required")]
        public string FollowUpType { get; set; } // Call / WhatsApp / Visit / Email

        [Required(ErrorMessage = "Discussion details are required")]
        public string Discussion { get; set; }

        public string NextActionRemark { get; set; }

        [Required(ErrorMessage = "Follow-Up By User is required")]
        public int FollowUpByUserID { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public int CreatedByUserID { get; set; }

        // Additional Display / Join Properties
        public string FollowUpByUserName { get; set; }

        public StudentEnquiryFollowUpModel()
        {
            FollowUpType = "Call";
        }        
        public string EncryptedID { get; set; }
        public bool IsClosed { get; set; }
    }
    public class DropdownItem
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }
}