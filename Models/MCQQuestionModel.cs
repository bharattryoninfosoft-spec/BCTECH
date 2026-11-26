using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace BCTECHERP.Models
{
    public class MCQQuestionModel
    {
        public int question_id { get; set; }
        public DateTime? dateandtime { get; set; }
        [AllowHtml]
        public string questions { get; set; }
        public int? course_id { get; set; }
        public byte? ISActive { get; set; }
    }

    public class MCQObjectiveModel
    {
        public int objective_id { get; set; }
        public DateTime dateandtime { get; set; }
        [AllowHtml]
        public string objectives { get; set; }
        public int question_id { get; set; }
    }

    public class MCQAnswerModel
    {
        public int answer_id { get; set; }
        public DateTime dateandtime { get; set; }
        public int question_id { get; set; }
        public int objective_id { get; set; }
        [AllowHtml]
        public string answer_description { get; set; }
    }

    public class MCQSaveDTO
    {
        public int question_id { get; set; }
        public int course_id { get; set; }
        [AllowHtml]
        public string questions { get; set; }
        [AllowHtml]
        public string txtOBA { get; set; }
        [AllowHtml]
        public string txtOBB { get; set; }
        [AllowHtml]
        public string txtOBC { get; set; }
        [AllowHtml]
        public string txtOBD { get; set; }
        public string DDLobjectives { get; set; } // 'A', 'B', 'C', 'D'
        [AllowHtml]
        public string txtansdesc { get; set; }
        public byte ISActive { get; set; } = 1;
    }

    public class MCQQuestionDetailDTO
    {
        public int question_id { get; set; }
        public DateTime? dateandtime { get; set; }
        public string questions { get; set; }
        public int? course_id { get; set; }
        public string CourseTitle { get; set; }
        public byte? ISActive { get; set; }

        public int OptionA_ID { get; set; }
        public string OptionA_Text { get; set; }

        public int OptionB_ID { get; set; }
        public string OptionB_Text { get; set; }

        public int OptionC_ID { get; set; }
        public string OptionC_Text { get; set; }

        public int OptionD_ID { get; set; }
        public string OptionD_Text { get; set; }

        public int answer_id { get; set; }
        public int objective_id { get; set; }
        public string CorrectOption { get; set; } // "A", "B", "C", "D"
        public string answer_description { get; set; }
    }
}
