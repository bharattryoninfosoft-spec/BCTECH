using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    [Table("UserMaster")]
    public class UserMaster
    {
        public string EditEncryptedID { get; set; }
        public string ViewEncryptedID { get; set; }
        public string EncryptedID { get; set; }
        [Key]
        public int UserID { get; set; }

        [Required]
        [StringLength(100)]
        public string UserCategory { get; set; }

        [Required]
        [StringLength(200)]
        public string FirstName { get; set; }

        [StringLength(200)]
        public string MiddleName { get; set; }

        [Required]
        [StringLength(200)]
        public string LastName { get; set; }

        [Required]
        [StringLength(100)]
        public string Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [Required]
        [StringLength(100)]
        public string MobileNo { get; set; }

        [StringLength(100)]
        public string AlternateMobileNo { get; set; }

        [EmailAddress]
        [StringLength(100)]
        public string EmailAddress { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        [StringLength(100)]
        public string City { get; set; }

        [Required]
        [StringLength(100)]
        public string State { get; set; }

        [Required]
        [StringLength(12)]
        public string Pincode { get; set; }

        [StringLength(100)]
        public string ProofOfIdentityType { get; set; }

        public string ProofOfIdentity { get; set; }

        [Required]
        [StringLength(200)]
        public string Username { get; set; }

        [Required]
        [StringLength(200)]
        public string Password { get; set; }

        public DateTime? JoiningDate { get; set; }
        public string DateOfBirthText { get; set; }
        public string JoiningDateText { get; set; }
        public string ProfilePhoto { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public int CreatedByUserID { get; set; }

        public DateTime? EditedDateTime { get; set; }

        public int? EditedByUserID { get; set; }
    }
}