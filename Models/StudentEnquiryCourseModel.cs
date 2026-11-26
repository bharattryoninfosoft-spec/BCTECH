using System;
using System.ComponentModel.DataAnnotations;

namespace BCTechERP.Models
{
    /// <summary>
    /// Represents individual course line items associated with a Student Enquiry.
    /// </summary>
    public class StudentEnquiryCourseModel
    {
        public int StudentEnquiryCourseID { get; set; }

        public int StudentEnquiryID { get; set; }

        [Required(ErrorMessage = "Course selection is required")]
        public int CourseID { get; set; }

        public decimal CourseFee { get; set; }

        public decimal Discount { get; set; }

        public decimal FinalFee { get; set; }

        public string Remarks { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public int CreatedByUserID { get; set; }

        // Additional Display / Join Properties
        public string CourseName { get; set; }

        public StudentEnquiryCourseModel()
        {
            CourseFee = 0.00m;
            Discount = 0.00m;
            FinalFee = 0.00m;
        }
    }
}