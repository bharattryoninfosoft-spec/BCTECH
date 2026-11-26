using System;

namespace BCTECHERP.Models
{
    public class ScheduleExamMasterModel
    {
        public int ScheduleExamId { get; set; }
        public DateTime? dateandtime { get; set; }
        public int course_id { get; set; }
        public string CourseTitle { get; set; }
        public string ExamScheduleDate { get; set; }
        public string NoOfQuestion { get; set; }
        public string ExamTime { get; set; }
        public string ExamTotalMarks { get; set; }
        public string PracticalExamTime { get; set; }
        public string TheoryExamTime { get; set; }
        public string SecureCode { get; set; }
        public int? BranchID { get; set; }
        public string ExamType { get; set; }
    }
}
