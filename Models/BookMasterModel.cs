using System;
using System.ComponentModel.DataAnnotations;

namespace BCTECHERP.Models
{
    public class BookMasterModel
    {
        public int BookID { get; set; }

        public int OrganizationID { get; set; }
        public string OrganizationName { get; set; }

        public int BookCategoryID { get; set; }
        public string BookCategoryTitle { get; set; }

        public int BookSourceID { get; set; }
        public string BookSourceTitle { get; set; }

        public int CourseID { get; set; }
        public string CourseTitle { get; set; }

        public string BookCode { get; set; }

        [Required(ErrorMessage = "Please enter book title")]
        public string BookTitle { get; set; }

        public string BookSubTitle { get; set; }
        public string AuthorName { get; set; }
        public string PublisherName { get; set; }
        public decimal Price { get; set; }
        public int TotalPages { get; set; }
        public string RackNo { get; set; }
        public string BookCoverImage { get; set; }
        public byte IsEBook { get; set; }
        public string EBookFile { get; set; }
        public string BookDescription { get; set; }
        public int MinimumStock { get; set; }
        public byte IsActive { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string CreatedDateTimes { get; set; }
        public int CreatedByUserID { get; set; }
    }
}
