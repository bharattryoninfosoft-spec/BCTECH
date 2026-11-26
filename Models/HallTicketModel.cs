using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace BCTECHERP.Models
{
    public class HallTicket
    {
        public int hall_ticket_id { get; set; }
        public DateTime dateandtime { get; set; }
        public int Schedule_Exam_Id { get; set; }
        public int student_id { get; set; }
        public int IsGivenExam { get; set; }
        public int SecureCode { get; set; }
        public string all_Student_id { get; set; }
        public DateTime PreviousExamDate { get; set; }
        public int branch_id { get; set; }
    }


    public class course
    {
        public int course_id { get; set; }
        public string course_title { get; set; }
    }

    public class ScheduleExam
    {
        public int Schedule_Exam_Id { get; set; }
        public string course_title { get; set; }
        public string Exam_schedule_Date { get; set; }
        public string Practical_Exam_Time { get; set; }
        public string Theory_Exam_Time { get; set; }
    }

    public class HallTicketExamViewModel
    {
        public int Schedule_Exam_Id { get; set; }
        public int? branch_id { get; set; }
        public string Branch { get; set; }
        public string branch_name { get; set; }
        public string branch_code { get; set; }
        public string Exam_schedule_Date { get; set; }
        public string Course { get; set; }
        public string course_title { get; set; }
        public string Practical_Exam_Time { get; set; }
        public string Theory_Exam_Time { get; set; }
        public string NoOfQuestion { get; set; }
        public string ExamTime { get; set; }
        public string ExamTotalMarks { get; set; }
        public string total { get; set; }
        public int No { get; set; }
    }

    public class SecureCode
    {
        public int Schedule_Exam_Id { get; set; }
        public DateTime Exam_schedule_Date { get; set; }
        public int No_Of_Question { get; set; }
        public int Exam_Time { get; set; }
        public int Exam_Total_Marks { get; set; }
        public string course_title { get; set; }
        public string course_image { get; set; }
        public string Practical_Exam_Time { get; set; }
        public string Theory_Exam_Time { get; set; }
        public int SecureCodeNo { get; set; }
    }
}
