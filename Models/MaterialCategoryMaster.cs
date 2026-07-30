using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace BCTECHERP.Models
{
    public class MaterialCategoryMaster
    {
        [Key]
        public int MaterialCategoryID { get; set; }

        [Required(ErrorMessage = "Category Code is required.")]
        [StringLength(50)]
        public string CategoryCode { get; set; }

        [Required(ErrorMessage = "Category Name is required.")]
        [StringLength(200)]
        public string CategoryName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public int CreatedByUserID { get; set; }

        public DateTime? EditedDateTime { get; set; }

        public int? EditedByUserID { get; set; }
    }
}