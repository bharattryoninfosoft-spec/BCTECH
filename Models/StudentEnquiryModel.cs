using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BCTechERP.Models
{
    /// <summary>
    /// Represents the main Student Enquiry Master entity and transaction payload.
    /// </summary>
    public class StudentEnquiryModel
    {
        public int StudentEnquiryID { get; set; }

        public int OrganizationID { get; set; }

        [Required(ErrorMessage = "Branch is required")]
        public int BranchID { get; set; }

        public string EnquiryNo { get; set; }

        [Required(ErrorMessage = "Enquiry Date is required")]
        public string EnquiryDate { get; set; }

        [Required(ErrorMessage = "Enquiry Taken By User is required")]
        public int EnquiryTakenByUserID { get; set; }

        [Required(ErrorMessage = "Student Name is required")]
        [StringLength(150, ErrorMessage = "Student Name cannot exceed 150 characters")]
        public string StudentName { get; set; }

        [Required(ErrorMessage = "Mobile Number is required")]
        [StringLength(10, ErrorMessage = "Mobile Number must be 10 digits")]
        public string StudentMobileNo { get; set; }

        [StringLength(10)]
        public string WhatsAppNo { get; set; }

        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string StudentEmail { get; set; }

        public string DOB { get; set; }

        public string Gender { get; set; }

        public string BloodGroup { get; set; }

        public string EducationQualification { get; set; }

        public string SchoolCollegeName { get; set; }

        public string FatherName { get; set; }

        [StringLength(10)]
        public string FatherMobileNo { get; set; }

        [StringLength(10)]
        public string FatherAlternateMobileNo { get; set; }

        public string FatherOccupation { get; set; }

        [EmailAddress]
        public string FatherEmail { get; set; }

        public string MotherName { get; set; }

        [StringLength(10)]
        public string MotherMobileNo { get; set; }

        public string MotherOccupation { get; set; }

        [EmailAddress]
        public string MotherEmail { get; set; }

        public string Address { get; set; }

        public string AreaLocation { get; set; }

        public string City { get; set; }

        public string State { get; set; }

        public string Pincode { get; set; }

        public int EnquirySourceID { get; set; }

        public string ReferencePersonName { get; set; }

        public string ExpectedJoiningDate { get; set; }

        public string Remarks { get; set; }

        public bool IsFollowUpRequired { get; set; }

        public bool IsConvertedToAdmission { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public int CreatedByUserID { get; set; }

        // Additional Display / Helper Properties
        public string BranchName { get; set; }

        public string EnquiryTakenByUserName { get; set; }

        public string SourceName { get; set; }

        public string CourseNames { get; set; }

        public string EncryptedID { get; set; }
        public string EditEncryptedID { get; set; }
        public string ViewEncryptedID { get; set; }

        public List<StudentEnquiryCourseModel> CourseList { get; set; }

        public StudentEnquiryFollowUpModel FollowUpObj { get; set; }
        public bool IsClosed { get; set; }

        public StudentEnquiryModel()
        {
            CourseList = new List<StudentEnquiryCourseModel>();
            IsActive = true;
        }
    }

    /// <summary>
    /// Parameter class for listing and searching student enquiries with pagination.
    /// </summary>
    public class StudentEnquiryFilterParam
    {
        public string SearchText { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }

        public StudentEnquiryFilterParam()
        {
            PageNumber = 1;
            PageSize = 10;
        }
    }

    /// <summary>
    /// Response wrapper for paginated grid results.
    /// </summary>
    public class StudentEnquiryListResponse
    {
        public int TotalRecords { get; set; }
        public List<StudentEnquiryModel> Data { get; set; }

        public StudentEnquiryListResponse()
        {
            Data = new List<StudentEnquiryModel>();
        }
    }

    /// <summary>
    /// Standard API/AJAX Transaction Response model.
    /// </summary>
    public class ProcessResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public int PrimaryID { get; set; }

        public ProcessResult()
        {
            IsSuccess = false;
            Message = string.Empty;
        }
    }
}