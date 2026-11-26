using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class PurchaseViewModel
    {
        // Purchase Master
        public int PurchaseInvoiceID { get; set; }
        public DateTime? DateAndTime { get; set; }
        public DateTime PurchaseInvoiceDate { get; set; }
        public string PurchaseInvoiceNo { get; set; }
        public string RefOrderNo { get; set; }
        public bool IsOutOfState { get; set; }
        public int PartyId { get; set; }
        public string PaymentTerm { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal GSTTax { get; set; }
        public decimal Amount { get; set; }
        public string NotesRemarks { get; set; }
        public decimal PurchaseBillDiscountRate { get; set; }
        public decimal PurchaseBillDiscountPercentage { get; set; }
        public decimal TotalAmount { get; set; }
        public int FinancialYearID { get; set; }
        public int BranchId { get; set; }
        public int UserId { get; set; }
        public decimal RoundOff { get; set; }

        // Purchase Details
        public List<PurchaseDetailModel> PurchaseDetails { get; set; }
            = new List<PurchaseDetailModel>();
    }

    public class PurchaseDetailModel
    {
        public int PurchaseInvoiceDetailID { get; set; }
        public int PurchaseInvoiceID { get; set; }

        public int MaterialID { get; set; }
        public string MaterialDescription { get; set; }
        public string HSNCode { get; set; }

        public decimal Qty { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal PurchaseDiscount { get; set; }
        public decimal PurchaseRateAfterDiscount { get; set; }

        public int GSTSLABId { get; set; }

        public decimal CGST { get; set; }
        public decimal CGSTRate { get; set; }

        public decimal SGST { get; set; }
        public decimal SGSTRate { get; set; }

        public decimal IGST { get; set; }
        public decimal IGSTPer { get; set; }

        public decimal Amount { get; set; }

        public int FinancialYearID { get; set; }
        public int BranchId { get; set; }

        public DateTime? CreatedDateTime { get; set; }
        public int CreatedByUserID { get; set; }
    }
    public class PurchaseListRequest
    {
        public string Search { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
    public class GstSlabModel
    {
        public int GSTSLABId { get; set; }
        public string GSTSLABName { get; set; }
        public decimal GST { get; set; }
        public decimal IGST { get; set; }
    }
}