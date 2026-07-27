using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class CourseModel
    {
        public int CourseID { get; set; }

        public string CourseCode { get; set; }

        public string CourseTitle { get; set; }

        public string CourseShortName { get; set; }

        public string CourseURL { get; set; }

        public string CourseImage { get; set; }

        public int Duration { get; set; }

        public string DurationType { get; set; }

        public decimal CourseFee { get; set; }

        public decimal RegistrationFee { get; set; }

        public decimal ExamFee { get; set; }

        public bool CertificateAvailable { get; set; }

        public bool IsMasterCourse { get; set; }

        public bool IsActive { get; set; }

        public string ShortDescription { get; set; }

        public string FullDescription { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? UpdatedDate { get; set; }
        public string EditEncryptedID { get; set; }
        public string ViewEncryptedID { get; set; }
        public string Remarks { get; set; }

    }
    public class GetCourseModel
    {
        public int CourseID { get; set; }
        public string CourseCode { get; set; }
        public string CourseTitle { get; set; }
        public string CourseShortName { get; set; }
        public string CourseURL { get; set; }
        public string CourseImage { get; set; }

        public int Duration { get; set; }
        public string DurationType { get; set; }

        public decimal CourseFee { get; set; }
        public decimal RegistrationFee { get; set; }
        public decimal ExamFee { get; set; }

        public bool CertificateAvailable { get; set; }
        public bool IsMasterCourse { get; set; }
        public bool IsActive { get; set; }

        public string ShortDescription { get; set; }
        public string FullDescription { get; set; }
        public string Remarks { get; set; }

        // Add this
        public List<int> MasterCourse { get; set; } = new List<int>();
    }
}