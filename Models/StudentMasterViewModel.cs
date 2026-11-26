using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class StudentMasterViewModel
    {
        public int StudentID { get; set; }
        public int? StudentEnquiryID { get; set; }
        public int OrganizationID { get; set; } = 1;
        public int BranchID { get; set; }
        public int? BatchID { get; set; }
        public int AdmissionTakenByUserID { get; set; }
        public int FinancialYearID { get; set; } = 1;

        // Admission & Enrollment
        public DateTime AdminssionDateTime { get; set; } = DateTime.Now;
        public long StudentEnrollmentNo { get; set; }
        public string StudentStringEnrollmentNo { get; set; }

        // Personal Details
        public string StudentFullName { get; set; }
        public string StudentMobileNo { get; set; }
        public string WhatsAppNo { get; set; }
        public string StudentEmail { get; set; }
        public string StudentPhoto { get; set; }
        public string StudentAadhar { get; set; }
        public DateTime? DOB { get; set; }
        public string Gender { get; set; }
        public string BloodGroup { get; set; }
        public string EducationQualification { get; set; }
        public string SchoolCollegeName { get; set; }

        // Parent Details
        public string FatherName { get; set; }
        public string FatherMobileNo { get; set; }
        public string FatherAlternateMobileNo { get; set; }
        public string FatherOccupation { get; set; }
        public string FatherEmail { get; set; }
        public string MotherName { get; set; }
        public string MotherMobileNo { get; set; }
        public string MotherOccupation { get; set; }
        public string MotherEmail { get; set; }

        // Address Details
        public string Address { get; set; }
        public string AreaLocation { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Pincode { get; set; }

        // Identity Proof
        public string ProofOfIdentityType { get; set; }
        public string ProofOfIdentity { get; set; }
        public string ProofOfIdentityNumber { get; set; }

        // Status & Audit Fields
        public string Remarks { get; set; }
        public string AdmissionRemark { get; set; }
        public int? PaymentTypeID { get; set; } = 1;
        public string TransactionNo { get; set; }
        public bool IsGeneratediCard { get; set; } = false;
        public bool IsOldStudent { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDateTime { get; set; } = DateTime.Now;
        public int CreatedByUserID { get; set; }

        // Child Course Collection Grid
        public List<StudentCourseViewModel> CourseList { get; set; } = new List<StudentCourseViewModel>();

        // Child Installment Collection Grid
        public List<FeesInstallmentViewModel> InstallmentList { get; set; } = new List<FeesInstallmentViewModel>();

        // Child Document Collection Grid
        public List<StudentDocumentDetailViewModel> DocumentList { get; set; } = new List<StudentDocumentDetailViewModel>();
    }

    public class StudentCourseViewModel
    {
        public int StudentCourseID { get; set; }
        public int StudentID { get; set; }
        public int CourseID { get; set; }
        public decimal CourseFee { get; set; }
        public decimal Discount { get; set; }
        public decimal FinalFee { get; set; }
        public string Remarks { get; set; }
    }

    public class FeesInstallmentViewModel
    {
        public int FeesInstallmentID { get; set; }
        public int StudentID { get; set; }
        public string DueDate { get; set; }
        public DateTime? FeesDate { get; set; }
        public int? FeesTypeID { get; set; }
        public string FeesTitle { get; set; }
        public decimal Amount { get; set; }
        public decimal FeesAmount { get; set; }
        public int FinancialYearID { get; set; } = 1;
    }

    public class StudentDocumentDetailViewModel
    {
        public int StudentDocumentDetailID { get; set; }
        public int StudentID { get; set; }
        public int StudentDocumentID { get; set; }
        public string StudentDocumentTitle { get; set; }
        public string DocumentPath { get; set; }
        public string DocumentName { get; set; }
    }
}