using System;
using System.ComponentModel.DataAnnotations;

namespace BCTECHERP.Models
{
    public class StudentFeeReceiptModel
    {
        public int StudentFeeReceiptID { get; set; }
        public int StudentID { get; set; }
        public int BranchID { get; set; }

        [Required(ErrorMessage = "Please Enter Receipt No")]
        [StringLength(100)]
        public string FeeReceiptNo { get; set; }

        public DateTime ReceiptDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Please Enter Amount Received")]
        public decimal AmountReceived { get; set; }

        public int PaymentTypeID { get; set; }
        public int FeesTypeID { get; set; }
        public string TransactionNo { get; set; }
        public string BankName { get; set; }
        public string Remarks { get; set; }
        public int ReceivedByUserID { get; set; }
        public int FinancialYearID { get; set; } = 1;
        public DateTime CreatedDateTime { get; set; } = DateTime.Now;
        public int CreatedByUserID { get; set; } = 1;

        // Joined Display Properties
        public string StudentFullName { get; set; }
        public string StudentEnrollmentNo { get; set; }
        public string StudentMobileNo { get; set; }
        public string PaymentTypeTitle { get; set; }
        public string FeesTypeTitle { get; set; }
        public string ReceivedByUserName { get; set; }
        public string BranchName { get; set; }
        public string FormattedReceiptDate => ReceiptDate.ToString("dd/MM/yyyy hh:mm tt");
    }
}
