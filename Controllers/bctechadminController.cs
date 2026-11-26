using BCTechERP.Models;
using BCTECHERP.Filters;
using BCTECHERP.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Runtime.Remoting.Contexts;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Web.Services.Description;
using System.Web.SessionState;
using System.Xml.Linq;
using static BCTECHERP.Controllers.bctechadminController;

namespace BCTECHERP.Controllers
{
    [CheckLogin]
    public class bctechadminController : Controller
    {
        string CS = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

        public class DbConnectionService
        {
            private string connectionString = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

            public DataTable Getdata(string query)
            {
                DataTable dt = new DataTable();
                try
                {
                    using (SqlConnection con = new SqlConnection(connectionString))
                    {
                        using (SqlCommand cmd = new SqlCommand(query, con))
                        {
                            using (SqlDataAdapter sda = new SqlDataAdapter(cmd))
                            {
                                sda.Fill(dt);
                            }
                        }
                    }
                }
                catch { }
                return dt;
            }

            public int updateRecord(string tableName, string setClause, string whereClause)
            {
                try
                {
                    using (SqlConnection con = new SqlConnection(connectionString))
                    {
                        con.Open();
                        using (SqlCommand cmd = new SqlCommand("UPDATE " + tableName + " SET " + setClause + " WHERE " + whereClause, con))
                        {
                            return cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch { return 0; }
            }
        }

        DbConnectionService cs = new DbConnectionService();

        private void notification()
        {
            // Placeholder for notification count
        }

        private string GetAdminId()
        {
            if (Request.Cookies["admin_id"] != null)
            {
                string val = Request.Cookies["admin_id"].Value;
                return val.Contains("=") ? val.Split('=')[1] : val;
            }
            if (Request.Cookies["UserID"] != null)
            {
                return Request.Cookies["UserID"].Value;
            }
            return "";
        }
        #region Common 
        [AllowAnonymous]
        public ActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public JsonResult Logout()
        {
            Session.Clear();
            Session.Abandon();

            string[] cookies =
            {
        "UserID",
        "UserName",
        "UserCategory"
    };

            foreach (string c in cookies)
            {
                if (Request.Cookies[c] != null)
                {
                    HttpCookie cookie = new HttpCookie(c);
                    cookie.Value = "";
                    cookie.Expires = DateTime.Now.AddDays(-1);

                    Response.Cookies.Add(cookie);
                }
            }

            Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            return Json("done");
        }
        [HttpPost]
        [AllowAnonymous]
        public JsonResult CheckLoginUsername(string username)
        {
            bool exists = false;

            string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM UserMaster
            WHERE Username=@Username
               OR EmailAddress=@Username
               OR MobileNo=@Username", con);

                cmd.Parameters.AddWithValue("@Username", username);

                exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }

            return Json(exists);
        }
        [HttpPost]
        [AllowAnonymous]
        public JsonResult GetLoginDetails(string username, string password)
        {
            string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                string encryptPassword = GetMD5(password);

                SqlCommand cmd = new SqlCommand(@"
                        SELECT UserID,
                               Username,
                               UserCategory,
                               IsActive
                        FROM UserMaster
                        WHERE
                        (
                            EmailAddress = @Username
                            OR MobileNo = @Username
                            OR Username = @Username
                        )
                        AND Password = @Password", con);

                cmd.Parameters.AddWithValue("@Username", username);
                cmd.Parameters.AddWithValue("@Password", encryptPassword);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    if (!(bool)dr["IsActive"])
                    {
                        return Json("deactivate");
                    }

                    HttpCookie userCookie = new HttpCookie("UserID");
                    userCookie.Value = dr["UserID"].ToString();
                    userCookie.Expires = DateTime.Now.AddDays(30);
                    Response.Cookies.Add(userCookie);

                    HttpCookie nameCookie = new HttpCookie("UserName");
                    nameCookie.Value = dr["Username"].ToString();
                    nameCookie.Expires = DateTime.Now.AddDays(30);
                    Response.Cookies.Add(nameCookie);

                    HttpCookie categoryCookie = new HttpCookie("UserCategory");
                    categoryCookie.Value = dr["UserCategory"].ToString();
                    categoryCookie.Expires = DateTime.Now.AddDays(30);
                    Response.Cookies.Add(categoryCookie);



                    return Json("done");
                }

                return Json("incorrect");
            }
        }
        [CheckLogin]
        public ActionResult Dashboard()
        {
            string userCategory = Request.Cookies["UserCategory"]?.Value ?? "";
            int userId = Convert.ToInt32(Request.Cookies["UserID"]?.Value ?? "0");

            if (string.IsNullOrEmpty(userCategory) && userId > 0)
            {
                try
                {
                    using (SqlConnection con = new SqlConnection(CS))
                    {
                        con.Open();
                        using (SqlCommand cmd = new SqlCommand("SELECT UserCategory FROM UserMaster WHERE UserID = @UserID", con))
                        {
                            cmd.Parameters.AddWithValue("@UserID", userId);
                            object cat = cmd.ExecuteScalar();
                            if (cat != null) userCategory = cat.ToString();
                        }
                    }
                }
                catch { }
            }

            bool isAdmin = string.Equals((userCategory ?? "").Trim(), "Admin", StringComparison.OrdinalIgnoreCase);
            ViewBag.IsAdmin = isAdmin;
            ViewBag.UserCategory = userCategory;

            return View();
        }

        [HttpPost]
        public JsonResult SetCurrentBranch(int branchId)
        {
            try
            {
                HttpCookie branchCookie = new HttpCookie("BranchID");
                branchCookie.Value = branchId.ToString();
                branchCookie.Expires = DateTime.Now.AddDays(30);
                branchCookie.Path = "/";
                Response.Cookies.Add(branchCookie);

                Session["BranchID"] = branchId;

                string branchName = "";
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT BranchName FROM BranchMaster WHERE BranchID = @BranchID", con))
                    {
                        cmd.Parameters.AddWithValue("@BranchID", branchId);
                        object bName = cmd.ExecuteScalar();
                        if (bName != null) branchName = bName.ToString();
                    }
                }

                HttpCookie nameCookie = new HttpCookie("BranchName");
                nameCookie.Value = branchName;
                nameCookie.Expires = DateTime.Now.AddDays(30);
                nameCookie.Path = "/";
                Response.Cookies.Add(nameCookie);

                return Json(new { Status = 1, BranchID = branchId, BranchName = branchName });
            }
            catch (Exception ex)
            {
                return Json(new { Status = 0, Message = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetDashboardBranchList()
        {
            try
            {
                string userCategory = Request.Cookies["UserCategory"]?.Value ?? "";
                int userId = Convert.ToInt32(Request.Cookies["UserID"]?.Value ?? "0");

                if (string.IsNullOrEmpty(userCategory) && userId > 0)
                {
                    try
                    {
                        using (SqlConnection con = new SqlConnection(CS))
                        {
                            con.Open();
                            using (SqlCommand cmd = new SqlCommand("SELECT UserCategory FROM UserMaster WHERE UserID = @UserID", con))
                            {
                                cmd.Parameters.AddWithValue("@UserID", userId);
                                object cat = cmd.ExecuteScalar();
                                if (cat != null) userCategory = cat.ToString();
                            }
                        }
                    }
                    catch { }
                }

                bool isAdmin = string.Equals((userCategory ?? "").Trim(), "Admin", StringComparison.OrdinalIgnoreCase);

                int currentBranchId = 0;
                if (Request.Cookies["BranchID"] != null)
                {
                    int.TryParse(Request.Cookies["BranchID"].Value, out currentBranchId);
                }
                if (currentBranchId == 0 && Session["BranchID"] != null)
                {
                    int.TryParse(Session["BranchID"].ToString(), out currentBranchId);
                }

                List<object> list = new List<object>();
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = @"
                        SELECT 
                            b.BranchID,
                            b.BranchCode,
                            b.BranchName,
                            b.Address,
                            b.City,
                            b.State,
                            b.MobileNo,
                            b.EmailAddress,
                            b.BranchHeadUserID,
                            COALESCE(
                                NULLIF(LTRIM(RTRIM(CONCAT(u.FirstName, ' ', u.LastName))), ''),
                                NULLIF(u.Username, ''),
                                (SELECT TOP 1 LTRIM(RTRIM(CONCAT(u3.FirstName, ' ', u3.LastName))) FROM UserMaster u3 WHERE u3.MobileNo = b.MobileNo AND u3.MobileNo IS NOT NULL AND u3.MobileNo <> ''),
                                (SELECT TOP 1 LTRIM(RTRIM(CONCAT(u2.FirstName, ' ', u2.LastName))) FROM BranchUserMaster bu INNER JOIN UserMaster u2 ON bu.UserID = u2.UserID WHERE bu.BranchID = b.BranchID),
                                b.City
                            ) AS BranchHeadName,
                            (SELECT COUNT(1) FROM StudentMaster WHERE BranchID = b.BranchID) AS TotalStudents
                        FROM BranchMaster b
                        LEFT JOIN UserMaster u ON b.BranchHeadUserID = u.UserID
                        WHERE ISNULL(b.IsActive, 1) = 1
                        ORDER BY b.BranchID ASC";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                int branchId = Convert.ToInt32(dr["BranchID"]);
                                list.Add(new
                                {
                                    BranchID = branchId,
                                    BranchCode = dr["BranchCode"]?.ToString() ?? "",
                                    BranchName = dr["BranchName"]?.ToString() ?? "",
                                    BranchHeadName = dr["BranchHeadName"]?.ToString() ?? "",
                                    City = dr["City"]?.ToString() ?? "",
                                    State = dr["State"]?.ToString() ?? "",
                                    MobileNo = dr["MobileNo"]?.ToString() ?? "",
                                    TotalStudents = Convert.ToInt32(dr["TotalStudents"]),
                                    IsSelected = (currentBranchId > 0 && currentBranchId == branchId),
                                    ViewEncryptedID = Encrypt(branchId.ToString() + "|V"),
                                    EditEncryptedID = Encrypt(branchId.ToString() + "|E")
                                });
                            }
                        }
                    }
                }

                return Json(new { Status = 1, IsAdmin = isAdmin, CurrentBranchID = currentBranchId, Data = list }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = 0, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        public JsonResult SendForgotOTP(string email)
        {
            try
            {
                string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

                using (SqlConnection con = new SqlConnection(cs))
                {
                    con.Open();

                    // Check User
                    SqlCommand checkCmd = new SqlCommand(@"
                SELECT UserID,EmailAddress
                FROM UserMaster
                WHERE EmailAddress=@Email", con);

                    checkCmd.Parameters.AddWithValue("@Email", email);

                    SqlDataReader dr = checkCmd.ExecuteReader();

                    if (!dr.Read())
                    {
                        dr.Close();
                        return Json(new
                        {
                            status = false,
                            message = "Email address not found."
                        });
                    }

                    dr.Close();

                    // Generate OTP
                    Random rd = new Random();
                    string otp = rd.Next(100000, 999999).ToString();

                    // Save OTP
                    SqlCommand updateCmd = new SqlCommand(@"
                UPDATE UserMaster
                SET ForgotOTP=@OTP,
                    OTPExpiry=DATEADD(MINUTE,10,GETDATE())
                WHERE EmailAddress=@Email", con);

                    updateCmd.Parameters.AddWithValue("@OTP", otp);
                    updateCmd.Parameters.AddWithValue("@Email", email);

                    updateCmd.ExecuteNonQuery();

                    // Mail Body
                    string body = $@"
                            <html>
                            <body style='font-family:Segoe UI;background:#f8f9fa;padding:20px;'>

                            <div style='max-width:600px;margin:auto;background:#ffffff;border-radius:10px;padding:30px;border:1px solid #dddddd;'>

                            <h2 style='color:#0d6efd'>
                            BC Tech Admin Portal
                            </h2>

                            <p>Hello,</p>

                            <p>You requested to reset your password.</p>

                            <p>Your verification code is:</p>

                            <h1 style='text-align:center;
                            color:#dc3545;
                            letter-spacing:8px'>
                            {otp}
                            </h1>

                            <p>
                            This OTP will expire in
                            <b>10 minutes</b>.
                            </p>

                            <p>
                            If you did not request this password reset,
                            please ignore this email.
                            </p>

                            <hr>

                            <p>
                            Thanks,<br/>
                            <b>BC Tech Team</b>
                            </p>

                            </div>

                            </body>
                            </html>";

                    MailMessage mail = new MailMessage();

                    mail.From = new MailAddress(
                        "info@bctech.org.in",
                        "BC Tech");

                    mail.To.Add(email);

                    mail.Subject = "BC Tech - Forgot Password OTP";

                    mail.Body = body;
                    mail.IsBodyHtml = true;

                    SmtpClient smtp = new SmtpClient();

                    smtp.Host = "plesk7200.is.cc";
                    smtp.Port = 587;
                    smtp.EnableSsl = true;
                    smtp.UseDefaultCredentials = false;

                    smtp.Credentials = new NetworkCredential(
                        "info@bctech.org.in",
                        "1w_840Dka");

                    smtp.Send(mail);

                    return Json(new
                    {
                        status = true,
                        message = "OTP sent successfully."
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }
        [AllowAnonymous]
        public ActionResult VerifyOTP(string email)
        {
            ViewBag.Email = email;
            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        public JsonResult VerifyOTP(string email, string otp)
        {
            string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand(@"
        SELECT COUNT(*)
        FROM UserMaster
        WHERE EmailAddress=@Email
        AND ForgotOTP=@OTP
        AND OTPExpiry>=GETDATE()", con);

                cmd.Parameters.AddWithValue("@Email", email);
                cmd.Parameters.AddWithValue("@OTP", otp);

                int count = Convert.ToInt32(cmd.ExecuteScalar());

                if (count > 0)
                {
                    return Json(new
                    {
                        status = true,
                        message = "OTP Verified"
                    });
                }

                return Json(new
                {
                    status = false,
                    message = "Invalid or Expired OTP."
                });
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public JsonResult UpdatePassword(string email, string password)
        {
            try
            {
                string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

                using (SqlConnection con = new SqlConnection(cs))
                {
                    con.Open();

                    SqlCommand cmd = new SqlCommand(@"
                UPDATE UserMaster
                SET Password=@Password,
                    ForgotOTP=NULL,
                    OTPExpiry=NULL
                WHERE EmailAddress=@Email", con);

                    cmd.Parameters.AddWithValue("@Password", GetMD5(password)); // Encrypt/Hash before saving in production
                    cmd.Parameters.AddWithValue("@Email", email);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Json(new
                        {
                            status = true,
                            message = "Password updated successfully."
                        });
                    }

                    return Json(new
                    {
                        status = false,
                        message = "Failed to update password."
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }
        [CheckLogin]
        public ActionResult MyProfile()
        {
            int userId = Convert.ToInt32(Request.Cookies["UserID"].Value);

            UserMasterModel model = new UserMasterModel();

            string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand(@"
        SELECT *
        FROM UserMaster
        WHERE UserID=@UserID", con);

                cmd.Parameters.AddWithValue("@UserID", userId);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    model.UserID = Convert.ToInt32(dr["UserID"]);
                    model.UserCategory = dr["UserCategory"].ToString();
                    model.FirstName = dr["FirstName"].ToString();
                    model.MiddleName = dr["MiddleName"].ToString();
                    model.LastName = dr["LastName"].ToString();
                    model.Gender = dr["Gender"].ToString();
                    model.DateOfBirth = Convert.ToDateTime(dr["DateOfBirth"]);
                    model.MobileNo = dr["MobileNo"].ToString();
                    model.AlternateMobileNo = dr["AlternateMobileNo"].ToString();
                    model.EmailAddress = dr["EmailAddress"].ToString();
                    model.Address = dr["Address"].ToString();
                    model.City = dr["City"].ToString();
                    model.State = dr["State"].ToString();
                    model.Pincode = dr["Pincode"].ToString();
                    model.Username = dr["Username"].ToString();
                    model.ProfilePhoto = dr["ProfilePhoto"].ToString();
                }
            }

            return View(model);
        }
        [HttpPost]
        public JsonResult UpdateMyProfile(UserMasterModel model, HttpPostedFileBase ProfilePhoto)
        {
            try
            {
                string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

                using (SqlConnection con = new SqlConnection(cs))
                {
                    con.Open();

                    // Upload Profile Photo
                    if (ProfilePhoto != null && ProfilePhoto.ContentLength > 0)
                    {
                        string folder = Server.MapPath("~/Content/Upload/UserProfilePhoto/");

                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        string fileName = "User_" + model.UserID + Path.GetExtension(ProfilePhoto.FileName);

                        string fullPath = Path.Combine(folder, fileName);

                        ProfilePhoto.SaveAs(fullPath);

                        model.ProfilePhoto = fileName;
                    }
                    else
                    {
                        SqlCommand photoCmd = new SqlCommand(
                            "SELECT ProfilePhoto FROM UserMaster WHERE UserID=@UserID", con);

                        photoCmd.Parameters.AddWithValue("@UserID", model.UserID);

                        object obj = photoCmd.ExecuteScalar();

                        model.ProfilePhoto = obj == null ? "" : obj.ToString();
                    }

                    SqlCommand cmd = new SqlCommand(@"
                                    UPDATE UserMaster SET

                                    FirstName=@FirstName,
                                    MiddleName=@MiddleName,
                                    LastName=@LastName,
                                    Gender=@Gender,
                                    DateOfBirth=@DateOfBirth,
                                    MobileNo=@MobileNo,
                                    AlternateMobileNo=@AlternateMobileNo,
                                    EmailAddress=@EmailAddress,
                                    Address=@Address,
                                    City=@City,
                                    State=@State,
                                    Pincode=@Pincode,
                                    ProfilePhoto=@ProfilePhoto

                                    WHERE UserID=@UserID", con);

                    cmd.Parameters.AddWithValue("@UserID", model.UserID);
                    cmd.Parameters.AddWithValue("@FirstName", model.FirstName);
                    cmd.Parameters.AddWithValue("@MiddleName", model.MiddleName);
                    cmd.Parameters.AddWithValue("@LastName", model.LastName);
                    cmd.Parameters.AddWithValue("@Gender", model.Gender);
                    cmd.Parameters.AddWithValue("@DateOfBirth", model.DateOfBirth);
                    cmd.Parameters.AddWithValue("@MobileNo", model.MobileNo);
                    cmd.Parameters.AddWithValue("@AlternateMobileNo", model.AlternateMobileNo);
                    cmd.Parameters.AddWithValue("@EmailAddress", model.EmailAddress);
                    cmd.Parameters.AddWithValue("@Address", model.Address);
                    cmd.Parameters.AddWithValue("@City", model.City);
                    cmd.Parameters.AddWithValue("@State", model.State);
                    cmd.Parameters.AddWithValue("@Pincode", model.Pincode);
                    cmd.Parameters.AddWithValue("@ProfilePhoto", model.ProfilePhoto);

                    cmd.ExecuteNonQuery();

                    // Update cookie if name changed
                    if (Request.Cookies["UserName"] != null)
                    {
                        HttpCookie cookie = Request.Cookies["UserName"];
                        cookie.Value = model.FirstName;
                        cookie.Expires = DateTime.Now.AddDays(30);
                        Response.Cookies.Add(cookie);
                    }

                    return Json("done");
                }
            }
            catch (Exception ex)
            {
                return Json(ex.Message);
            }
        }
        public static string Encrypt(string text)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }
        public static string Decrypt(string cipher)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cipher))
                    return "0";

                // Replace spaces back to '+' (lost during URL transmission)
                cipher = cipher.Replace(" ", "+");

                // Fix Base64 padding (Length must be a multiple of 4)
                switch (cipher.Length % 4)
                {
                    case 2: cipher += "=="; break;
                    case 3: cipher += "="; break;
                }

                byte[] base64EncodedBytes = Convert.FromBase64String(cipher);
                return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
            }
            catch
            {
                // If cipher is a plain number or invalid string, return as-is or fallback
                return cipher;
            }
        }
        public static string GetMD5(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();

                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }
        #endregion Common 
        #region OrganizationMaster
        public ActionResult OrganizationMaster()
        {
            ViewData["ParentMenu"] = "Utility";
            ViewData["PageTitle"] = "Organization Master";
            return View();
        }
        [HttpGet]
        public JsonResult GetOrganization()
        {
            var conn = ConfigurationManager.ConnectionStrings["con"];

            if (conn == null)
            {
                throw new Exception("Connection string 'con' not found.");
            }

            string cs = conn.ConnectionString;
            using (SqlConnection con = new SqlConnection(cs))
            {
                SqlCommand cmd = new SqlCommand("SELECT TOP 1 * FROM OrganizationMaster", con);
                con.Open();
                SqlDataReader dr = cmd.ExecuteReader();
                if (dr.Read())
                {
                    return Json(new
                    {
                        OrganizationID = dr["OrganizationID"],
                        OrganizationName = dr["OrganizationName"].ToString(),
                        RegistrationNo = dr["RegistrationNo"].ToString(),
                        OfficeAddress = dr["OfficeAddress"].ToString(),
                        OfficeState = dr["OfficeState"].ToString(),
                        OfficeCity = dr["OfficeCity"].ToString(),
                        OfficeMobileNo = dr["OfficeMobileNo"].ToString(),
                        OfficialEmail = dr["OfficialEmail"].ToString(),
                        OrganizationLogo = dr["OrganizationLogo"].ToString(),
                        OwnerName = dr["OwnerName"].ToString(),
                        OwnerMobileNo = dr["OwnerMobileNo"].ToString(),
                        OwnerEmail = dr["OwnerEmail"].ToString()
                    }, JsonRequestBehavior.AllowGet);
                }
            }

            return Json(null, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult UpdateOrganization(OrganizationMasterModel model)
        {
            string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

            using (SqlConnection con = new SqlConnection(cs))
            {
                SqlCommand cmd = new SqlCommand(@"
                            UPDATE OrganizationMaster
                            SET
                                OrganizationName=@OrganizationName,
                                RegistrationNo=@RegistrationNo,
                                OfficeAddress=@OfficeAddress,
                                OfficeState=@OfficeState,
                                OfficeCity=@OfficeCity,
                                OfficeMobileNo=@OfficeMobileNo,
                                OfficialEmail=@OfficialEmail,
                                OrganizationLogo=@OrganizationLogo,
                                OwnerName=@OwnerName,
                                OwnerMobileNo=@OwnerMobileNo,
                                OwnerEmail=@OwnerEmail,
                                EditedDateTime=GETDATE(),
                                EditedByUserID=1
                            WHERE OrganizationID=@OrganizationID", con);

                cmd.Parameters.AddWithValue("@OrganizationID", model.OrganizationID);
                cmd.Parameters.AddWithValue("@OrganizationName", model.OrganizationName);
                cmd.Parameters.AddWithValue("@RegistrationNo", model.RegistrationNo);
                cmd.Parameters.AddWithValue("@OfficeAddress", model.OfficeAddress);
                cmd.Parameters.AddWithValue("@OfficeState", model.OfficeState);
                cmd.Parameters.AddWithValue("@OfficeCity", model.OfficeCity);
                cmd.Parameters.AddWithValue("@OfficeMobileNo", model.OfficeMobileNo);
                cmd.Parameters.AddWithValue("@OfficialEmail", model.OfficialEmail);
                cmd.Parameters.AddWithValue("@OrganizationLogo", model.OrganizationLogo);
                cmd.Parameters.AddWithValue("@OwnerName", model.OwnerName);
                cmd.Parameters.AddWithValue("@OwnerMobileNo", model.OwnerMobileNo);
                cmd.Parameters.AddWithValue("@OwnerEmail", model.OwnerEmail);

                con.Open();
                int i = cmd.ExecuteNonQuery();

                return Json(i > 0, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult GetOrganizationList()
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                SqlDataAdapter da = new SqlDataAdapter(
                    "SELECT OrganizationID, OrganizationName FROM OrganizationMaster ORDER BY OrganizationName",
                    con);

                DataTable dt = new DataTable();
                da.Fill(dt);

                var list = dt.AsEnumerable().Select(r => new
                {
                    OrganizationID = Convert.ToInt32(r["OrganizationID"]),
                    OrganizationName = r["OrganizationName"].ToString()
                }).ToList();

                return Json(new
                {
                    Status = 1,
                    Data = list
                }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult SaveOrganization(OrganizationMasterModel model)
        {
            try
            {
                string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

                using (SqlConnection con = new SqlConnection(cs))
                {
                    con.Open();
                    SqlCommand cmd = new SqlCommand();
                    cmd.Connection = con;

                    if (model.OrganizationID == 0)
                    {
                        // INSERT
                        cmd.CommandText = @"
                    INSERT INTO OrganizationMaster
                    (
                        OrganizationName,
                        RegistrationNo,
                        OfficeAddress,
                        OfficeState,
                        OfficeCity,
                        OfficeMobileNo,
                        OfficialEmail,
                        OrganizationLogo,
                        OwnerName,
                        OwnerMobileNo,
                        OwnerEmail,
                        CreatedDateTime,
                        CreatedByUserID
                    )
                    VALUES
                    (
                        @OrganizationName,
                        @RegistrationNo,
                        @OfficeAddress,
                        @OfficeState,
                        @OfficeCity,
                        @OfficeMobileNo,
                        @OfficialEmail,
                        @OrganizationLogo,
                        @OwnerName,
                        @OwnerMobileNo,
                        @OwnerEmail,
                        GETDATE(),
                        @CreatedByUserID
                    )";

                        cmd.Parameters.AddWithValue("@CreatedByUserID", "1");
                    }
                    else
                    {
                        // UPDATE
                        cmd.CommandText = @"
                    UPDATE OrganizationMaster SET
                        OrganizationName = @OrganizationName,
                        RegistrationNo   = @RegistrationNo,
                        OfficeAddress    = @OfficeAddress,
                        OfficeState      = @OfficeState,
                        OfficeCity       = @OfficeCity,
                        OfficeMobileNo   = @OfficeMobileNo,
                        OfficialEmail    = @OfficialEmail,
                        OrganizationLogo = @OrganizationLogo,
                        OwnerName        = @OwnerName,
                        OwnerMobileNo    = @OwnerMobileNo,
                        OwnerEmail       = @OwnerEmail,
                        EditedDateTime   = GETDATE(),
                        EditedByUserID   = @EditedByUserID
                    WHERE OrganizationID = @OrganizationID";

                        cmd.Parameters.AddWithValue("@OrganizationID", model.OrganizationID);
                        cmd.Parameters.AddWithValue("@EditedByUserID", "1");
                    }

                    cmd.Parameters.AddWithValue("@OrganizationName", model.OrganizationName ?? "");
                    cmd.Parameters.AddWithValue("@RegistrationNo", model.RegistrationNo ?? "");
                    cmd.Parameters.AddWithValue("@OfficeAddress", model.OfficeAddress ?? "");
                    cmd.Parameters.AddWithValue("@OfficeState", model.OfficeState ?? "");
                    cmd.Parameters.AddWithValue("@OfficeCity", model.OfficeCity ?? "");
                    cmd.Parameters.AddWithValue("@OfficeMobileNo", model.OfficeMobileNo ?? "");
                    cmd.Parameters.AddWithValue("@OfficialEmail", model.OfficialEmail ?? "");
                    cmd.Parameters.AddWithValue("@OrganizationLogo", model.OrganizationLogo ?? "");
                    cmd.Parameters.AddWithValue("@OwnerName", model.OwnerName ?? "");
                    cmd.Parameters.AddWithValue("@OwnerMobileNo", model.OwnerMobileNo ?? "");
                    cmd.Parameters.AddWithValue("@OwnerEmail", model.OwnerEmail ?? "");

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Json(new
                        {
                            Status = 1,
                            Message = model.OrganizationID == 0
                                ? "Organization Created Successfully."
                                : "Organization Updated Successfully."
                        });
                    }
                    else
                    {
                        return Json(new
                        {
                            Status = 0,
                            Message = "Record not saved."
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
        }
        #endregion
        #region BranchMaster
        [CheckLogin]
        public ActionResult BranchMaster()
        {
            ViewData["ParentMenu"] = "Utility";
            ViewData["PageTitle"] = "Branch Master";
            return View();
        }
        public JsonResult GetBranchList()
        {
            string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand(@"
                        SELECT
                            B.*,
                            U.UserID,
                            U.Username,
                            U.ProfilePhoto
                        FROM BranchMaster B
                        LEFT JOIN BranchUserMaster BU
                            ON B.BranchID = BU.BranchID
                        LEFT JOIN UserMaster U
                            ON BU.UserID = U.UserID
                        WHERE B.IsActive = 1
                        ORDER BY B.BranchName, U.Username", con);

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);

                var list = dt.AsEnumerable()
                    .GroupBy(r => Convert.ToInt32(r["BranchID"]))
                    .Select(g => new
                    {
                        BranchID = Convert.ToInt32(g.First()["BranchID"]),
                        BranchCode = g.First()["BranchCode"].ToString(),
                        BranchName = g.First()["BranchName"].ToString(),
                        Address = g.First()["Address"].ToString(),
                        City = g.First()["City"].ToString(),
                        State = g.First()["State"].ToString(),
                        MobileNo = g.First()["MobileNo"].ToString(),
                        EmailAddress = g.First()["EmailAddress"].ToString(),
                        IsActive = Convert.ToBoolean(g.First()["IsActive"]),

                        EditEncryptedID = Encrypt(g.First()["BranchID"].ToString() + "|E"),
                        ViewEncryptedID = Encrypt(g.First()["BranchID"].ToString() + "|V"),
                        EncryptedBranchID = Encrypt(g.First()["BranchID"].ToString()),

                        Staff = g
                            .Where(x => x["UserID"] != DBNull.Value)
                            .Select(x => new
                            {
                                UserID = Convert.ToInt32(x["UserID"]),
                                Username = x["Username"].ToString(),
                                ProfilePhoto = x["ProfilePhoto"] == DBNull.Value ? "" : x["ProfilePhoto"].ToString()
                            }).ToList()
                    }).ToList();

                return Json(new
                {
                    Status = 1,
                    Data = list
                }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult GetStaffList(int BranchID)
        {
            List<object> list = new List<object>();

            string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = @"
                               SELECT
                            U.UserID,
                            U.Username,
                            U.MobileNo,
                            U.EmailAddress,
                            U.ProfilePhoto,
                            CASE
                                WHEN BU.UserID IS NULL THEN 0
                                ELSE 1
                            END AS IsAssigned
                        FROM UserMaster U
                        LEFT JOIN BranchUserMaster BU
                            ON U.UserID = BU.UserID
                           AND BU.BranchID = @BranchID
                        WHERE U.UserCategory='Staff'
                          AND U.IsActive=1
                        ORDER BY U.Username";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@BranchID", BranchID);

                con.Open();

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    list.Add(new
                    {
                        UserID = Convert.ToInt32(dr["UserID"]),
                        Username = dr["Username"].ToString(),
                        MobileNo = dr["MobileNo"].ToString(),
                        EmailAddress = dr["EmailAddress"].ToString(),
                        ProfilePhoto = dr["ProfilePhoto"].ToString(),
                        IsAssigned = Convert.ToBoolean(dr["IsAssigned"])
                    });
                }
            }

            return Json(new
            {
                Status = 1,
                Data = list
            }, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public JsonResult GetBranchHeadUsers()
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                string query = @"SELECT UserID, Username
                         FROM UserMaster
                         WHERE UserCategory = 'Branch Head'
                         ORDER BY Username";

                SqlCommand cmd = new SqlCommand(query, con);
                con.Open();

                SqlDataReader dr = cmd.ExecuteReader();

                List<object> list = new List<object>();

                while (dr.Read())
                {
                    list.Add(new
                    {
                        UserID = dr["UserID"],
                        Username = dr["Username"].ToString()
                    });
                }

                con.Close();

                return Json(list, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult CreateBranchMaster(string id)
        {
            int branchId = 0;
            string mode = "";

            if (!string.IsNullOrEmpty(id))
            {
                string value = Decrypt(id);

                string[] arr = value.Split('|');

                branchId = Convert.ToInt32(arr[0]);
                mode = arr[1];
            }
            ViewBag.branchId = branchId > 0 ? Encrypt(branchId.ToString()) : "";
            ViewBag.Mode = mode;
            ViewData["ParentMenu"] = "Utility";
            ViewData["SubMenu"] = "Branch Master";
            ViewData["SubMenuUrl"] = Url.Action("BranchMaster", "bctechadmin");
            switch (mode)
            {
                case "E":
                    ViewData["PageTitle"] = "Edit Branch";
                    break;

                case "V":
                    ViewData["PageTitle"] = "View Branch";
                    break;

                default:
                    ViewData["PageTitle"] = "Create Branch";
                    break;
            }
            return View();
        }
        [HttpGet]
        public JsonResult GetBranchById(string id)
        {
            try
            {
                int BranchID = Convert.ToInt32(Decrypt(id));

                string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

                using (SqlConnection con = new SqlConnection(cs))
                {
                    con.Open();

                    SqlCommand cmd = new SqlCommand("SELECT * FROM BranchMaster WHERE BranchID=@BranchID", con);
                    cmd.Parameters.AddWithValue("@BranchID", BranchID);

                    SqlDataReader dr = cmd.ExecuteReader();

                    if (dr.Read())
                    {
                        BranchMaster model = new BranchMaster();

                        model.BranchID = Convert.ToInt32(dr["BranchID"]);
                        model.OrganizationID = Convert.ToInt32(dr["OrganizationID"]);
                        model.BranchCode = dr["BranchCode"].ToString();
                        model.BranchName = dr["BranchName"].ToString();
                        model.Address = dr["Address"].ToString();
                        model.State = dr["State"].ToString();
                        model.City = dr["City"].ToString();
                        model.Pincode = dr["Pincode"].ToString();
                        model.Latitude = dr["Latitude"].ToString();
                        model.Longitude = dr["Longitude"].ToString();
                        model.MobileNo = dr["MobileNo"].ToString();
                        model.AlternateMobileNo = dr["AlternateMobileNo"].ToString();
                        model.EmailAddress = dr["EmailAddress"].ToString();
                        model.BankName = dr["BankName"].ToString();
                        model.BankAccountNo = dr["BankAccountNo"].ToString();
                        model.IFSCCode = dr["IFSCCode"].ToString();
                        model.UPIID = dr["UPIID"].ToString();
                        model.BranchHeadUserID = Convert.ToInt32(dr["BranchHeadUserID"]);
                        model.IsHeadOffice = Convert.ToBoolean(dr["IsHeadOffice"]);
                        model.IsActive = Convert.ToBoolean(dr["IsActive"]);

                        dr.Close();

                        return Json(new
                        {
                            Status = 1,
                            Data = model
                        }, JsonRequestBehavior.AllowGet);
                    }

                    return Json(new
                    {
                        Status = 0,
                        Message = "Branch not found."
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult DeleteBranch(int BranchID)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    SqlCommand cmd = new SqlCommand(@"
                UPDATE BranchMaster 
                SET IsActive = 0, 
                    EditedDateTime = GETDATE(), 
                    EditedByUserID = @UserID
                WHERE BranchID = @BranchID", con);

                    cmd.Parameters.AddWithValue("@BranchID", BranchID);

                    // Read from Cookie safely
                    string userId = Request.Cookies["UserID"]?.Value;

                    // Handle potential null/empty cookie value using DBNull.Value
                    cmd.Parameters.AddWithValue("@UserID", string.IsNullOrEmpty(userId) ? (object)DBNull.Value : userId);

                    int i = cmd.ExecuteNonQuery();

                    if (i > 0)
                    {
                        return Json(new
                        {
                            Status = 1,
                            Message = "Branch deleted successfully."
                        });
                    }

                    return Json(new
                    {
                        Status = 0,
                        Message = "Delete failed."
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
        }
        [HttpPost]
        public JsonResult SaveBranchMaster(BranchMaster model)
        {
            try
            {
                string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

                using (SqlConnection con = new SqlConnection(cs))
                {
                    con.Open();

                    SqlCommand cmd = new SqlCommand();
                    cmd.Connection = con;

                    if (model.BranchID == 0)
                    {
                        // INSERT
                        cmd.CommandText = @"
                INSERT INTO BranchMaster
                (
                    OrganizationID,
                    BranchCode,
                    BranchName,
                    Address,
                    State,
                    City,
                    Pincode,
                    Latitude,
                    Longitude,
                    MobileNo,
                    AlternateMobileNo,
                    EmailAddress,
                    BankName,
                    BankAccountNo,
                    IFSCCode,
                    UPIID,
                    BranchHeadUserID,
                    IsHeadOffice,
                    IsActive,
                    CreatedDateTime,
                    CreatedByUserID
                )
                VALUES
                (
                    @OrganizationID,
                    @BranchCode,
                    @BranchName,
                    @Address,
                    @State,
                    @City,
                    @Pincode,
                    @Latitude,
                    @Longitude,
                    @MobileNo,
                    @AlternateMobileNo,
                    @EmailAddress,
                    @BankName,
                    @BankAccountNo,
                    @IFSCCode,
                    @UPIID,
                    @BranchHeadUserID,
                    @IsHeadOffice,
                    @IsActive,
                    GETDATE(),
                    @CreatedByUserID
                )";

                        cmd.Parameters.AddWithValue("@CreatedByUserID", "1");
                    }
                    else
                    {
                        // UPDATE
                        cmd.CommandText = @"
                UPDATE BranchMaster SET

                    OrganizationID      = @OrganizationID,
                    BranchCode         = @BranchCode,
                    BranchName         = @BranchName,
                    Address            = @Address,
                    State              = @State,
                    City               = @City,
                    Pincode            = @Pincode,
                    Latitude           = @Latitude,
                    Longitude          = @Longitude,
                    MobileNo           = @MobileNo,
                    AlternateMobileNo  = @AlternateMobileNo,
                    EmailAddress       = @EmailAddress,
                    BankName           = @BankName,
                    BankAccountNo      = @BankAccountNo,
                    IFSCCode           = @IFSCCode,
                    UPIID              = @UPIID,
                    BranchHeadUserID   = @BranchHeadUserID,
                    IsHeadOffice       = @IsHeadOffice,
                    IsActive           = @IsActive,
                    EditedDateTime     = GETDATE(),
                    EditedByUserID     = @EditedByUserID

                WHERE BranchID=@BranchID";

                        cmd.Parameters.AddWithValue("@BranchID", model.BranchID);
                        cmd.Parameters.AddWithValue("@EditedByUserID", "1");
                    }

                    cmd.Parameters.AddWithValue("@OrganizationID", model.OrganizationID);
                    cmd.Parameters.AddWithValue("@BranchCode", model.BranchCode);
                    cmd.Parameters.AddWithValue("@BranchName", model.BranchName);
                    cmd.Parameters.AddWithValue("@Address", model.Address);
                    cmd.Parameters.AddWithValue("@State", model.State);
                    cmd.Parameters.AddWithValue("@City", model.City);
                    cmd.Parameters.AddWithValue("@Pincode", model.Pincode ?? "");
                    cmd.Parameters.AddWithValue("@Latitude", model.Latitude ?? "");
                    cmd.Parameters.AddWithValue("@Longitude", model.Longitude ?? "");
                    cmd.Parameters.AddWithValue("@MobileNo", model.MobileNo);
                    cmd.Parameters.AddWithValue("@AlternateMobileNo", model.AlternateMobileNo ?? "");
                    cmd.Parameters.AddWithValue("@EmailAddress", model.EmailAddress ?? "");
                    cmd.Parameters.AddWithValue("@BankName", model.BankName ?? "");
                    cmd.Parameters.AddWithValue("@BankAccountNo", model.BankAccountNo ?? "");
                    cmd.Parameters.AddWithValue("@IFSCCode", model.IFSCCode ?? "");
                    cmd.Parameters.AddWithValue("@UPIID", model.UPIID ?? "");
                    cmd.Parameters.AddWithValue("@BranchHeadUserID", model.BranchHeadUserID == 0 ? (object)DBNull.Value : model.BranchHeadUserID);
                    cmd.Parameters.AddWithValue("@IsHeadOffice", model.IsHeadOffice);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Json(new
                        {
                            Status = 1,
                            Message = model.BranchID == 0
                                ? "Branch Created Successfully."
                                : "Branch Updated Successfully."
                        });
                    }
                    else
                    {
                        return Json(new
                        {
                            Status = 0,
                            Message = "Record not saved."
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
        }
        [HttpPost]
        public JsonResult AssignStaff(int BranchID, int[] StaffIDs)
        {
            string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                // 1. Delete Unchecked Staff
                if (StaffIDs != null && StaffIDs.Length > 0)
                {
                    string ids = string.Join(",", StaffIDs);

                    SqlCommand deleteCmd = new SqlCommand(@"
                DELETE FROM BranchUserMaster
                WHERE BranchID = @BranchID
                AND UserID NOT IN (" + ids + ")", con);

                    deleteCmd.Parameters.AddWithValue("@BranchID", BranchID);
                    deleteCmd.ExecuteNonQuery();
                }
                else
                {
                    // Agar kuch bhi select nahi kiya to sab delete
                    SqlCommand deleteAll = new SqlCommand(@"
                DELETE FROM BranchUserMaster
                WHERE BranchID=@BranchID", con);

                    deleteAll.Parameters.AddWithValue("@BranchID", BranchID);
                    deleteAll.ExecuteNonQuery();
                }

                // 2. Insert New Staff
                if (StaffIDs != null)
                {
                    foreach (int userId in StaffIDs)
                    {
                        SqlCommand checkCmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM BranchUserMaster
                    WHERE BranchID=@BranchID
                    AND UserID=@UserID", con);

                        checkCmd.Parameters.AddWithValue("@BranchID", BranchID);
                        checkCmd.Parameters.AddWithValue("@UserID", userId);

                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count == 0)
                        {
                            SqlCommand insertCmd = new SqlCommand(@"
                        INSERT INTO BranchUserMaster
                        (BranchID,UserID)
                        VALUES
                        (@BranchID,@UserID)", con);

                            insertCmd.Parameters.AddWithValue("@BranchID", BranchID);
                            insertCmd.Parameters.AddWithValue("@UserID", userId);

                            insertCmd.ExecuteNonQuery();
                        }
                    }
                }
            }

            return Json(new
            {
                Status = 1,
                Message = "Staff updated successfully."
            });
        }
        #endregion
        #region UserMaster
        public ActionResult UserMaster()
        {
            ViewData["ParentMenu"] = "Utility";
            ViewData["PageTitle"] = "User Master";

            List<string> userCategories = new List<string>();
            try
            {
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = "SELECT DISTINCT UserCategory FROM UserMaster WHERE UserCategory IS NOT NULL AND LTRIM(RTRIM(UserCategory)) <> '' ORDER BY UserCategory DESC";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                userCategories.Add(dr["UserCategory"].ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }

            ViewBag.UserCategoryList = userCategories;
            return View();
        }

        [HttpGet]
        public JsonResult GetUserCategoryList()
        {
            try
            {
                List<string> list = new List<string>();
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = "SELECT DISTINCT UserCategory FROM UserMaster WHERE UserCategory IS NOT NULL AND LTRIM(RTRIM(UserCategory)) <> '' ORDER BY UserCategory DESC";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(dr["UserCategory"].ToString());
                            }
                        }
                    }
                }
                return Json(new { Status = 1, Data = list }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = 0, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult CreateUser(string id)
        {
            int userId = 0;
            string mode = "";
            if (!string.IsNullOrWhiteSpace(id))
            {
                try
                {
                    string decryptValue = Decrypt(id);

                    if (!string.IsNullOrWhiteSpace(decryptValue))
                    {
                        string[] arr = decryptValue.Split('|');

                        if (arr.Length >= 2)
                        {
                            int.TryParse(arr[0], out userId);
                            mode = arr[1];
                        }
                    }
                }
                catch
                {
                    return RedirectToAction("UserMaster");
                }
            }
            ViewBag.UserID = userId > 0 ? Encrypt(userId.ToString()) : "";
            ViewBag.Mode = mode;
            ViewData["ParentMenu"] = "Utility";
            ViewData["SubMenu"] = "User Master";
            ViewData["SubMenuUrl"] = Url.Action("UserMaster", "bctechadmin");
            switch (mode)
            {
                case "E":
                    ViewData["PageTitle"] = "Edit User";
                    break;

                case "V":
                    ViewData["PageTitle"] = "View User";
                    break;

                default:
                    ViewData["PageTitle"] = "Create User";
                    break;
            }
            return View();
        }
        [HttpGet]
        public JsonResult GetUserByID(string UserID)
        {
            int id = Convert.ToInt32(Decrypt(UserID));
            UserMaster obj = new UserMaster();
            string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;
            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand("SELECT * FROM UserMaster WHERE UserID=@UserID", con);
                cmd.Parameters.AddWithValue("@UserID", id);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    obj.UserID = Convert.ToInt32(dr["UserID"]);
                    obj.UserCategory = dr["UserCategory"].ToString();
                    obj.FirstName = dr["FirstName"].ToString();
                    obj.MiddleName = dr["MiddleName"].ToString();
                    obj.LastName = dr["LastName"].ToString();
                    obj.Gender = dr["Gender"].ToString();
                    obj.DateOfBirth = dr["DateOfBirth"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(dr["DateOfBirth"]);
                    obj.MobileNo = dr["MobileNo"].ToString();
                    obj.AlternateMobileNo = dr["AlternateMobileNo"].ToString();
                    obj.EmailAddress = dr["EmailAddress"].ToString();
                    obj.Address = dr["Address"].ToString();
                    obj.City = dr["City"].ToString();
                    obj.State = dr["State"].ToString();
                    obj.Pincode = dr["Pincode"].ToString();
                    obj.ProofOfIdentityType = dr["ProofOfIdentityType"].ToString();
                    obj.ProofOfIdentity = dr["ProofOfIdentity"].ToString();
                    obj.Username = dr["Username"].ToString();
                    obj.Password = dr["Password"].ToString();
                    obj.DateOfBirthText = dr["DateOfBirth"] == DBNull.Value ? "" : Convert.ToDateTime(dr["DateOfBirth"]).ToString("yyyy-MM-dd");
                    obj.JoiningDateText = dr["JoiningDate"] == DBNull.Value ? "" : Convert.ToDateTime(dr["JoiningDate"]).ToString("yyyy-MM-dd");
                    obj.JoiningDate = dr["JoiningDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(dr["JoiningDate"]);
                    obj.ProfilePhoto = dr["ProfilePhoto"].ToString();
                    obj.IsActive = Convert.ToBoolean(dr["IsActive"]);
                }
            }

            return Json(new
            {
                Status = 1,
                Data = obj
            }, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public JsonResult GetUserMasterList()
        {
            try
            {
                List<UserMaster> list = new List<UserMaster>();

                string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

                string loginUserCategory = Request.Cookies["UserCategory"]?.Value ?? "";
                int loginUserID = Convert.ToInt32(Request.Cookies["UserID"]?.Value ?? "0");

                using (SqlConnection con = new SqlConnection(cs))
                {
                    con.Open();

                    SqlCommand cmd = new SqlCommand();
                    cmd.Connection = con;

                    if (loginUserCategory == "Admin")
                    {
                        cmd.CommandText = @"
                    SELECT *
                    FROM UserMaster
                    ORDER BY UserID DESC";
                    }
                    else if (loginUserCategory == "Branch Head")
                    {
                        cmd.CommandText = @"
                    SELECT DISTINCT U.*
                    FROM UserMaster U
                    INNER JOIN BranchUserMaster BU
                        ON U.UserID = BU.UserID
                    INNER JOIN BranchMaster B
                        ON BU.BranchID = B.BranchID
                    WHERE B.BranchHeadUserID = @LoginUserID
                      AND U.UserCategory = 'Staff'
                    ORDER BY U.UserID DESC";

                        cmd.Parameters.AddWithValue("@LoginUserID", loginUserID);
                    }
                    else
                    {
                        cmd.CommandText = @"
                    SELECT *
                    FROM UserMaster
                    WHERE 1 = 0";
                    }

                    SqlDataReader dr = cmd.ExecuteReader();

                    while (dr.Read())
                    {
                        list.Add(new UserMaster
                        {
                            EncryptedID = Encrypt(dr["UserID"].ToString()),
                            EditEncryptedID = Encrypt(dr["UserID"].ToString() + "|edit"),
                            ViewEncryptedID = Encrypt(dr["UserID"].ToString() + "|view"),

                            UserID = Convert.ToInt32(dr["UserID"]),
                            UserCategory = dr["UserCategory"].ToString(),
                            FirstName = dr["FirstName"].ToString(),
                            MiddleName = dr["MiddleName"].ToString(),
                            LastName = dr["LastName"].ToString(),
                            Gender = dr["Gender"].ToString(),
                            DateOfBirth = dr["DateOfBirth"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(dr["DateOfBirth"]),

                            MobileNo = dr["MobileNo"].ToString(),
                            AlternateMobileNo = dr["AlternateMobileNo"].ToString(),
                            EmailAddress = dr["EmailAddress"].ToString(),
                            Address = dr["Address"].ToString(),
                            City = dr["City"].ToString(),
                            State = dr["State"].ToString(),
                            Pincode = dr["Pincode"].ToString(),

                            ProofOfIdentityType = dr["ProofOfIdentityType"].ToString(),
                            ProofOfIdentity = dr["ProofOfIdentity"].ToString(),

                            Username = dr["Username"].ToString(),
                            Password = dr["Password"].ToString(),

                            JoiningDate = dr["JoiningDate"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(dr["JoiningDate"]),

                            ProfilePhoto = dr["ProfilePhoto"].ToString(),
                            IsActive = Convert.ToBoolean(dr["IsActive"])
                        });
                    }
                }

                return Json(new
                {
                    Status = 1,
                    Data = list
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult DeleteUser(int UserID)
        {
            string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;
            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("DELETE FROM UserMaster WHERE UserID=@UserID", con);
                cmd.Parameters.AddWithValue("@UserID", UserID);
                cmd.ExecuteNonQuery();
            }

            return Json(new
            {
                Status = 1,
                Message = "User Deleted Successfully."
            });
        }
        [HttpPost]
        public JsonResult SaveUserMaster(UserMaster obj, HttpPostedFileBase ProfilePhoto, HttpPostedFileBase ProofOfIdentity)
        {
            try
            {
                string cs = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

                using (SqlConnection con = new SqlConnection(cs))
                {
                    con.Open();
                    string loginUserCategory = Session["UserCategory"]?.ToString() ?? "";

                    // Branch Head sirf Staff create/update kar sakta hai
                    if (loginUserCategory == "Branch Head" &&
                        obj.UserCategory != "Staff")
                    {
                        return Json(new
                        {
                            Status = 0,
                            Message = "You are not authorized to create Admin or Branch Head users."
                        });
                    }

                    // Staff ko User Create/Edit ki permission nahi
                    if (loginUserCategory == "Staff")
                    {
                        return Json(new
                        {
                            Status = 0,
                            Message = "You are not authorized to create users."
                        });
                    }
                    //===========================
                    // Profile Photo Upload
                    //===========================
                    if (ProfilePhoto != null && ProfilePhoto.ContentLength > 0)
                    {
                        string ext = Path.GetExtension(ProfilePhoto.FileName);

                        string fileName = Guid.NewGuid().ToString() + ext;

                        string folder = Server.MapPath("~/Content/Upload/UserProfilePhoto/");

                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        ProfilePhoto.SaveAs(Path.Combine(folder, fileName));

                        obj.ProfilePhoto = fileName;
                    }

                    //===========================
                    // Proof Upload
                    //===========================
                    if (ProofOfIdentity != null && ProofOfIdentity.ContentLength > 0)
                    {
                        string ext = Path.GetExtension(ProofOfIdentity.FileName);

                        string fileName = Guid.NewGuid().ToString() + ext;

                        string folder = Server.MapPath("~/Content/Upload/UserProofOfIdentity/");

                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        ProofOfIdentity.SaveAs(Path.Combine(folder, fileName));

                        obj.ProofOfIdentity = fileName;
                    }

                    if (obj.UserID == 0)
                    {
                        //-------------------------
                        // INSERT
                        //-------------------------
                        SqlCommand cmd = new SqlCommand(@"
INSERT INTO UserMaster
(
UserCategory,
FirstName,
MiddleName,
LastName,
Gender,
DateOfBirth,
MobileNo,
AlternateMobileNo,
EmailAddress,
Address,
City,
State,
Pincode,
ProofOfIdentityType,
ProofOfIdentity,
Username,
Password,
JoiningDate,
ProfilePhoto,
IsActive,
CreatedDateTime,
CreatedByUserID
)
VALUES
(
@UserCategory,
@FirstName,
@MiddleName,
@LastName,
@Gender,
@DateOfBirth,
@MobileNo,
@AlternateMobileNo,
@EmailAddress,
@Address,
@City,
@State,
@Pincode,
@ProofOfIdentityType,
@ProofOfIdentity,
@Username,
@Password,
@JoiningDate,
@ProfilePhoto,
@IsActive,
Getdate(),
@CreatedByUserID
)", con);

                        cmd.Parameters.AddWithValue("@UserCategory", obj.UserCategory);
                        cmd.Parameters.AddWithValue("@FirstName", obj.FirstName);
                        cmd.Parameters.AddWithValue("@MiddleName", obj.MiddleName ?? "");
                        cmd.Parameters.AddWithValue("@LastName", obj.LastName);
                        cmd.Parameters.AddWithValue("@Gender", obj.Gender);
                        cmd.Parameters.AddWithValue("@DateOfBirth", (object)obj.DateOfBirth ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@MobileNo", obj.MobileNo);
                        cmd.Parameters.AddWithValue("@AlternateMobileNo", obj.AlternateMobileNo ?? "");
                        cmd.Parameters.AddWithValue("@EmailAddress", obj.EmailAddress ?? "");
                        cmd.Parameters.AddWithValue("@Address", obj.Address);
                        cmd.Parameters.AddWithValue("@City", obj.City);
                        cmd.Parameters.AddWithValue("@State", obj.State);
                        cmd.Parameters.AddWithValue("@Pincode", obj.Pincode);
                        cmd.Parameters.AddWithValue("@ProofOfIdentityType", obj.ProofOfIdentityType ?? "");
                        cmd.Parameters.AddWithValue("@ProofOfIdentity", obj.ProofOfIdentity ?? "");
                        cmd.Parameters.AddWithValue("@Username", obj.Username);
                        cmd.Parameters.AddWithValue("@Password", GetMD5(obj.Password));
                        cmd.Parameters.AddWithValue("@JoiningDate", (object)obj.JoiningDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ProfilePhoto", obj.ProfilePhoto ?? "");
                        cmd.Parameters.AddWithValue("@IsActive", obj.IsActive);
                        cmd.Parameters.AddWithValue("@CreatedByUserID", "1");

                        cmd.ExecuteNonQuery();

                        return Json(new
                        {
                            Status = 1,
                            Message = "User Saved Successfully."
                        });
                    }
                    else
                    {
                        //-------------------------
                        // UPDATE
                        //-------------------------

                        if (string.IsNullOrEmpty(obj.ProfilePhoto))
                        {
                            SqlCommand img = new SqlCommand("SELECT ProfilePhoto FROM UserMaster WHERE UserID=@UserID", con);
                            img.Parameters.AddWithValue("@UserID", obj.UserID);
                            obj.ProfilePhoto = Convert.ToString(img.ExecuteScalar());
                        }

                        if (string.IsNullOrEmpty(obj.ProofOfIdentity))
                        {
                            SqlCommand proof = new SqlCommand("SELECT ProofOfIdentity FROM UserMaster WHERE UserID=@UserID", con);
                            proof.Parameters.AddWithValue("@UserID", obj.UserID);
                            obj.ProofOfIdentity = Convert.ToString(proof.ExecuteScalar());
                        }

                        SqlCommand cmd = new SqlCommand(@"
                                    UPDATE UserMaster SET

                                    UserCategory=@UserCategory,
                                    FirstName=@FirstName,
                                    MiddleName=@MiddleName,
                                    LastName=@LastName,
                                    Gender=@Gender,
                                    DateOfBirth=@DateOfBirth,
                                    MobileNo=@MobileNo,
                                    AlternateMobileNo=@AlternateMobileNo,
                                    EmailAddress=@EmailAddress,
                                    Address=@Address,
                                    City=@City,
                                    State=@State,
                                    Pincode=@Pincode,
                                    ProofOfIdentityType=@ProofOfIdentityType,
                                    ProofOfIdentity=@ProofOfIdentity,
                                    Username=@Username,
                                    Password=@Password,
                                    JoiningDate=@JoiningDate,
                                    ProfilePhoto=@ProfilePhoto,
                                    IsActive=@IsActive
                                    WHERE UserID=@UserID", con);

                        cmd.Parameters.AddWithValue("@UserID", obj.UserID);
                        cmd.Parameters.AddWithValue("@UserCategory", obj.UserCategory);
                        cmd.Parameters.AddWithValue("@FirstName", obj.FirstName);
                        cmd.Parameters.AddWithValue("@MiddleName", obj.MiddleName ?? "");
                        cmd.Parameters.AddWithValue("@LastName", obj.LastName);
                        cmd.Parameters.AddWithValue("@Gender", obj.Gender);
                        cmd.Parameters.AddWithValue("@DateOfBirth", (object)obj.DateOfBirth ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@MobileNo", obj.MobileNo);
                        cmd.Parameters.AddWithValue("@AlternateMobileNo", obj.AlternateMobileNo ?? "");
                        cmd.Parameters.AddWithValue("@EmailAddress", obj.EmailAddress ?? "");
                        cmd.Parameters.AddWithValue("@Address", obj.Address);
                        cmd.Parameters.AddWithValue("@City", obj.City);
                        cmd.Parameters.AddWithValue("@State", obj.State);
                        cmd.Parameters.AddWithValue("@Pincode", obj.Pincode);
                        cmd.Parameters.AddWithValue("@ProofOfIdentityType", obj.ProofOfIdentityType ?? "");
                        cmd.Parameters.AddWithValue("@ProofOfIdentity", obj.ProofOfIdentity ?? "");
                        cmd.Parameters.AddWithValue("@Username", obj.Username);
                        string password;

                        if (string.IsNullOrWhiteSpace(obj.Password))
                        {
                            SqlCommand pass = new SqlCommand("SELECT Password FROM UserMaster WHERE UserID=@UserID", con);
                            pass.Parameters.AddWithValue("@UserID", obj.UserID);
                            password = Convert.ToString(pass.ExecuteScalar());
                        }
                        else
                        {
                            password = GetMD5(obj.Password);
                        }

                        cmd.Parameters.AddWithValue("@Password", password);
                        cmd.Parameters.AddWithValue("@JoiningDate", (object)obj.JoiningDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ProfilePhoto", obj.ProfilePhoto ?? "");
                        cmd.Parameters.AddWithValue("@IsActive", obj.IsActive);

                        cmd.ExecuteNonQuery();

                        return Json(new
                        {
                            Status = 1,
                            Message = "User Updated Successfully."
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
        }
        #endregion
        #region FinancialYearMaster
        public ActionResult FinancialYearMaster()
        {
            ViewData["ParentMenu"] = "Utility";
            ViewData["PageTitle"] = "FinancialYear Master";
            return View();
        }
        [HttpGet]
        public JsonResult GetFinancialYearList()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand(@"SELECT FinancialYearID,
                                                 FinancialYear,
                                                 AssessmentYear,
                                                 StartDate,
                                                 EndDate,
                                                 IsDefault
                                          FROM FinancialYearMaster
                                          ORDER BY FinancialYearID DESC", con);

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    list.Add(new
                    {
                        FinancialYearID = dr["FinancialYearID"],
                        FinancialYear = dr["FinancialYear"].ToString(),
                        AssessmentYear = dr["AssessmentYear"].ToString(),
                        StartDate = Convert.ToDateTime(dr["StartDate"]).ToString("dd/MM/yyyy"),
                        EndDate = Convert.ToDateTime(dr["EndDate"]).ToString("dd/MM/yyyy"),
                        IsDefault = Convert.ToBoolean(dr["IsDefault"])
                    });
                }
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult SaveFinancialYear(FinancialYearMaster model)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                // Duplicate Check
                SqlCommand checkCmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM FinancialYearMaster
            WHERE FinancialYear = @FinancialYear
              AND FinancialYearID <> @FinancialYearID", con);

                checkCmd.Parameters.AddWithValue("@FinancialYear", model.FinancialYear);
                checkCmd.Parameters.AddWithValue("@FinancialYearID", model.FinancialYearID);

                int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Financial Year already exists."
                    });
                }

                // Set Default
                if (model.IsDefault)
                {
                    SqlCommand cmd1 = new SqlCommand(
                        "UPDATE FinancialYearMaster SET IsDefault = 0", con);
                    cmd1.ExecuteNonQuery();
                }

                if (model.FinancialYearID == 0)
                {
                    SqlCommand cmd = new SqlCommand(@"
                INSERT INTO FinancialYearMaster
                (
                    FinancialYear,
                    AssessmentYear,
                    StartDate,
                    EndDate,
                    IsDefault
                )
                VALUES
                (
                    @FinancialYear,
                    @AssessmentYear,
                    @StartDate,
                    @EndDate,
                    @IsDefault
                )", con);

                    cmd.Parameters.AddWithValue("@FinancialYear", model.FinancialYear);
                    cmd.Parameters.AddWithValue("@AssessmentYear", model.AssessmentYear);
                    cmd.Parameters.AddWithValue("@StartDate", model.StartDate);
                    cmd.Parameters.AddWithValue("@EndDate", model.EndDate);
                    cmd.Parameters.AddWithValue("@IsDefault", model.IsDefault);

                    cmd.ExecuteNonQuery();
                }
                else
                {
                    SqlCommand cmd = new SqlCommand(@"
                UPDATE FinancialYearMaster
                SET
                    FinancialYear = @FinancialYear,
                    AssessmentYear = @AssessmentYear,
                    StartDate = @StartDate,
                    EndDate = @EndDate,
                    IsDefault = @IsDefault
                WHERE FinancialYearID = @FinancialYearID", con);

                    cmd.Parameters.AddWithValue("@FinancialYearID", model.FinancialYearID);
                    cmd.Parameters.AddWithValue("@FinancialYear", model.FinancialYear);
                    cmd.Parameters.AddWithValue("@AssessmentYear", model.AssessmentYear);
                    cmd.Parameters.AddWithValue("@StartDate", model.StartDate);
                    cmd.Parameters.AddWithValue("@EndDate", model.EndDate);
                    cmd.Parameters.AddWithValue("@IsDefault", model.IsDefault);

                    cmd.ExecuteNonQuery();
                }

                return Json(new
                {
                    Status = true,
                    Message = "Financial Year saved successfully."
                });
            }
        }
        [HttpGet]
        public JsonResult GetFinancialYearByID(int id)
        {
            FinancialYearMaster model = new FinancialYearMaster();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand(
                    "SELECT * FROM FinancialYearMaster WHERE FinancialYearID=@ID", con);

                cmd.Parameters.AddWithValue("@ID", id);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    model.FinancialYearID = Convert.ToInt32(dr["FinancialYearID"]);
                    model.FinancialYear = dr["FinancialYear"].ToString();
                    model.AssessmentYear = dr["AssessmentYear"].ToString();
                    model.StartDate = Convert.ToDateTime(dr["StartDate"]).ToString("yyyy-MM-dd");
                    model.EndDate = Convert.ToDateTime(dr["EndDate"]).ToString("yyyy-MM-dd");
                    model.IsDefault = Convert.ToBoolean(dr["IsDefault"]);
                }
            }

            return Json(model, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult DeleteFinancialYear(int id)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                // Check Default Financial Year
                SqlCommand checkCmd = new SqlCommand(
                    "SELECT IsDefault FROM FinancialYearMaster WHERE FinancialYearID=@FinancialYearID", con);

                checkCmd.Parameters.AddWithValue("@FinancialYearID", id);

                object result = checkCmd.ExecuteScalar();

                if (result != null && Convert.ToBoolean(result))
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Default Financial Year cannot be deleted."
                    });
                }

                SqlCommand cmd = new SqlCommand(
                    "DELETE FROM FinancialYearMaster WHERE FinancialYearID=@FinancialYearID", con);

                cmd.Parameters.AddWithValue("@FinancialYearID", id);

                cmd.ExecuteNonQuery();

                return Json(new
                {
                    Status = true,
                    Message = "Financial Year deleted successfully."
                });
            }
        }
        #endregion
        #region FeesTypeMaster
        public ActionResult FeesTypeMaster()
        {
            ViewData["ParentMenu"] = "Utility";
            ViewData["PageTitle"] = "FeesType Master";
            return View();
        }
        [HttpGet]
        public JsonResult GetFeesTypeList()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"SELECT
                            FeesTypeID,
                            CONVERT(varchar(10),DateAndTime,103) AS DateAndTime,
                            FeesTypeTitle,
                            UserID
                         FROM FeesTypeMaster
                         ORDER BY FeesTypeID DESC";

                SqlCommand cmd = new SqlCommand(query, con);

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    list.Add(new
                    {
                        FeesTypeID = Convert.ToInt32(dr["FeesTypeID"]),
                        DateAndTime = dr["DateAndTime"].ToString(),
                        FeesTypeTitle = dr["FeesTypeTitle"].ToString(),
                        UserID = Convert.ToInt32(dr["UserID"])
                    });
                }
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult SaveFeesType(FeesTypeMaster model)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                // Duplicate Check
                SqlCommand checkCmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM FeesTypeMaster
            WHERE FeesTypeTitle = @FeesTypeTitle
              AND FeesTypeID <> @FeesTypeID", con);

                checkCmd.Parameters.AddWithValue("@FeesTypeTitle", model.FeesTypeTitle);
                checkCmd.Parameters.AddWithValue("@FeesTypeID", model.FeesTypeID);

                int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Fees Type already exists."
                    });
                }

                if (model.FeesTypeID == 0)
                {
                    string query = @"INSERT INTO FeesTypeMaster
                            (
                                DateAndTime,
                                FeesTypeTitle,
                                UserID
                            )
                            VALUES
                            (
                                GETDATE(),
                                @FeesTypeTitle,
                                @UserID
                            )";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@FeesTypeTitle", model.FeesTypeTitle);
                    cmd.Parameters.AddWithValue("@UserID", 1);

                    cmd.ExecuteNonQuery();
                }
                else
                {
                    string query = @"UPDATE FeesTypeMaster
                             SET FeesTypeTitle = @FeesTypeTitle
                             WHERE FeesTypeID = @FeesTypeID";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@FeesTypeID", model.FeesTypeID);
                    cmd.Parameters.AddWithValue("@FeesTypeTitle", model.FeesTypeTitle);

                    cmd.ExecuteNonQuery();
                }
            }

            return Json(new
            {
                Status = true,
                Message = "Fees Type saved successfully."
            });
        }
        [HttpGet]
        public JsonResult GetFeesTypeByID(int id)
        {
            FeesTypeMaster model = new FeesTypeMaster();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"SELECT *
                         FROM FeesTypeMaster
                         WHERE FeesTypeID=@FeesTypeID";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@FeesTypeID", id);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    model.FeesTypeID = Convert.ToInt32(dr["FeesTypeID"]);
                    model.FeesTypeTitle = dr["FeesTypeTitle"].ToString();
                    model.UserID = Convert.ToInt32(dr["UserID"]);
                }
            }

            return Json(model, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult DeleteFeesType(int id)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"DELETE FROM FeesTypeMaster
                         WHERE FeesTypeID=@FeesTypeID";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@FeesTypeID", id);

                cmd.ExecuteNonQuery();
            }

            return Json("Success");
        }
        #endregion



        #region Student Document
        public ActionResult StudentDocumentMaster()
        {
            ViewData["ParentMenu"] = "Utility";
            ViewData["PageTitle"] = "Student Document master";
            return View();
        }
        [HttpGet]
        public JsonResult GetStudentDocumentList()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"SELECT
                            StudentDocumentID,
                            CONVERT(varchar(10),DateAndTime,103) AS DateAndTime,
                            StudentDocumentTitle,
                            UserID
                         FROM StudentDocumentMaster
                         ORDER BY StudentDocumentID DESC";

                SqlCommand cmd = new SqlCommand(query, con);

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    list.Add(new
                    {
                        StudentDocumentID = Convert.ToInt32(dr["StudentDocumentID"]),
                        DateAndTime = dr["DateAndTime"].ToString(),
                        StudentDocumentTitle = dr["StudentDocumentTitle"].ToString(),
                        UserID = Convert.ToInt32(dr["UserID"])
                    });
                }
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult SaveStudentDocument(StudentDocumentMaster model)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                // Duplicate Check
                SqlCommand checkCmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM StudentDocumentMaster
            WHERE StudentDocumentTitle = @StudentDocumentTitle
              AND StudentDocumentID <> @StudentDocumentID", con);

                checkCmd.Parameters.AddWithValue("@StudentDocumentTitle", model.StudentDocumentTitle);
                checkCmd.Parameters.AddWithValue("@StudentDocumentID", model.StudentDocumentID);

                int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Student Document already exists."
                    });
                }

                if (model.StudentDocumentID == 0)
                {
                    string query = @"INSERT INTO StudentDocumentMaster
                            (
                                DateAndTime,
                                StudentDocumentTitle,
                                UserID
                            )
                            VALUES
                            (
                                GETDATE(),
                                @StudentDocumentTitle,
                                @UserID
                            )";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@StudentDocumentTitle", model.StudentDocumentTitle);
                    cmd.Parameters.AddWithValue("@UserID", 1);

                    cmd.ExecuteNonQuery();
                }
                else
                {
                    string query = @"UPDATE StudentDocumentMaster
                             SET StudentDocumentTitle = @StudentDocumentTitle
                             WHERE StudentDocumentID = @StudentDocumentID";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@StudentDocumentID", model.StudentDocumentID);
                    cmd.Parameters.AddWithValue("@StudentDocumentTitle", model.StudentDocumentTitle);

                    cmd.ExecuteNonQuery();
                }
            }

            return Json(new
            {
                Status = true,
                Message = "Student Document saved successfully."
            });
        }
        [HttpGet]
        public JsonResult GetStudentDocumentByID(int id)
        {
            StudentDocumentMaster model = new StudentDocumentMaster();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"SELECT *
                         FROM StudentDocumentMaster
                         WHERE StudentDocumentID=@StudentDocumentID";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@StudentDocumentID", id);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    model.StudentDocumentID = Convert.ToInt32(dr["StudentDocumentID"]);
                    model.StudentDocumentTitle = dr["StudentDocumentTitle"].ToString();
                    model.UserID = Convert.ToInt32(dr["UserID"]);
                }
            }

            return Json(model, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult DeleteStudentDocument(int id)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"DELETE FROM StudentDocumentMaster
                         WHERE StudentDocumentID=@StudentDocumentID";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@StudentDocumentID", id);

                cmd.ExecuteNonQuery();
            }

            return Json("Success");
        }
        #endregion





        #region AdmissionTermCondMaster

        public ActionResult AdmissionTermCondMaster()
        {
            ViewData["ParentMenu"] = "Utility";
            ViewData["PageTitle"] = "Admission Term & Condition Master";

            return View();
        }


        // =====================================================
        // GET: Admission Term & Condition List
        // =====================================================

        [HttpGet]
        public JsonResult GetAdmissionTermCondList()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(
                ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"
            SELECT
                AdmissionTermCondID,
                AdmissionTermCond,
                IsActive,
                CONVERT(varchar(10), CreatedDateTime, 103) AS CreatedDateTime,
                CreatedByUserID
            FROM AdmissionTermCondMaster
            ORDER BY AdmissionTermCondID DESC";

                SqlCommand cmd = new SqlCommand(query, con);

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    list.Add(new
                    {
                        AdmissionTermCondID =
                            Convert.ToInt32(dr["AdmissionTermCondID"]),

                        AdmissionTermCond =
                            dr["AdmissionTermCond"].ToString(),

                        IsActive =
                            Convert.ToBoolean(dr["IsActive"]),

                        CreatedDateTime =
                            dr["CreatedDateTime"].ToString(),

                        CreatedByUserID =
                            Convert.ToInt32(dr["CreatedByUserID"])
                    });
                }
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }


        // =====================================================
        // SAVE / UPDATE
        // =====================================================

        [HttpPost]
        public JsonResult SaveAdmissionTermCond(AdmissionTermCondMaster model)
        {
            using (SqlConnection con = new SqlConnection(
                ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();


                // ============================================
                // Validation
                // ============================================

                if (string.IsNullOrWhiteSpace(model.AdmissionTermCond))
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Please enter Admission Term & Condition."
                    });
                }


                // ============================================
                // Duplicate Check
                // ============================================

                SqlCommand checkCmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM AdmissionTermCondMaster
            WHERE AdmissionTermCond = @AdmissionTermCond
              AND AdmissionTermCondID <> @AdmissionTermCondID", con);

                checkCmd.Parameters.AddWithValue(
                    "@AdmissionTermCond",
                    model.AdmissionTermCond.Trim());

                checkCmd.Parameters.AddWithValue(
                    "@AdmissionTermCondID",
                    model.AdmissionTermCondID);


                int count = Convert.ToInt32(
                    checkCmd.ExecuteScalar());


                if (count > 0)
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Admission Term & Condition already exists."
                    });
                }


                // ============================================
                // INSERT
                // ============================================

                if (model.AdmissionTermCondID == 0)
                {
                    string query = @"
                INSERT INTO AdmissionTermCondMaster
                (
                    AdmissionTermCond,
                    IsActive,
                    CreatedDateTime,
                    CreatedByUserID
                )
                VALUES
                (
                    @AdmissionTermCond,
                    @IsActive,
                    GETDATE(),
                    @CreatedByUserID
                )";


                    SqlCommand cmd =
                        new SqlCommand(query, con);


                    cmd.Parameters.AddWithValue(
                        "@AdmissionTermCond",
                        model.AdmissionTermCond.Trim());


                    cmd.Parameters.AddWithValue(
                        "@IsActive",
                        model.IsActive);


                    // Replace with Logged-in User ID
                    cmd.Parameters.AddWithValue(
                        "@CreatedByUserID",
                        1);


                    cmd.ExecuteNonQuery();
                }


                // ============================================
                // UPDATE
                // ============================================

                else
                {
                    string query = @"
                UPDATE AdmissionTermCondMaster
                SET
                    AdmissionTermCond = @AdmissionTermCond,
                    IsActive = @IsActive
                WHERE AdmissionTermCondID =
                      @AdmissionTermCondID";


                    SqlCommand cmd =
                        new SqlCommand(query, con);


                    cmd.Parameters.AddWithValue(
                        "@AdmissionTermCondID",
                        model.AdmissionTermCondID);


                    cmd.Parameters.AddWithValue(
                        "@AdmissionTermCond",
                        model.AdmissionTermCond.Trim());


                    cmd.Parameters.AddWithValue(
                        "@IsActive",
                        model.IsActive);


                    cmd.ExecuteNonQuery();
                }
            }


            return Json(new
            {
                Status = true,
                Message = "Admission Term & Condition saved successfully."
            });
        }


        // =====================================================
        // GET BY ID
        // =====================================================

        [HttpGet]
        public JsonResult GetAdmissionTermCondByID(int id)
        {
            AdmissionTermCondMaster model =
                new AdmissionTermCondMaster();


            using (SqlConnection con = new SqlConnection(
                ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();


                string query = @"
            SELECT
                AdmissionTermCondID,
                AdmissionTermCond,
                IsActive,
                CreatedDateTime,
                CreatedByUserID
            FROM AdmissionTermCondMaster
            WHERE AdmissionTermCondID =
                  @AdmissionTermCondID";


                SqlCommand cmd =
                    new SqlCommand(query, con);


                cmd.Parameters.AddWithValue(
                    "@AdmissionTermCondID",
                    id);


                SqlDataReader dr =
                    cmd.ExecuteReader();


                if (dr.Read())
                {
                    model.AdmissionTermCondID =
                        Convert.ToInt32(
                            dr["AdmissionTermCondID"]);


                    model.AdmissionTermCond =
                        dr["AdmissionTermCond"].ToString();


                    model.IsActive =
                        Convert.ToBoolean(
                            dr["IsActive"]);


                    model.CreatedDateTime =
                        Convert.ToDateTime(
                            dr["CreatedDateTime"]);


                    model.CreatedByUserID =
                        Convert.ToInt32(
                            dr["CreatedByUserID"]);
                }
            }


            return Json(
                model,
                JsonRequestBehavior.AllowGet);
        }


        // =====================================================
        // DELETE
        // =====================================================

        [HttpPost]
        public JsonResult DeleteAdmissionTermCond(int id)
        {
            using (SqlConnection con = new SqlConnection(
                ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();


                string query = @"
            DELETE FROM AdmissionTermCondMaster
            WHERE AdmissionTermCondID =
                  @AdmissionTermCondID";


                SqlCommand cmd =
                    new SqlCommand(query, con);


                cmd.Parameters.AddWithValue(
                    "@AdmissionTermCondID",
                    id);


                cmd.ExecuteNonQuery();
            }


            return Json("Success");
        }

        #endregion
        #region FeeTermCondMaster

        public ActionResult FeeTermCondMaster()
        {
            ViewData["ParentMenu"] = "Utility";
            ViewData["PageTitle"] = "Fee Term & Condition Master";

            return View();
        }


        // =====================================================
        // GET: Fee Term & Condition List
        // =====================================================

        [HttpGet]
        public JsonResult GetFeeTermCondList()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(
                ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"
            SELECT
                FeeTermCondID,
                FeeTermCond,
                IsActive,
                CONVERT(varchar(10), CreatedDateTime, 103) AS CreatedDateTime,
                CreatedByUserID
            FROM FeeTermCondMaster
            ORDER BY FeeTermCondID DESC";

                SqlCommand cmd = new SqlCommand(query, con);

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    list.Add(new
                    {
                        FeeTermCondID =
                            Convert.ToInt32(dr["FeeTermCondID"]),

                        FeeTermCond =
                            dr["FeeTermCond"].ToString(),

                        IsActive =
                            Convert.ToByte(dr["IsActive"]),

                        CreatedDateTime =
                            dr["CreatedDateTime"].ToString(),

                        CreatedByUserID =
                            Convert.ToInt32(dr["CreatedByUserID"])
                    });
                }
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }


        // =====================================================
        // SAVE / UPDATE
        // =====================================================

        [HttpPost]
        public JsonResult SaveFeeTermCond(FeeTermCondMaster model)
        {
            using (SqlConnection con = new SqlConnection(
                ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();


                // ============================================
                // Validation
                // ============================================

                if (string.IsNullOrWhiteSpace(model.FeeTermCond))
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Please enter Fee Term & Condition."
                    });
                }


                // ============================================
                // Duplicate Check
                // ============================================

                SqlCommand checkCmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM FeeTermCondMaster
            WHERE FeeTermCond = @FeeTermCond
              AND FeeTermCondID <> @FeeTermCondID", con);

                checkCmd.Parameters.AddWithValue(
                    "@FeeTermCond",
                    model.FeeTermCond.Trim());

                checkCmd.Parameters.AddWithValue(
                    "@FeeTermCondID",
                    model.FeeTermCondID);


                int count = Convert.ToInt32(
                    checkCmd.ExecuteScalar());


                if (count > 0)
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Fee Term & Condition already exists."
                    });
                }


                // ============================================
                // INSERT
                // ============================================

                if (model.FeeTermCondID == 0)
                {
                    string query = @"
                INSERT INTO FeeTermCondMaster
                (
                    FeeTermCond,
                    IsActive,
                    CreatedDateTime,
                    CreatedByUserID
                )
                VALUES
                (
                    @FeeTermCond,
                    @IsActive,
                    GETDATE(),
                    @CreatedByUserID
                )";


                    SqlCommand cmd =
                        new SqlCommand(query, con);


                    cmd.Parameters.AddWithValue(
                        "@FeeTermCond",
                        model.FeeTermCond.Trim());


                    cmd.Parameters.AddWithValue(
                        "@IsActive",
                        model.IsActive);


                    // Replace with logged-in User ID
                    cmd.Parameters.AddWithValue(
                        "@CreatedByUserID",
                        1);


                    cmd.ExecuteNonQuery();
                }


                // ============================================
                // UPDATE
                // ============================================

                else
                {
                    string query = @"
                UPDATE FeeTermCondMaster
                SET
                    FeeTermCond = @FeeTermCond,
                    IsActive = @IsActive
                WHERE FeeTermCondID = @FeeTermCondID";


                    SqlCommand cmd =
                        new SqlCommand(query, con);


                    cmd.Parameters.AddWithValue(
                        "@FeeTermCondID",
                        model.FeeTermCondID);


                    cmd.Parameters.AddWithValue(
                        "@FeeTermCond",
                        model.FeeTermCond.Trim());


                    cmd.Parameters.AddWithValue(
                        "@IsActive",
                        model.IsActive);


                    cmd.ExecuteNonQuery();
                }
            }


            return Json(new
            {
                Status = true,
                Message = "Fee Term & Condition saved successfully."
            });
        }


        // =====================================================
        // GET BY ID
        // =====================================================

        [HttpGet]
        public JsonResult GetFeeTermCondByID(int id)
        {
            FeeTermCondMaster model =
                new FeeTermCondMaster();


            using (SqlConnection con = new SqlConnection(
                ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();


                string query = @"
            SELECT
                FeeTermCondID,
                FeeTermCond,
                IsActive,
                CreatedDateTime,
                CreatedByUserID
            FROM FeeTermCondMaster
            WHERE FeeTermCondID = @FeeTermCondID";


                SqlCommand cmd =
                    new SqlCommand(query, con);


                cmd.Parameters.AddWithValue(
                    "@FeeTermCondID",
                    id);


                SqlDataReader dr =
                    cmd.ExecuteReader();


                if (dr.Read())
                {
                    model.FeeTermCondID =
                        Convert.ToInt32(
                            dr["FeeTermCondID"]);


                    model.FeeTermCond =
                        dr["FeeTermCond"].ToString();


                    model.IsActive =
                        Convert.ToByte(
                            dr["IsActive"]);


                    model.CreatedDateTime =
                        Convert.ToDateTime(
                            dr["CreatedDateTime"]);


                    model.CreatedByUserID =
                        Convert.ToInt32(
                            dr["CreatedByUserID"]);
                }
            }


            return Json(
                model,
                JsonRequestBehavior.AllowGet);
        }


        // =====================================================
        // DELETE
        // =====================================================

        [HttpPost]
        public JsonResult DeleteFeeTermCond(int id)
        {
            using (SqlConnection con = new SqlConnection(
                ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();


                string query = @"
            DELETE FROM FeeTermCondMaster
            WHERE FeeTermCondID = @FeeTermCondID";


                SqlCommand cmd =
                    new SqlCommand(query, con);


                cmd.Parameters.AddWithValue(
                    "@FeeTermCondID",
                    id);


                cmd.ExecuteNonQuery();
            }


            return Json("Success");
        }

        #endregion
        #region StudentChargeMaster
        public ActionResult StudentChargeMaster()
        {
            ViewData["ParentMenu"] = "Utility";
            ViewData["PageTitle"] = "Student Charge Master";
            return View();
        }

        [HttpGet]
        public JsonResult GetStudentChargeList()
        {
            List<object> list = new List<object>();
            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();
                string query = @"SELECT 
                                    StudentChargeID,
                                    ISNULL(ChargeName, '') AS ChargeName,
                                    ISNULL(ChargeAmount, 0) AS ChargeAmount,
                                    ISNULL(ChargeDescription, '') AS ChargeDescription,
                                    ISNULL(IsActive, 1) AS IsActive,
                                    ISNULL(CONVERT(varchar(10), CreatedDateTime, 103), '-') AS CreatedDateFormatted,
                                    ISNULL(CreatedByUserID, 1) AS CreatedByUserID
                                 FROM StudentChargeMaster
                                 ORDER BY StudentChargeID DESC";

                SqlCommand cmd = new SqlCommand(query, con);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new
                    {
                        StudentChargeID = Convert.ToInt32(dr["StudentChargeID"]),
                        ChargeName = dr["ChargeName"].ToString(),
                        ChargeAmount = Convert.ToDecimal(dr["ChargeAmount"]),
                        ChargeDescription = dr["ChargeDescription"].ToString(),
                        IsActive = Convert.ToBoolean(dr["IsActive"]),
                        CreatedDateFormatted = dr["CreatedDateFormatted"].ToString(),
                        CreatedByUserID = Convert.ToInt32(dr["CreatedByUserID"])
                    });
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult SaveStudentCharge(StudentChargeMaster model)
        {
            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();

                // Duplicate Check
                SqlCommand checkCmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM StudentChargeMaster
                    WHERE ChargeName = @ChargeName
                      AND StudentChargeID <> @StudentChargeID", con);

                checkCmd.Parameters.AddWithValue("@ChargeName", model.ChargeName ?? "");
                checkCmd.Parameters.AddWithValue("@StudentChargeID", model.StudentChargeID);

                int count = Convert.ToInt32(checkCmd.ExecuteScalar());
                if (count > 0)
                {
                    return Json(new { Status = false, Message = "Student Charge with this name already exists." });
                }

                if (model.StudentChargeID == 0)
                {
                    string query = @"INSERT INTO StudentChargeMaster
                                     (ChargeName, ChargeAmount, ChargeDescription, IsActive, CreatedDateTime, CreatedByUserID)
                                     VALUES
                                     (@ChargeName, @ChargeAmount, @ChargeDescription, @IsActive, GETDATE(), @CreatedByUserID)";

                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@ChargeName", model.ChargeName ?? "");
                    cmd.Parameters.AddWithValue("@ChargeAmount", model.ChargeAmount);
                    cmd.Parameters.AddWithValue("@ChargeDescription", (object)model.ChargeDescription ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    cmd.Parameters.AddWithValue("@CreatedByUserID", 1);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    string query = @"UPDATE StudentChargeMaster
                                     SET ChargeName = @ChargeName,
                                         ChargeAmount = @ChargeAmount,
                                         ChargeDescription = @ChargeDescription,
                                         IsActive = @IsActive
                                     WHERE StudentChargeID = @StudentChargeID";

                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@StudentChargeID", model.StudentChargeID);
                    cmd.Parameters.AddWithValue("@ChargeName", model.ChargeName ?? "");
                    cmd.Parameters.AddWithValue("@ChargeAmount", model.ChargeAmount);
                    cmd.Parameters.AddWithValue("@ChargeDescription", (object)model.ChargeDescription ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    cmd.ExecuteNonQuery();
                }
            }
            return Json(new { Status = true, Message = "Student Charge saved successfully." });
        }

        [HttpGet]
        public JsonResult GetStudentChargeByID(int id)
        {
            StudentChargeMaster model = new StudentChargeMaster();
            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();
                string query = @"SELECT * FROM StudentChargeMaster WHERE StudentChargeID = @StudentChargeID";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@StudentChargeID", id);
                SqlDataReader dr = cmd.ExecuteReader();
                if (dr.Read())
                {
                    model.StudentChargeID = Convert.ToInt32(dr["StudentChargeID"]);
                    model.ChargeName = dr["ChargeName"].ToString();
                    model.ChargeAmount = Convert.ToDecimal(dr["ChargeAmount"]);
                    model.ChargeDescription = dr["ChargeDescription"] != DBNull.Value ? dr["ChargeDescription"].ToString() : "";
                    model.IsActive = Convert.ToBoolean(dr["IsActive"]);
                }
            }
            return Json(model, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult DeleteStudentCharge(int id)
        {
            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();
                string query = @"DELETE FROM StudentChargeMaster WHERE StudentChargeID = @StudentChargeID";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@StudentChargeID", id);
                cmd.ExecuteNonQuery();
            }
            return Json(new { Status = true, Message = "Student Charge deleted successfully." });
        }
        #endregion
        #region PaymentTypeMaster
        public ActionResult PaymentTypeMaster()
        {
            ViewData["ParentMenu"] = "Utility";
            ViewData["PageTitle"] = "PaymentType Master";
            return View();
        }

        [HttpGet]
        public JsonResult GetPaymentTypeList()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"SELECT
                            PaymentTypeID,
                            CONVERT(varchar(10),DateAndTime,103) AS DateAndTime,
                            PaymentTypeTitle,
                            UserID
                         FROM PaymentTypeMaster
                         ORDER BY PaymentTypeID DESC";

                SqlCommand cmd = new SqlCommand(query, con);

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    list.Add(new
                    {
                        PaymentTypeID = Convert.ToInt32(dr["PaymentTypeID"]),
                        DateAndTime = dr["DateAndTime"].ToString(),
                        PaymentTypeTitle = dr["PaymentTypeTitle"].ToString(),
                        UserID = Convert.ToInt32(dr["UserID"])
                    });
                }
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult SavePaymentType(PaymentTypeMaster model)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                // Duplicate Check
                SqlCommand checkCmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM PaymentTypeMaster
            WHERE PaymentTypeTitle = @PaymentTypeTitle
              AND PaymentTypeID <> @PaymentTypeID", con);

                checkCmd.Parameters.AddWithValue("@PaymentTypeTitle", model.PaymentTypeTitle ?? "");
                checkCmd.Parameters.AddWithValue("@PaymentTypeID", model.PaymentTypeID);

                int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Payment Type already exists."
                    });
                }

                if (model.PaymentTypeID == 0)
                {
                    string query = @"INSERT INTO PaymentTypeMaster
                            (
                                DateAndTime,
                                PaymentTypeTitle,
                                UserID
                            )
                            VALUES
                            (
                                GETDATE(),
                                @PaymentTypeTitle,
                                @UserID
                            )";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@PaymentTypeTitle", model.PaymentTypeTitle ?? "");
                    cmd.Parameters.AddWithValue("@UserID", 1);

                    cmd.ExecuteNonQuery();
                }
                else
                {
                    string query = @"UPDATE PaymentTypeMaster
                             SET PaymentTypeTitle = @PaymentTypeTitle
                             WHERE PaymentTypeID = @PaymentTypeID";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@PaymentTypeID", model.PaymentTypeID);
                    cmd.Parameters.AddWithValue("@PaymentTypeTitle", model.PaymentTypeTitle ?? "");

                    cmd.ExecuteNonQuery();
                }
            }

            return Json(new
            {
                Status = true,
                Message = "Payment Type saved successfully."
            });
        }

        [HttpGet]
        public JsonResult GetPaymentTypeByID(int id)
        {
            PaymentTypeMaster model = new PaymentTypeMaster();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"SELECT *
                         FROM PaymentTypeMaster
                         WHERE PaymentTypeID=@PaymentTypeID";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@PaymentTypeID", id);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    model.PaymentTypeID = Convert.ToInt32(dr["PaymentTypeID"]);
                    model.PaymentTypeTitle = dr["PaymentTypeTitle"].ToString();
                    model.UserID = Convert.ToInt32(dr["UserID"]);
                    if (dr["DateAndTime"] != DBNull.Value)
                    {
                        model.DateAndTime = Convert.ToDateTime(dr["DateAndTime"]);
                    }
                }
            }

            return Json(model, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult DeletePaymentType(int id)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"DELETE FROM PaymentTypeMaster
                         WHERE PaymentTypeID=@PaymentTypeID";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@PaymentTypeID", id);

                cmd.ExecuteNonQuery();
            }

            return Json("Success");
        }
        #endregion
        #region StudentFeeReceipt
        public ActionResult StudentFeeReceipt()
        {

            ViewData["ParentMenu"] = "Student";
            ViewData["PageTitle"] = "Student Fee Receipt";

            int currentUserID = 1;
            string currentUserName = "Admin";

            if (Request.Cookies["UserID"] != null && int.TryParse(Request.Cookies["UserID"].Value, out int cookieUserId))
            {
                currentUserID = cookieUserId;
            }
            if (Request.Cookies["UserName"] != null && !string.IsNullOrEmpty(Request.Cookies["UserName"].Value))
            {
                currentUserName = Server.UrlDecode(Request.Cookies["UserName"].Value);
            }

            ViewBag.CurrentUserID = currentUserID;
            ViewBag.CurrentUserName = currentUserName;

            return View();
        }



        public ActionResult StudentFeeReceiptPrint(string id = "")
        {
            ViewBag.StudentID = id;
            ViewData["ParentMenu"] = "Student";
            ViewData["PageTitle"] = "Student Fee Receipt";

            int currentUserID = 1;
            string currentUserName = "Admin";

            if (Request.Cookies["UserID"] != null && int.TryParse(Request.Cookies["UserID"].Value, out int cookieUserId))
            {
                currentUserID = cookieUserId;
            }
            if (Request.Cookies["UserName"] != null && !string.IsNullOrEmpty(Request.Cookies["UserName"].Value))
            {
                currentUserName = Server.UrlDecode(Request.Cookies["UserName"].Value);
            }

            ViewBag.CurrentUserID = currentUserID;
            ViewBag.CurrentUserName = currentUserName;

            return View();
        }



        [HttpGet]
        public JsonResult GetStudentFeeReceiptList(string searchText = "", int pageNumber = 1, int pageSize = 10)
        {
            List<StudentFeeReceiptModel> list = new List<StudentFeeReceiptModel>();
            int totalRecords = 0;

            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();

                string searchCondition = "";
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    searchCondition = @" AND (r.FeeReceiptNo LIKE @Search
                                           OR s.StudentFullName LIKE @Search
                                           OR s.StudentStringEnrollmentNo LIKE @Search
                                           OR s.StudentMobileNo LIKE @Search
                                           OR p.PaymentTypeTitle LIKE @Search
                                           OR f.FeesTypeTitle LIKE @Search)";
                }

                string countQuery = $@"SELECT COUNT(*)
                                     FROM StudentFeeReceipt r
                                     LEFT JOIN StudentMaster s ON r.StudentID = s.StudentID
                                     LEFT JOIN PaymentTypeMaster p ON r.PaymentTypeID = p.PaymentTypeID
                                     LEFT JOIN FeesTypeMaster f ON r.FeesTypeID = f.FeesTypeID
                                     WHERE 1=1 {searchCondition}";

                SqlCommand countCmd = new SqlCommand(countQuery, con);
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    countCmd.Parameters.AddWithValue("@Search", "%" + searchText.Trim() + "%");
                }
                totalRecords = Convert.ToInt32(countCmd.ExecuteScalar());

                int offset = (pageNumber - 1) * pageSize;

                string query = $@"SELECT
                                    r.StudentFeeReceiptID,
                                    r.StudentID,
                                    r.BranchID,
                                    ISNULL(r.FeeReceiptNo, '') AS FeeReceiptNo,
                                    r.ReceiptDate,
                                    ISNULL(r.AmountReceived, 0) AS AmountReceived,
                                    ISNULL(r.PaymentTypeID, 0) AS PaymentTypeID,
                                    ISNULL(r.FeesTypeID, 0) AS FeesTypeID,
                                    ISNULL(r.TransactionNo, '') AS TransactionNo,
                                    ISNULL(r.BankName, '') AS BankName,
                                    ISNULL(r.Remarks, '') AS Remarks,
                                    ISNULL(r.ReceivedByUserID, 0) AS ReceivedByUserID,
                                    ISNULL(r.FinancialYearID, 1) AS FinancialYearID,
                                    ISNULL(r.CreatedDateTime, GETDATE()) AS CreatedDateTime,
                                    ISNULL(r.CreatedByUserID, 1) AS CreatedByUserID,
                                    ISNULL(s.StudentFullName, '') AS StudentFullName,
                                    ISNULL(s.StudentStringEnrollmentNo, '') AS StudentEnrollmentNo,
                                    ISNULL(s.StudentMobileNo, '') AS StudentMobileNo,
                                    ISNULL(p.PaymentTypeTitle, 'Cash') AS PaymentTypeTitle,
                                    ISNULL(f.FeesTypeTitle, 'General') AS FeesTypeTitle,
                                    ISNULL(u.UserName, 'Admin') AS ReceivedByUserName,
                                    ISNULL(b.BranchName, '') AS BranchName
                                  FROM StudentFeeReceipt r
                                  LEFT JOIN StudentMaster s ON r.StudentID = s.StudentID
                                  LEFT JOIN PaymentTypeMaster p ON r.PaymentTypeID = p.PaymentTypeID
                                  LEFT JOIN FeesTypeMaster f ON r.FeesTypeID = f.FeesTypeID
                                  LEFT JOIN UserMaster u ON r.ReceivedByUserID = u.UserID
                                  LEFT JOIN BranchMaster b ON r.BranchID = b.BranchID
                                  WHERE 1=1 {searchCondition}
                                  ORDER BY r.StudentFeeReceiptID DESC
                                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    cmd.Parameters.AddWithValue("@Search", "%" + searchText.Trim() + "%");
                }

                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new StudentFeeReceiptModel
                    {
                        StudentFeeReceiptID = Convert.ToInt32(dr["StudentFeeReceiptID"]),
                        StudentID = Convert.ToInt32(dr["StudentID"]),
                        BranchID = Convert.ToInt32(dr["BranchID"]),
                        FeeReceiptNo = dr["FeeReceiptNo"].ToString(),
                        ReceiptDate = dr["ReceiptDate"] != DBNull.Value ? Convert.ToDateTime(dr["ReceiptDate"]) : DateTime.Now,
                        AmountReceived = Convert.ToDecimal(dr["AmountReceived"]),
                        PaymentTypeID = Convert.ToInt32(dr["PaymentTypeID"]),
                        FeesTypeID = Convert.ToInt32(dr["FeesTypeID"]),
                        TransactionNo = dr["TransactionNo"].ToString(),
                        BankName = dr["BankName"].ToString(),
                        Remarks = dr["Remarks"].ToString(),
                        ReceivedByUserID = Convert.ToInt32(dr["ReceivedByUserID"]),
                        FinancialYearID = Convert.ToInt32(dr["FinancialYearID"]),
                        CreatedDateTime = Convert.ToDateTime(dr["CreatedDateTime"]),
                        CreatedByUserID = Convert.ToInt32(dr["CreatedByUserID"]),
                        StudentFullName = dr["StudentFullName"].ToString(),
                        StudentEnrollmentNo = dr["StudentEnrollmentNo"].ToString(),
                        StudentMobileNo = dr["StudentMobileNo"].ToString(),
                        PaymentTypeTitle = dr["PaymentTypeTitle"].ToString(),
                        FeesTypeTitle = dr["FeesTypeTitle"].ToString(),
                        ReceivedByUserName = dr["ReceivedByUserName"].ToString(),
                        BranchName = dr["BranchName"].ToString()
                    });
                }
            }

            return Json(new { Data = list, TotalRecords = totalRecords }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetStudentFeeReceiptByID(int id)
        {
            StudentFeeReceiptModel model = new StudentFeeReceiptModel();

            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();

                string query = @"SELECT
                                    r.StudentFeeReceiptID,
                                    r.StudentID,
                                    r.BranchID,
                                    ISNULL(r.FeeReceiptNo, '') AS FeeReceiptNo,
                                    r.ReceiptDate,
                                    ISNULL(r.AmountReceived, 0) AS AmountReceived,
                                    ISNULL(r.PaymentTypeID, 0) AS PaymentTypeID,
                                    ISNULL(r.FeesTypeID, 0) AS FeesTypeID,
                                    ISNULL(r.TransactionNo, '') AS TransactionNo,
                                    ISNULL(r.BankName, '') AS BankName,
                                    ISNULL(r.Remarks, '') AS Remarks,
                                    ISNULL(r.ReceivedByUserID, 0) AS ReceivedByUserID,
                                    ISNULL(r.FinancialYearID, 1) AS FinancialYearID,
                                    ISNULL(s.StudentFullName, '') AS StudentFullName,
                                    ISNULL(s.StudentStringEnrollmentNo, '') AS StudentEnrollmentNo,
                                    ISNULL(p.PaymentTypeTitle, '') AS PaymentTypeTitle,
                                    ISNULL(f.FeesTypeTitle, '') AS FeesTypeTitle,
                                    ISNULL(u.UserName, '') AS ReceivedByUserName
                                  FROM StudentFeeReceipt r
                                  LEFT JOIN StudentMaster s ON r.StudentID = s.StudentID
                                  LEFT JOIN PaymentTypeMaster p ON r.PaymentTypeID = p.PaymentTypeID
                                  LEFT JOIN FeesTypeMaster f ON r.FeesTypeID = f.FeesTypeID
                                  LEFT JOIN UserMaster u ON r.ReceivedByUserID = u.UserID
                                  WHERE r.StudentFeeReceiptID = @StudentFeeReceiptID";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@StudentFeeReceiptID", id);

                SqlDataReader dr = cmd.ExecuteReader();
                if (dr.Read())
                {
                    model.StudentFeeReceiptID = Convert.ToInt32(dr["StudentFeeReceiptID"]);
                    model.StudentID = Convert.ToInt32(dr["StudentID"]);
                    model.BranchID = Convert.ToInt32(dr["BranchID"]);
                    model.FeeReceiptNo = dr["FeeReceiptNo"].ToString();
                    model.ReceiptDate = dr["ReceiptDate"] != DBNull.Value ? Convert.ToDateTime(dr["ReceiptDate"]) : DateTime.Now;
                    model.AmountReceived = Convert.ToDecimal(dr["AmountReceived"]);
                    model.PaymentTypeID = Convert.ToInt32(dr["PaymentTypeID"]);
                    model.FeesTypeID = Convert.ToInt32(dr["FeesTypeID"]);
                    model.TransactionNo = dr["TransactionNo"].ToString();
                    model.BankName = dr["BankName"].ToString();
                    model.Remarks = dr["Remarks"].ToString();
                    model.ReceivedByUserID = Convert.ToInt32(dr["ReceivedByUserID"]);
                    model.FinancialYearID = Convert.ToInt32(dr["FinancialYearID"]);
                    model.StudentFullName = dr["StudentFullName"].ToString();
                    model.StudentEnrollmentNo = dr["StudentEnrollmentNo"].ToString();
                    model.PaymentTypeTitle = dr["PaymentTypeTitle"].ToString();
                    model.FeesTypeTitle = dr["FeesTypeTitle"].ToString();
                    model.ReceivedByUserName = dr["ReceivedByUserName"].ToString();
                }
            }

            return Json(model, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetStudentFeeLedgerPrintDetails(int id)
        {
            try
            {
                int studentId = 0;
                object receiptObj = null;
                object studentObj = null;
                List<object> installmentList = new List<object>();
                List<object> paymentHistoryList = new List<object>();
                decimal totalFeeAmount = 0;
                decimal totalReceived = 0;

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    // 1. Fetch Selected Receipt Details
                    string queryReceipt = @"SELECT
                                                r.StudentFeeReceiptID,
                                                r.StudentID,
                                                r.BranchID,
                                                ISNULL(r.FeeReceiptNo, '') AS FeeReceiptNo,
                                                r.ReceiptDate,
                                                ISNULL(r.AmountReceived, 0) AS AmountReceived,
                                                ISNULL(r.PaymentTypeID, 0) AS PaymentTypeID,
                                                ISNULL(r.FeesTypeID, 0) AS FeesTypeID,
                                                ISNULL(r.TransactionNo, '') AS TransactionNo,
                                                ISNULL(r.BankName, '') AS BankName,
                                                ISNULL(r.Remarks, '') AS Remarks,
                                                ISNULL(p.PaymentTypeTitle, 'Cash') AS PaymentTypeTitle,
                                                ISNULL(f.FeesTypeTitle, 'General') AS FeesTypeTitle,
                                                ISNULL(u.UserName, 'Admin') AS ReceivedByUserName,
                                                ISNULL(NULLIF(b.BranchName, ''), ISNULL(NULLIF(br.BranchName, ''), 'Branch')) AS BranchName
                                              FROM StudentFeeReceipt r
                                              LEFT JOIN StudentMaster s ON r.StudentID = s.StudentID
                                              LEFT JOIN PaymentTypeMaster p ON r.PaymentTypeID = p.PaymentTypeID
                                              LEFT JOIN FeesTypeMaster f ON r.FeesTypeID = f.FeesTypeID
                                              LEFT JOIN UserMaster u ON r.ReceivedByUserID = u.UserID
                                              LEFT JOIN BranchMaster b ON r.BranchID = b.BranchID
                                              LEFT JOIN BranchMaster br ON s.BranchID = br.BranchID
                                              WHERE r.StudentFeeReceiptID = @ID";

                    using (SqlCommand cmdRec = new SqlCommand(queryReceipt, con))
                    {
                        cmdRec.Parameters.AddWithValue("@ID", id);
                        using (SqlDataReader drRec = cmdRec.ExecuteReader())
                        {
                            if (drRec.Read())
                            {
                                studentId = Convert.ToInt32(drRec["StudentID"]);
                                DateTime rDate = drRec["ReceiptDate"] != DBNull.Value ? Convert.ToDateTime(drRec["ReceiptDate"]) : DateTime.Now;
                                receiptObj = new
                                {
                                    StudentFeeReceiptID = Convert.ToInt32(drRec["StudentFeeReceiptID"]),
                                    FeeReceiptNo = drRec["FeeReceiptNo"].ToString(),
                                    ReceiptDateFormatted = rDate.ToString("dd/MM/yyyy hh:mm tt"),
                                    ReceiptDateShort = rDate.ToString("dd MMMM yyyy"),
                                    AmountReceived = Convert.ToDecimal(drRec["AmountReceived"]),
                                    PaymentTypeTitle = drRec["PaymentTypeTitle"].ToString(),
                                    FeesTypeTitle = drRec["FeesTypeTitle"].ToString(),
                                    TransactionNo = drRec["TransactionNo"].ToString(),
                                    BankName = drRec["BankName"].ToString(),
                                    Remarks = drRec["Remarks"].ToString(),
                                    ReceivedByUserName = drRec["ReceivedByUserName"].ToString(),
                                    BranchName = drRec["BranchName"].ToString()
                                };
                            }
                        }
                    }

                    if (studentId > 0)
                    {
                        // 2. Fetch Student Details
                        string queryStu = @"SELECT
                                                s.StudentID,
                                                ISNULL(s.StudentFullName, '') AS StudentFullName,
                                                ISNULL(s.StudentStringEnrollmentNo, '') AS StudentEnrollmentNo,
                                                ISNULL(s.StudentMobileNo, '') AS StudentMobileNo,
                                                ISNULL(s.FatherName, '') AS FatherName,
                                                ISNULL(s.StudentPhoto, '') AS StudentPhoto,
                                                ISNULL(s.CreatedDateTime, GETDATE()) AS JoiningDate,
                                                STUFF((
                                                    SELECT ', ' + c.CourseTitle
                                                    FROM StudentRegistrationCourse src
                                                    INNER JOIN CourseMaster c ON src.CourseID = c.CourseID
                                                    WHERE src.StudentID = s.StudentID
                                                    FOR XML PATH('')
                                                ), 1, 2, '') AS CourseTitle,
                                                ISNULL(b.BatchTitle, '') AS BatchTime,
                                                ISNULL(br.BranchName, 'Branch') AS BranchName
                                              FROM StudentMaster s
                                              LEFT JOIN BranchBatchMaster b ON s.BatchID = b.BatchID
                                              LEFT JOIN BranchMaster br ON s.BranchID = br.BranchID
                                              WHERE s.StudentID = @StudentID";

                        using (SqlCommand cmdStu = new SqlCommand(queryStu, con))
                        {
                            cmdStu.Parameters.AddWithValue("@StudentID", studentId);
                            using (SqlDataReader drStu = cmdStu.ExecuteReader())
                            {
                                if (drStu.Read())
                                {
                                    DateTime jDate = drStu["JoiningDate"] != DBNull.Value ? Convert.ToDateTime(drStu["JoiningDate"]) : DateTime.Now;
                                    studentObj = new
                                    {
                                        StudentID = Convert.ToInt32(drStu["StudentID"]),
                                        StudentFullName = drStu["StudentFullName"].ToString(),
                                        StudentEnrollmentNo = drStu["StudentEnrollmentNo"].ToString(),
                                        StudentMobileNo = drStu["StudentMobileNo"].ToString(),
                                        FatherName = drStu["FatherName"].ToString(),
                                        StudentPhoto = drStu["StudentPhoto"].ToString(),
                                        JoiningDateFormatted = jDate.ToString("dd/MM/yyyy hh:mm tt"),
                                        CourseTitle = string.IsNullOrEmpty(drStu["CourseTitle"].ToString()) ? "General Course" : drStu["CourseTitle"].ToString(),
                                        BatchTime = string.IsNullOrEmpty(drStu["BatchTime"].ToString()) ? "10.00 to 11.30 AM" : drStu["BatchTime"].ToString(),
                                        BranchName = drStu["BranchName"].ToString()
                                    };
                                }
                            }
                        }

                        // 3. Fetch Planned FeesInstallments Schedule
                        string queryInst = @"SELECT 
                                                fi.FeesInstallmentID,
                                                ISNULL(fi.FeesTypeID, 0) AS FeesTypeID,
                                                ISNULL(CONVERT(varchar(10), fi.FeesDate, 103), '-') AS DueDate,
                                                ISNULL(fi.FeesAmount, 0) AS FeesAmount,
                                                ISNULL(ft.FeesTypeTitle, '') AS FeesTypeTitle
                                             FROM FeesInstallmentMaster fi
                                             LEFT JOIN FeesTypeMaster ft ON fi.FeesTypeID = ft.FeesTypeID
                                             WHERE fi.StudentID = @StudentID
                                             ORDER BY fi.FeesInstallmentID ASC";

                        int instIdx = 1;
                        using (SqlCommand cmdInst = new SqlCommand(queryInst, con))
                        {
                            cmdInst.Parameters.AddWithValue("@StudentID", studentId);
                            using (SqlDataReader drInst = cmdInst.ExecuteReader())
                            {
                                while (drInst.Read())
                                {
                                    decimal amt = Convert.ToDecimal(drInst["FeesAmount"]);
                                    totalFeeAmount += amt;
                                    string name = !string.IsNullOrEmpty(drInst["FeesTypeTitle"].ToString())
                                        ? drInst["FeesTypeTitle"].ToString()
                                        : ("Installment " + instIdx);

                                    installmentList.Add(new
                                    {
                                        FeesInstallmentID = Convert.ToInt32(drInst["FeesInstallmentID"]),
                                        FeesTypeID = Convert.ToInt32(drInst["FeesTypeID"]),
                                        InstallmentName = name,
                                        DueDate = drInst["DueDate"].ToString(),
                                        Amount = amt
                                    });
                                    instIdx++;
                                }
                            }
                        }

                        // 4. Fetch Date-Wise Paid Receipt History
                        string queryHist = @"SELECT
                                                r.StudentFeeReceiptID,
                                                ISNULL(r.FeeReceiptNo, '') AS FeeReceiptNo,
                                                r.ReceiptDate,
                                                ISNULL(r.AmountReceived, 0) AS AmountReceived,
                                                ISNULL(p.PaymentTypeTitle, 'Cash') AS PaymentTypeTitle,
                                                ISNULL(f.FeesTypeTitle, 'General') AS FeesTypeTitle,
                                                ISNULL(r.TransactionNo, '') AS TransactionNo,
                                                ISNULL(r.BankName, '') AS BankName,
                                                ISNULL(u.UserName, 'Admin') AS ReceivedByUserName,
                                                ISNULL(r.Remarks, '') AS Remarks
                                             FROM StudentFeeReceipt r
                                             LEFT JOIN PaymentTypeMaster p ON r.PaymentTypeID = p.PaymentTypeID
                                             LEFT JOIN FeesTypeMaster f ON r.FeesTypeID = f.FeesTypeID
                                             LEFT JOIN UserMaster u ON r.ReceivedByUserID = u.UserID
                                             WHERE r.StudentID = @StudentID
                                             ORDER BY r.ReceiptDate ASC, r.StudentFeeReceiptID ASC";

                        using (SqlCommand cmdHist = new SqlCommand(queryHist, con))
                        {
                            cmdHist.Parameters.AddWithValue("@StudentID", studentId);
                            using (SqlDataReader drHist = cmdHist.ExecuteReader())
                            {
                                while (drHist.Read())
                                {
                                    decimal recAmt = Convert.ToDecimal(drHist["AmountReceived"]);
                                    totalReceived += recAmt;
                                    DateTime hDate = drHist["ReceiptDate"] != DBNull.Value ? Convert.ToDateTime(drHist["ReceiptDate"]) : DateTime.Now;
                                    paymentHistoryList.Add(new
                                    {
                                        StudentFeeReceiptID = Convert.ToInt32(drHist["StudentFeeReceiptID"]),
                                        FeeReceiptNo = drHist["FeeReceiptNo"].ToString(),
                                        ReceiptDateFormatted = hDate.ToString("dd MMM yyyy hh:mm tt"),
                                        ReceiptDateShort = hDate.ToString("dd MMM yyyy"),
                                        AmountReceived = recAmt,
                                        PaymentTypeTitle = drHist["PaymentTypeTitle"].ToString(),
                                        FeesTypeTitle = drHist["FeesTypeTitle"].ToString(),
                                        TransactionNo = drHist["TransactionNo"].ToString(),
                                        BankName = drHist["BankName"].ToString(),
                                        ReceivedByUserName = drHist["ReceivedByUserName"].ToString(),
                                        Remarks = drHist["Remarks"].ToString()
                                    });
                                }
                            }
                        }
                    }
                }

                decimal balanceFee = totalFeeAmount - totalReceived;
                if (balanceFee < 0) balanceFee = 0;

                return Json(new
                {
                    IsSuccess = true,
                    Receipt = receiptObj,
                    Student = studentObj,
                    Installments = installmentList,
                    PaymentHistory = paymentHistoryList,
                    Summary = new
                    {
                        TotalFeeAmount = totalFeeAmount,
                        TotalReceived = totalReceived,
                        BalanceFee = balanceFee
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpPost]
        public JsonResult SaveStudentFeeReceipt(StudentFeeReceiptModel model)
        {
            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();
                SqlTransaction trans = con.BeginTransaction();

                try
                {
                    int currentUserId = 1;
                    if (Request.Cookies["UserID"] != null && !string.IsNullOrEmpty(Request.Cookies["UserID"].Value))
                    {
                        int.TryParse(Request.Cookies["UserID"].Value, out currentUserId);
                    }

                    int studentId = model.StudentID;
                    int branchId = 1;
                    int orgId = 1;
                    int finYearId = 1;
                    string studentFullName = "";
                    string studentEnrollmentNo = "";

                    // Query student info
                    SqlCommand stuCmd = new SqlCommand(@"
                        SELECT ISNULL(BranchID, 1) AS BranchID,
                               ISNULL(OrganizationID, 1) AS OrganizationID,
                               ISNULL(FinancialYearID, 1) AS FinancialYearID,
                               ISNULL(StudentFullName, '') AS StudentFullName,
                               ISNULL(StudentStringEnrollmentNo, '') AS StudentStringEnrollmentNo
                        FROM StudentMaster WHERE StudentID = @StudentID", con, trans);
                    stuCmd.Parameters.AddWithValue("@StudentID", studentId);
                    SqlDataReader stuDr = stuCmd.ExecuteReader();
                    if (stuDr.Read())
                    {
                        branchId = Convert.ToInt32(stuDr["BranchID"]);
                        orgId = Convert.ToInt32(stuDr["OrganizationID"]);
                        finYearId = Convert.ToInt32(stuDr["FinancialYearID"]);
                        studentFullName = stuDr["StudentFullName"].ToString();
                        studentEnrollmentNo = stuDr["StudentStringEnrollmentNo"].ToString();
                    }
                    stuDr.Close();

                    // Query payment type title
                    string paymentTypeTitle = "Cash";
                    SqlCommand payTypeCmd = new SqlCommand(@"SELECT PaymentTypeTitle FROM PaymentTypeMaster WHERE PaymentTypeID = @PaymentTypeID", con, trans);
                    payTypeCmd.Parameters.AddWithValue("@PaymentTypeID", model.PaymentTypeID);
                    object pTypeObj = payTypeCmd.ExecuteScalar();
                    if (pTypeObj != null && pTypeObj != DBNull.Value)
                    {
                        paymentTypeTitle = pTypeObj.ToString();
                    }

                    string ledgerDescription = $"Fee Receipt from {studentFullName} ({studentEnrollmentNo})";
                    if (!string.IsNullOrWhiteSpace(model.BankName))
                    {
                        ledgerDescription += $" - Bank: {model.BankName.Trim()}";
                    }

                    // Validate Remaining Fee Installment Balance
                    decimal totalFeeAmount = 0;
                    string queryInstTotal = @"SELECT ISNULL(SUM(FeesAmount), 0) FROM FeesInstallmentMaster WHERE StudentID = @StudentID";
                    using (SqlCommand cmdInstTotal = new SqlCommand(queryInstTotal, con, trans))
                    {
                        cmdInstTotal.Parameters.AddWithValue("@StudentID", studentId);
                        object instObj = cmdInstTotal.ExecuteScalar();
                        if (instObj != null && instObj != DBNull.Value)
                        {
                            totalFeeAmount = Convert.ToDecimal(instObj);
                        }
                    }

                    if (totalFeeAmount > 0)
                    {
                        decimal otherReceived = 0;
                        string queryOtherRec = @"SELECT ISNULL(SUM(AmountReceived), 0) FROM StudentFeeReceipt WHERE StudentID = @StudentID AND StudentFeeReceiptID != @StudentFeeReceiptID";
                        using (SqlCommand cmdOtherRec = new SqlCommand(queryOtherRec, con, trans))
                        {
                            cmdOtherRec.Parameters.AddWithValue("@StudentID", studentId);
                            cmdOtherRec.Parameters.AddWithValue("@StudentFeeReceiptID", model.StudentFeeReceiptID);
                            object recObj = cmdOtherRec.ExecuteScalar();
                            if (recObj != null && recObj != DBNull.Value)
                            {
                                otherReceived = Convert.ToDecimal(recObj);
                            }
                        }

                        decimal remainingAllowed = totalFeeAmount - otherReceived;
                        if (remainingAllowed < 0) remainingAllowed = 0;

                        if (model.StudentFeeReceiptID == 0 && remainingAllowed <= 0)
                        {
                            trans.Rollback();
                            return Json(new { IsSuccess = false, Message = "Total Fees for this student are already fully paid. You cannot create a new fee receipt." });
                        }

                        if (model.AmountReceived > remainingAllowed)
                        {
                            trans.Rollback();
                            return Json(new { IsSuccess = false, Message = $"Received Amount (₹ {model.AmountReceived:N2}) cannot be greater than the remaining balance of ₹ {remainingAllowed:N2}." });
                        }
                    }

                    int receiptId = model.StudentFeeReceiptID;

                    if (receiptId == 0)
                    {
                        // INSERT INTO StudentFeeReceipt
                        string insertReceipt = @"
                            INSERT INTO StudentFeeReceipt
                            (
                                StudentID,
                                BranchID,
                                FeeReceiptNo,
                                ReceiptDate,
                                AmountReceived,
                                PaymentTypeID,
                                FeesTypeID,
                                TransactionNo,
                                BankName,
                                Remarks,
                                ReceivedByUserID,
                                FinancialYearID,
                                CreatedDateTime,
                                CreatedByUserID
                            )
                            VALUES
                            (
                                @StudentID,
                                @BranchID,
                                @FeeReceiptNo,
                                @ReceiptDate,
                                @AmountReceived,
                                @PaymentTypeID,
                                @FeesTypeID,
                                @TransactionNo,
                                @BankName,
                                @Remarks,
                                @ReceivedByUserID,
                                @FinancialYearID,
                                GETDATE(),
                                @CreatedByUserID
                            );
                            SELECT SCOPE_IDENTITY();";

                        SqlCommand cmdIns = new SqlCommand(insertReceipt, con, trans);
                        cmdIns.Parameters.AddWithValue("@StudentID", model.StudentID);
                        cmdIns.Parameters.AddWithValue("@BranchID", branchId);
                        cmdIns.Parameters.AddWithValue("@FeeReceiptNo", model.FeeReceiptNo ?? "");
                        cmdIns.Parameters.AddWithValue("@ReceiptDate", model.ReceiptDate);
                        cmdIns.Parameters.AddWithValue("@AmountReceived", model.AmountReceived);
                        cmdIns.Parameters.AddWithValue("@PaymentTypeID", model.PaymentTypeID);
                        cmdIns.Parameters.AddWithValue("@FeesTypeID", model.FeesTypeID);
                        cmdIns.Parameters.AddWithValue("@TransactionNo", model.TransactionNo ?? "");
                        cmdIns.Parameters.AddWithValue("@BankName", model.BankName ?? "");
                        cmdIns.Parameters.AddWithValue("@Remarks", model.Remarks ?? "");
                        cmdIns.Parameters.AddWithValue("@ReceivedByUserID", model.ReceivedByUserID > 0 ? model.ReceivedByUserID : currentUserId);
                        cmdIns.Parameters.AddWithValue("@FinancialYearID", finYearId);
                        cmdIns.Parameters.AddWithValue("@CreatedByUserID", model.ReceivedByUserID > 0 ? model.ReceivedByUserID : currentUserId);

                        receiptId = Convert.ToInt32(cmdIns.ExecuteScalar());

                        // INSERT INTO BranchLedger
                        string insertLedger = @"
                            INSERT INTO BranchLedger
                            (
                                OrganizationID,
                                BranchID,
                                FinancialYearID,
                                TransactionDate,
                                RelatedObjName,
                                RelatedObjID,
                                ReferenceNo,
                                Description,
                                Credit,
                                Debit,
                                PaymentType,
                                Remarks,
                                CreatedDateTime,
                                CreatedByUserID
                            )
                            VALUES
                            (
                                @OrganizationID,
                                @BranchID,
                                @FinancialYearID,
                                @TransactionDate,
                                'StudentFeeReceipt',
                                @RelatedObjID,
                                @ReferenceNo,
                                @Description,
                                @Credit,
                                0.00,
                                @PaymentType,
                                @Remarks,
                                GETDATE(),
                                @CreatedByUserID
                            )";

                        SqlCommand cmdLedger = new SqlCommand(insertLedger, con, trans);
                        cmdLedger.Parameters.AddWithValue("@OrganizationID", orgId);
                        cmdLedger.Parameters.AddWithValue("@BranchID", branchId);
                        cmdLedger.Parameters.AddWithValue("@FinancialYearID", finYearId);
                        cmdLedger.Parameters.AddWithValue("@TransactionDate", model.ReceiptDate);
                        cmdLedger.Parameters.AddWithValue("@RelatedObjID", receiptId);
                        cmdLedger.Parameters.AddWithValue("@ReferenceNo", model.FeeReceiptNo ?? "");
                        cmdLedger.Parameters.AddWithValue("@Description", ledgerDescription);
                        cmdLedger.Parameters.AddWithValue("@Credit", model.AmountReceived);
                        cmdLedger.Parameters.AddWithValue("@PaymentType", paymentTypeTitle);
                        cmdLedger.Parameters.AddWithValue("@Remarks", model.Remarks ?? "");
                        cmdLedger.Parameters.AddWithValue("@CreatedByUserID", model.ReceivedByUserID > 0 ? model.ReceivedByUserID : currentUserId);

                        cmdLedger.ExecuteNonQuery();
                    }
                    else
                    {
                        // UPDATE StudentFeeReceipt
                        string updateReceipt = @"
                            UPDATE StudentFeeReceipt
                            SET StudentID = @StudentID,
                                BranchID = @BranchID,
                                FeeReceiptNo = @FeeReceiptNo,
                                ReceiptDate = @ReceiptDate,
                                AmountReceived = @AmountReceived,
                                PaymentTypeID = @PaymentTypeID,
                                FeesTypeID = @FeesTypeID,
                                TransactionNo = @TransactionNo,
                                BankName = @BankName,
                                Remarks = @Remarks,
                                ReceivedByUserID = @ReceivedByUserID,
                                FinancialYearID = @FinancialYearID
                            WHERE StudentFeeReceiptID = @StudentFeeReceiptID";

                        SqlCommand cmdUpd = new SqlCommand(updateReceipt, con, trans);
                        cmdUpd.Parameters.AddWithValue("@StudentFeeReceiptID", receiptId);
                        cmdUpd.Parameters.AddWithValue("@StudentID", model.StudentID);
                        cmdUpd.Parameters.AddWithValue("@BranchID", branchId);
                        cmdUpd.Parameters.AddWithValue("@FeeReceiptNo", model.FeeReceiptNo ?? "");
                        cmdUpd.Parameters.AddWithValue("@ReceiptDate", model.ReceiptDate);
                        cmdUpd.Parameters.AddWithValue("@AmountReceived", model.AmountReceived);
                        cmdUpd.Parameters.AddWithValue("@PaymentTypeID", model.PaymentTypeID);
                        cmdUpd.Parameters.AddWithValue("@FeesTypeID", model.FeesTypeID);
                        cmdUpd.Parameters.AddWithValue("@TransactionNo", model.TransactionNo ?? "");
                        cmdUpd.Parameters.AddWithValue("@BankName", model.BankName ?? "");
                        cmdUpd.Parameters.AddWithValue("@Remarks", model.Remarks ?? "");
                        cmdUpd.Parameters.AddWithValue("@ReceivedByUserID", model.ReceivedByUserID > 0 ? model.ReceivedByUserID : currentUserId);
                        cmdUpd.Parameters.AddWithValue("@FinancialYearID", finYearId);

                        cmdUpd.ExecuteNonQuery();

                        // UPDATE BranchLedger
                        string updateLedger = @"
                            UPDATE BranchLedger
                            SET OrganizationID = @OrganizationID,
                                BranchID = @BranchID,
                                FinancialYearID = @FinancialYearID,
                                TransactionDate = @TransactionDate,
                                ReferenceNo = @ReferenceNo,
                                Description = @Description,
                                Credit = @Credit,
                                PaymentType = @PaymentType,
                                Remarks = @Remarks,
                                CreatedByUserID=@CreatedByUserID
                            WHERE RelatedObjName = 'StudentFeeReceipt' AND RelatedObjID = @RelatedObjID";

                        SqlCommand cmdUpdLedger = new SqlCommand(updateLedger, con, trans);
                        cmdUpdLedger.Parameters.AddWithValue("@OrganizationID", orgId);
                        cmdUpdLedger.Parameters.AddWithValue("@BranchID", branchId);
                        cmdUpdLedger.Parameters.AddWithValue("@FinancialYearID", finYearId);
                        cmdUpdLedger.Parameters.AddWithValue("@TransactionDate", model.ReceiptDate);
                        cmdUpdLedger.Parameters.AddWithValue("@ReferenceNo", model.FeeReceiptNo ?? "");
                        cmdUpdLedger.Parameters.AddWithValue("@Description", ledgerDescription);
                        cmdUpdLedger.Parameters.AddWithValue("@Credit", model.AmountReceived);
                        cmdUpdLedger.Parameters.AddWithValue("@PaymentType", paymentTypeTitle);
                        cmdUpdLedger.Parameters.AddWithValue("@Remarks", model.Remarks ?? "");
                        cmdUpdLedger.Parameters.AddWithValue("@RelatedObjID", receiptId);
                        cmdUpdLedger.Parameters.AddWithValue("@CreatedByUserID", model.ReceivedByUserID > 0 ? model.ReceivedByUserID : currentUserId);
                        int rowsAffected = cmdUpdLedger.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            // Insert into BranchLedger if record was missing
                            string insertLedger = @"
                                INSERT INTO BranchLedger
                                (
                                    OrganizationID,
                                    BranchID,
                                    FinancialYearID,
                                    TransactionDate,
                                    RelatedObjName,
                                    RelatedObjID,
                                    ReferenceNo,
                                    Description,
                                    Credit,
                                    Debit,
                                    PaymentType,
                                    Remarks,
                                    CreatedDateTime,
                                    CreatedByUserID
                                )
                                VALUES
                                (
                                    @OrganizationID,
                                    @BranchID,
                                    @FinancialYearID,
                                    @TransactionDate,
                                    'StudentFeeReceipt',
                                    @RelatedObjID,
                                    @ReferenceNo,
                                    @Description,
                                    @Credit,
                                    0.00,
                                    @PaymentType,
                                    @Remarks,
                                    GETDATE(),
                                    @CreatedByUserID
                                )";

                            SqlCommand cmdInsLedger = new SqlCommand(insertLedger, con, trans);
                            cmdInsLedger.Parameters.AddWithValue("@OrganizationID", orgId);
                            cmdInsLedger.Parameters.AddWithValue("@BranchID", branchId);
                            cmdInsLedger.Parameters.AddWithValue("@FinancialYearID", finYearId);
                            cmdInsLedger.Parameters.AddWithValue("@TransactionDate", model.ReceiptDate);
                            cmdInsLedger.Parameters.AddWithValue("@RelatedObjID", receiptId);
                            cmdInsLedger.Parameters.AddWithValue("@ReferenceNo", model.FeeReceiptNo ?? "");
                            cmdInsLedger.Parameters.AddWithValue("@Description", ledgerDescription);
                            cmdInsLedger.Parameters.AddWithValue("@Credit", model.AmountReceived);
                            cmdInsLedger.Parameters.AddWithValue("@PaymentType", paymentTypeTitle);
                            cmdInsLedger.Parameters.AddWithValue("@Remarks", model.Remarks ?? "");
                            cmdInsLedger.Parameters.AddWithValue("@CreatedByUserID", model.ReceivedByUserID > 0 ? model.ReceivedByUserID : currentUserId);

                            cmdInsLedger.ExecuteNonQuery();
                        }
                    }

                    trans.Commit();
                    return Json(new { IsSuccess = true, Message = "Student Fee Receipt saved successfully and posted to Branch Ledger!" });
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    return Json(new { IsSuccess = false, Message = "Error saving fee receipt: " + ex.Message });
                }
            }
        }

        [HttpPost]
        public JsonResult DeleteStudentFeeReceipt(int id)
        {
            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();
                SqlTransaction trans = con.BeginTransaction();

                try
                {
                    // Delete from BranchLedger
                    SqlCommand cmdLedger = new SqlCommand(@"DELETE FROM BranchLedger WHERE RelatedObjName = 'StudentFeeReceipt' AND RelatedObjID = @ID", con, trans);
                    cmdLedger.Parameters.AddWithValue("@ID", id);
                    cmdLedger.ExecuteNonQuery();

                    // Delete from StudentFeeReceipt
                    SqlCommand cmdReceipt = new SqlCommand(@"DELETE FROM StudentFeeReceipt WHERE StudentFeeReceiptID = @ID", con, trans);
                    cmdReceipt.Parameters.AddWithValue("@ID", id);
                    cmdReceipt.ExecuteNonQuery();

                    trans.Commit();
                    return Json(new { IsSuccess = true, Message = "Student Fee Receipt and corresponding Branch Ledger entry deleted successfully!" });
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    return Json(new { IsSuccess = false, Message = "Error deleting fee receipt: " + ex.Message });
                }
            }
        }

        [HttpGet]
        public JsonResult GetStudentDropdownForReceipt(int currentReceiptStudentId = 0)
        {
            List<object> list = new List<object>();
            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();
                string query = @"SELECT
                                    s.StudentID,
                                    ISNULL(s.StudentFullName, '') AS StudentFullName,
                                    ISNULL(s.StudentStringEnrollmentNo, '') AS StudentEnrollmentNo,
                                    ISNULL(s.StudentMobileNo, '') AS StudentMobileNo,
                                    ISNULL(s.FatherName, '') AS FatherName,
                                    ISNULL(s.StudentPhoto, '') AS StudentPhoto,
                                    ISNULL(inst.TotalFee, 0) AS TotalFee,
                                    ISNULL(rec.TotalReceived, 0) AS TotalReceived
                                  FROM StudentMaster s
                                  OUTER APPLY (
                                      SELECT ISNULL(SUM(FeesAmount), 0) AS TotalFee 
                                      FROM FeesInstallmentMaster 
                                      WHERE StudentID = s.StudentID
                                  ) inst
                                  OUTER APPLY (
                                      SELECT ISNULL(SUM(AmountReceived), 0) AS TotalReceived 
                                      FROM StudentFeeReceipt 
                                      WHERE StudentID = s.StudentID
                                  ) rec
                                  WHERE (inst.TotalFee = 0 OR (inst.TotalFee - rec.TotalReceived) > 0.01 OR s.StudentID = @CurrentReceiptStudentID)
                                  ORDER BY s.StudentFullName ASC";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@CurrentReceiptStudentID", currentReceiptStudentId);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    string rawEnroll = dr["StudentEnrollmentNo"].ToString();
                    string cleanEnroll = (!string.IsNullOrEmpty(rawEnroll) && !rawEnroll.StartsWith("AUTO-", StringComparison.OrdinalIgnoreCase)) ? rawEnroll : "";
                    string mobile = dr["StudentMobileNo"].ToString();
                    string father = dr["FatherName"].ToString();
                    string name = dr["StudentFullName"].ToString();

                    List<string> parts = new List<string>();
                    if (!string.IsNullOrEmpty(name)) parts.Add(name);
                    if (!string.IsNullOrEmpty(cleanEnroll)) parts.Add(cleanEnroll);
                    if (!string.IsNullOrEmpty(mobile)) parts.Add(mobile);
                    if (!string.IsNullOrEmpty(father)) parts.Add(father);

                    list.Add(new
                    {
                        StudentID = Convert.ToInt32(dr["StudentID"]),
                        StudentFullName = name,
                        StudentEnrollmentNo = cleanEnroll,
                        StudentMobileNo = mobile,
                        FatherName = father,
                        StudentPhoto = dr["StudentPhoto"].ToString(),
                        DisplayText = string.Join(" | ", parts)
                    });
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetPaymentTypeDropdownForReceipt()
        {
            List<object> list = new List<object>();
            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();
                string query = @"SELECT PaymentTypeID, PaymentTypeTitle FROM PaymentTypeMaster ORDER BY PaymentTypeTitle ASC";
                SqlCommand cmd = new SqlCommand(query, con);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new
                    {
                        PaymentTypeID = Convert.ToInt32(dr["PaymentTypeID"]),
                        PaymentTypeTitle = dr["PaymentTypeTitle"].ToString()
                    });
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetFeesTypeDropdownForReceipt()
        {
            List<object> list = new List<object>();
            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();
                string query = @"SELECT FeesTypeID, FeesTypeTitle FROM FeesTypeMaster ORDER BY FeesTypeTitle ASC";
                SqlCommand cmd = new SqlCommand(query, con);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new
                    {
                        FeesTypeID = Convert.ToInt32(dr["FeesTypeID"]),
                        FeesTypeTitle = dr["FeesTypeTitle"].ToString()
                    });
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetUserDropdownForReceipt()
        {
            List<object> list = new List<object>();
            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();
                string query = @"SELECT UserID, UserName FROM UserMaster ORDER BY UserName ASC";
                SqlCommand cmd = new SqlCommand(query, con);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new
                    {
                        UserID = Convert.ToInt32(dr["UserID"]),
                        UserName = dr["UserName"].ToString()
                    });
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetReceiptNoByStudent(int studentId)
        {
            string receiptNo = "";
            if (studentId <= 0)
            {
                return Json(new { ReceiptNo = "" }, JsonRequestBehavior.AllowGet);
            }

            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();

                int branchId = 1;
                string branchCode = "BR";

                // 1. Get student's BranchID and BranchCode
                string studentQuery = @"
                    SELECT s.BranchID, ISNULL(b.BranchCode, 'BR') AS BranchCode 
                    FROM StudentMaster s
                    LEFT JOIN BranchMaster b ON s.BranchID = b.BranchID
                    WHERE s.StudentID = @StudentID";

                using (SqlCommand cmdStu = new SqlCommand(studentQuery, con))
                {
                    cmdStu.Parameters.AddWithValue("@StudentID", studentId);
                    using (SqlDataReader drStu = cmdStu.ExecuteReader())
                    {
                        if (drStu.Read())
                        {
                            if (drStu["BranchID"] != DBNull.Value) branchId = Convert.ToInt32(drStu["BranchID"]);
                            if (drStu["BranchCode"] != DBNull.Value && !string.IsNullOrEmpty(drStu["BranchCode"].ToString()))
                            {
                                branchCode = drStu["BranchCode"].ToString().Trim();
                            }
                        }
                    }
                }

                // 2. Format branch prefix: e.g. "SAW-2026-" or "BR01-2026-"
                string currentYear = DateTime.Now.Year.ToString();
                string prefix = branchCode + "-" + currentYear + "-";

                // 3. Find MAX sequence for this branch and prefix
                string maxQuery = @"
                    SELECT MAX(CAST(RIGHT(FeeReceiptNo, 4) AS INT)) 
                    FROM StudentFeeReceipt 
                    WHERE BranchID = @BranchID 
                      AND FeeReceiptNo LIKE @Prefix + '%'";

                int nextNumber = 1;
                using (SqlCommand cmdMax = new SqlCommand(maxQuery, con))
                {
                    cmdMax.Parameters.AddWithValue("@BranchID", branchId);
                    cmdMax.Parameters.AddWithValue("@Prefix", prefix);

                    object maxVal = cmdMax.ExecuteScalar();
                    if (maxVal != null && maxVal != DBNull.Value)
                    {
                        nextNumber = Convert.ToInt32(maxVal) + 1;
                    }
                }

                // 4. Format string (e.g., SAW-2026-0001)
                receiptNo = string.Format("{0}{1:D4}", prefix, nextNumber);
            }

            return Json(new { ReceiptNo = receiptNo }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetStudentInstallmentDetails(int studentId, int receiptId = 0)
        {
            List<object> installments = new List<object>();
            decimal totalFeeAmount = 0;
            decimal totalReceived = 0;

            if (studentId <= 0)
            {
                return Json(new { Installments = installments, TotalFeeAmount = 0, TotalReceived = 0 }, JsonRequestBehavior.AllowGet);
            }

            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();

                // 1. Fetch Installments from FeesInstallmentMaster
                string queryInst = @"
                    SELECT 
                        fi.FeesInstallmentID,
                        ISNULL(fi.FeesTypeID, 0) AS FeesTypeID,
                        ISNULL(CONVERT(varchar(10), fi.FeesDate, 103), '-') AS DueDate,
                        ISNULL(fi.FeesAmount, 0) AS FeesAmount,
                        ISNULL(ft.FeesTypeTitle, '') AS FeesTypeTitle
                    FROM FeesInstallmentMaster fi
                    LEFT JOIN FeesTypeMaster ft ON fi.FeesTypeID = ft.FeesTypeID
                    WHERE fi.StudentID = @StudentID
                    ORDER BY fi.FeesInstallmentID ASC";

                int instIndex = 1;
                using (SqlCommand cmd = new SqlCommand(queryInst, con))
                {
                    cmd.Parameters.AddWithValue("@StudentID", studentId);
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            decimal amt = Convert.ToDecimal(dr["FeesAmount"]);
                            totalFeeAmount += amt;
                            string instName = !string.IsNullOrEmpty(dr["FeesTypeTitle"].ToString())
                                ? dr["FeesTypeTitle"].ToString()
                                : ("Installment " + instIndex);

                            installments.Add(new
                            {
                                FeesInstallmentID = Convert.ToInt32(dr["FeesInstallmentID"]),
                                FeesTypeID = Convert.ToInt32(dr["FeesTypeID"]),
                                FeesTypeTitle = dr["FeesTypeTitle"].ToString(),
                                InstallmentName = instName,
                                DueDate = dr["DueDate"].ToString(),
                                Amount = amt
                            });
                            instIndex++;
                        }
                    }
                }

                // 2. Fetch Total Received so far for this student from StudentFeeReceipt
                string queryRec = @"SELECT ISNULL(SUM(AmountReceived), 0) FROM StudentFeeReceipt WHERE StudentID = @StudentID AND (@ReceiptID = 0 OR StudentFeeReceiptID != @ReceiptID)";
                using (SqlCommand cmdRec = new SqlCommand(queryRec, con))
                {
                    cmdRec.Parameters.AddWithValue("@StudentID", studentId);
                    cmdRec.Parameters.AddWithValue("@ReceiptID", receiptId);
                    object recObj = cmdRec.ExecuteScalar();
                    if (recObj != null && recObj != DBNull.Value)
                    {
                        totalReceived = Convert.ToDecimal(recObj);
                    }
                }
            }

            return Json(new
            {
                Installments = installments,
                TotalFeeAmount = totalFeeAmount,
                TotalReceived = totalReceived
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult UpdateInstallmentDueDate(int feesInstallmentId, string dueDate)
        {
            try
            {
                if (feesInstallmentId <= 0 || string.IsNullOrWhiteSpace(dueDate))
                {
                    return Json(new { IsSuccess = false, Message = "Invalid installment or due date." });
                }

                DateTime dt;
                if (!DateTime.TryParse(dueDate, out dt))
                {
                    return Json(new { IsSuccess = false, Message = "Invalid date format." });
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string sql = "UPDATE FeesInstallmentMaster SET FeesDate = @FeesDate WHERE FeesInstallmentID = @FeesInstallmentID";
                    using (SqlCommand cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@FeesDate", dt);
                        cmd.Parameters.AddWithValue("@FeesInstallmentID", feesInstallmentId);
                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { IsSuccess = true, Message = "Next Due Date updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GenerateNextReceiptNo()
        {
            Random rnd = new Random();
            int nextNo = rnd.Next(10000000, 99999999);
            return Json(new { ReceiptNo = nextNo.ToString() }, JsonRequestBehavior.AllowGet);
        }
        #endregion
        #region EnquirySourceMaster
        public ActionResult EnquirySourceMaster()
        {
            ViewData["ParentMenu"] = "Utility";
            ViewData["PageTitle"] = "Enquiry Source Master";
            return View();
        }
        [HttpGet]
        public JsonResult GetEnquirySourceList()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"SELECT
                            EnquirySourceID,
                            CONVERT(VARCHAR(10), DateAndTime, 103) AS DateAndTime,
                            EnquirySourceType,
                            UserID
                         FROM EnquirySourceMaster
                         ORDER BY EnquirySourceID DESC";

                SqlCommand cmd = new SqlCommand(query, con);

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    list.Add(new
                    {
                        EnquirySourceID = Convert.ToInt32(dr["EnquirySourceID"]),
                        DateAndTime = dr["DateAndTime"].ToString(),
                        EnquirySourceType = dr["EnquirySourceType"].ToString(),
                        UserID = Convert.ToInt32(dr["UserID"])
                    });
                }

                dr.Close();
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult SaveEnquirySource(EnquirySourceMaster model)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    // Duplicate Check
                    SqlCommand checkCmd = new SqlCommand(@"
                SELECT COUNT(*)
                FROM EnquirySourceMaster
                WHERE EnquirySourceType = @EnquirySourceType
                  AND EnquirySourceID <> @EnquirySourceID", con);

                    checkCmd.Parameters.AddWithValue("@EnquirySourceType", model.EnquirySourceType);
                    checkCmd.Parameters.AddWithValue("@EnquirySourceID", model.EnquirySourceID);

                    int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (count > 0)
                    {
                        return Json(new
                        {
                            Status = false,
                            Message = "Enquiry Source already exists."
                        });
                    }

                    if (model.EnquirySourceID == 0)
                    {
                        // INSERT
                        string query = @"INSERT INTO EnquirySourceMaster
                                (
                                    DateAndTime,
                                    EnquirySourceType,
                                    UserID
                                )
                                VALUES
                                (
                                    GETDATE(),
                                    @EnquirySourceType,
                                    @UserID
                                )";

                        SqlCommand cmd = new SqlCommand(query, con);

                        cmd.Parameters.AddWithValue("@EnquirySourceType", model.EnquirySourceType);
                        cmd.Parameters.AddWithValue("@UserID", 1);

                        cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        // UPDATE
                        string query = @"UPDATE EnquirySourceMaster
                                 SET EnquirySourceType = @EnquirySourceType
                                 WHERE EnquirySourceID = @EnquirySourceID";

                        SqlCommand cmd = new SqlCommand(query, con);

                        cmd.Parameters.AddWithValue("@EnquirySourceID", model.EnquirySourceID);
                        cmd.Parameters.AddWithValue("@EnquirySourceType", model.EnquirySourceType);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new
                {
                    Status = true,
                    Message = "Enquiry Source saved successfully."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }
        [HttpGet]
        public JsonResult GetEnquirySourceByID(int id)
        {
            EnquirySourceMaster model = new EnquirySourceMaster();

            try
            {
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    string query = @"SELECT
                                EnquirySourceID,
                                DateAndTime,
                                EnquirySourceType,
                                UserID
                             FROM EnquirySourceMaster
                             WHERE EnquirySourceID = @EnquirySourceID";

                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@EnquirySourceID", id);

                    SqlDataReader dr = cmd.ExecuteReader();

                    if (dr.Read())
                    {
                        model.EnquirySourceID = Convert.ToInt32(dr["EnquirySourceID"]);
                        model.DateAndTime = Convert.ToDateTime(dr["DateAndTime"]);
                        model.EnquirySourceType = dr["EnquirySourceType"].ToString();
                        model.UserID = Convert.ToInt32(dr["UserID"]);
                    }

                    dr.Close();
                }

                return Json(model, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(ex.Message, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult DeleteEnquirySource(int id)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    string query = @"DELETE FROM EnquirySourceMaster
                             WHERE EnquirySourceID = @EnquirySourceID";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@EnquirySourceID", id);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Json("Success");
                    }
                    else
                    {
                        return Json("Record Not Found");
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(ex.Message);
            }
        }
        #endregion
        #region Course Manage
        public ActionResult CourseManage()
        {
            ViewData["ParentMenu"] = "Course";
            ViewData["PageTitle"] = "Course Manage";
            return View();
        }
        [HttpGet]
        public JsonResult GetCourseList(int page = 1, int pageSize = 10, string search = "")
        {
            List<CourseModel> list = new List<CourseModel>();
            int totalRecords = 0;

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                // Total Records
                string countQuery = @"
            SELECT COUNT(*)  FROM CourseMaster WHERE  (CourseTitle LIKE @Search  OR CourseCode LIKE @Search )";
                SqlCommand countCmd = new SqlCommand(countQuery, con);
                countCmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                totalRecords = Convert.ToInt32(countCmd.ExecuteScalar());
                // Data Query
                string query = @"
            SELECT *
            FROM CourseMaster
            WHERE   (
                CourseTitle LIKE @Search
                OR CourseCode LIKE @Search
            )
            ORDER BY CourseID DESC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    list.Add(new CourseModel
                    {
                        CourseID = Convert.ToInt32(dr["CourseID"]),
                        CourseCode = dr["CourseCode"].ToString(),
                        CourseTitle = dr["CourseTitle"].ToString(),
                        CourseImage = dr["CourseImage"].ToString(),
                        Duration = Convert.ToInt32(dr["Duration"]),
                        DurationType = dr["DurationType"].ToString(),
                        IsActive = Convert.ToBoolean(dr["IsActive"].ToString()),
                        CourseFee = Convert.ToDecimal(dr["CourseFee"]),
                        EditEncryptedID = Encrypt(dr["CourseID"].ToString() + "|E"),
                        ViewEncryptedID = Encrypt(dr["CourseID"].ToString() + "|V")
                    });

                }

                dr.Close();
            }

            return Json(new
            {
                data = list,
                totalRecords = totalRecords
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetMasterCourseList(int CourseID = 0)
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();

                string query = @"
                           SELECT
                        CM.CourseID, 
                        CM.CourseTitle, ISNULL(MC.CourseFee, CM.CourseFee) AS CourseFee,
                        CASE
                            WHEN MC.SelectCourseID IS NULL THEN 0
                            ELSE 1
                        END AS IsSelected
                    FROM CourseMaster CM
                    LEFT JOIN MasterCourse MC
                        ON CM.CourseID = MC.SelectCourseID
                        AND MC.CourseID = @CourseID
                    WHERE CM.IsActive = 1
                      AND CM.IsMasterCourse = 0
                      AND CM.CourseID <> @CourseID
                    ORDER BY CM.CourseTitle;";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@CourseID", CourseID);

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    list.Add(new
                    {
                        CourseID = Convert.ToInt32(dr["CourseID"]),
                        CourseTitle = dr["CourseTitle"].ToString(),
                        CourseFee = dr["CourseFee"].ToString(),
                        IsSelected = Convert.ToBoolean(dr["IsSelected"])
                    });
                }
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetCourse()
        {
            try
            {
                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    string query = @"SELECT
                                CourseID,
                                CourseCode,
                                CourseTitle,
                                Duration,
                                DurationType,
                                CourseFee,
                                IsActive
                             FROM CourseMaster
                             ORDER BY CourseID DESC";

                    SqlCommand cmd = new SqlCommand(query, con);

                    SqlDataReader dr = cmd.ExecuteReader();

                    while (dr.Read())
                    {
                        list.Add(new
                        {
                            CourseID = Convert.ToInt32(dr["CourseID"]),
                            CourseCode = dr["CourseCode"].ToString(),
                            CourseTitle = dr["CourseTitle"].ToString(),
                            Duration = dr["Duration"].ToString(),
                            DurationType = dr["DurationType"].ToString(),
                            CourseFee = dr["CourseFee"].ToString(),
                            IsActive = Convert.ToBoolean(dr["IsActive"])
                        });
                    }
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetCourseByID(int CourseID)
        {
            GetCourseModel model = new GetCourseModel();

            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand(@"
                        SELECT
                            CM.*,
                            CD.ShortDescription,
                            CD.FullDescription,
                            CD.Remarks
                        FROM CourseMaster CM
                        LEFT JOIN CourseDescription CD
                        ON CM.CourseID=CD.CourseID
                        WHERE CM.CourseID=@CourseID", con);

                cmd.Parameters.AddWithValue("@CourseID", CourseID);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    model.CourseID = Convert.ToInt32(dr["CourseID"]);
                    model.CourseCode = dr["CourseCode"].ToString();
                    model.CourseTitle = dr["CourseTitle"].ToString();
                    model.CourseShortName = dr["CourseShortName"].ToString();
                    model.CourseURL = dr["CourseURL"].ToString();
                    model.CourseImage = dr["CourseImage"].ToString();

                    model.Duration = Convert.ToInt32(dr["Duration"]);
                    model.DurationType = dr["DurationType"].ToString();

                    model.CourseFee = Convert.ToDecimal(dr["CourseFee"]);
                    model.RegistrationFee = Convert.ToDecimal(dr["RegistrationFee"]);
                    model.ExamFee = Convert.ToDecimal(dr["ExamFee"]);

                    model.CertificateAvailable = Convert.ToBoolean(dr["CertificateAvailable"]);
                    model.IsMasterCourse = Convert.ToBoolean(dr["IsMasterCourse"]);
                    model.IsActive = Convert.ToBoolean(dr["IsActive"]);

                    model.ShortDescription = dr["ShortDescription"].ToString();
                    model.FullDescription = dr["FullDescription"].ToString();
                    model.Remarks = dr["Remarks"].ToString();
                }

                dr.Close();

                List<int> ids = new List<int>();

                SqlCommand cmd2 = new SqlCommand(@"
                        SELECT SelectCourseID
                        FROM MasterCourse
                        WHERE CourseID=@CourseID", con);

                cmd2.Parameters.AddWithValue("@CourseID", CourseID);

                SqlDataReader dr2 = cmd2.ExecuteReader();

                while (dr2.Read())
                {
                    ids.Add(Convert.ToInt32(dr2["SelectCourseID"]));
                }

                model.MasterCourse = ids;
            }

            return Json(model, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public ActionResult CreateCourse(string id)
        {
            int CourseID = 0;
            string mode = "";
            CourseModel model = new CourseModel();
            if (!string.IsNullOrWhiteSpace(id))
            {
                try
                {
                    string decryptValue = Decrypt(id);

                    if (!string.IsNullOrWhiteSpace(decryptValue))
                    {
                        string[] arr = decryptValue.Split('|');

                        if (arr.Length >= 2)
                        {
                            int.TryParse(arr[0], out CourseID);
                            mode = arr[1];
                        }
                    }
                }
                catch
                {
                    return RedirectToAction("CourseManage");
                }
            }
            ViewBag.CourseID = CourseID;
            ViewBag.Mode = mode;
            ViewData["ParentMenu"] = "Course";
            ViewData["SubMenu"] = "CourseManage";
            ViewData["SubMenuUrl"] = Url.Action("CourseManage", "bctechadmin");
            switch (mode)
            {
                case "E":
                    ViewData["PageTitle"] = "Edit Course";
                    break;
                case "V":
                    ViewData["PageTitle"] = "View Course";
                    break;
                default:
                    ViewData["PageTitle"] = "Create Course";
                    break;
            }

            if (CourseID > 0)
            {
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    SqlCommand cmd = new SqlCommand("SELECT * FROM CourseMaster WHERE CourseID=@CourseID", con);
                    cmd.Parameters.AddWithValue("@CourseID", CourseID);

                    SqlDataReader dr = cmd.ExecuteReader();

                    if (dr.Read())
                    {
                        model.CourseID = Convert.ToInt32(dr["CourseID"]);
                        model.CourseCode = dr["CourseCode"].ToString();
                        model.CourseTitle = dr["CourseTitle"].ToString();
                        model.CourseShortName = dr["CourseShortName"].ToString();
                        model.CourseURL = dr["CourseURL"].ToString();
                        model.CourseImage = dr["CourseImage"].ToString();
                        model.Duration = Convert.ToInt32(dr["Duration"]);
                        model.DurationType = dr["DurationType"].ToString();
                        model.CourseFee = Convert.ToDecimal(dr["CourseFee"]);
                    }
                }
            }

            return View(model);
        }
        [HttpPost]
        public JsonResult DeleteCourse(int CourseID)
        {
            SqlConnection con = new SqlConnection(CS);
            SqlTransaction trans = null;

            try
            {
                con.Open();
                trans = con.BeginTransaction();

                string imageName = "";

                // Get Image Name
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT CourseImage FROM CourseMaster WHERE CourseID=@CourseID",
                    con, trans))
                {
                    cmd.Parameters.AddWithValue("@CourseID", CourseID);

                    object obj = cmd.ExecuteScalar();

                    if (obj != null && obj != DBNull.Value)
                        imageName = obj.ToString();
                }

                // Delete Master Course Mapping
                using (SqlCommand cmd = new SqlCommand(@"
                DELETE FROM MasterCourse
                WHERE CourseID=@CourseID
                   OR SelectCourseID=@CourseID",
                        con, trans))
                {
                    cmd.Parameters.AddWithValue("@CourseID", CourseID);
                    cmd.ExecuteNonQuery();
                }

                // Delete Course Description
                using (SqlCommand cmd = new SqlCommand(
                    "DELETE FROM CourseDescription WHERE CourseID=@CourseID",
                    con, trans))
                {
                    cmd.Parameters.AddWithValue("@CourseID", CourseID);
                    cmd.ExecuteNonQuery();
                }

                // Delete Course
                using (SqlCommand cmd = new SqlCommand(
                    "DELETE FROM CourseMaster WHERE CourseID=@CourseID",
                    con, trans))
                {
                    cmd.Parameters.AddWithValue("@CourseID", CourseID);
                    cmd.ExecuteNonQuery();
                }

                trans.Commit();

                // Delete Image from Folder
                if (!string.IsNullOrWhiteSpace(imageName))
                {
                    string path = Server.MapPath("~/Content/Upload/CourseImage/" + imageName);

                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                    }
                }

                return Json(new
                {
                    Status = 1,
                    Message = "Course Deleted Successfully."
                });
            }
            catch (Exception ex)
            {
                if (trans != null)
                    trans.Rollback();

                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
            finally
            {
                if (con.State == ConnectionState.Open)
                    con.Close();

                con.Dispose();
            }
        }
        [HttpGet]
        public JsonResult GetMasterCourseDropdown()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand(@"
SELECT
CourseID,
CourseTitle
FROM CourseMaster
WHERE IsActive=1
ORDER BY CourseTitle", con);

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    list.Add(new
                    {
                        CourseID = dr["CourseID"],
                        CourseTitle = dr["CourseTitle"].ToString()
                    });
                }
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        [ValidateInput(false)]
        public JsonResult SaveCourse()
        {
            SqlConnection con = new SqlConnection(CS);
            SqlTransaction trans = null;
            try
            {
                con.Open();
                trans = con.BeginTransaction();
                int CourseID = 0;
                if (!string.IsNullOrEmpty(Request["CourseID"]))
                    CourseID = Convert.ToInt32(Request["CourseID"]);
                string FullDescription = Request.Unvalidated.Form["FullDescription"];
                string ImageName = "";
                #region Upload Image
                if (Request.Files.Count > 0)
                {
                    HttpPostedFileBase file = Request.Files["CourseImage"];

                    if (file != null && file.ContentLength > 0)
                    {
                        string ext = Path.GetExtension(file.FileName).ToLower();

                        if (ext != ".jpg" &&
                            ext != ".jpeg" &&
                            ext != ".png")
                        {
                            return Json(new
                            {
                                Status = 0,
                                Message = "Only JPG, JPEG and PNG images are allowed."
                            });
                        }

                        if (file.ContentLength > (2 * 1024 * 1024))
                        {
                            return Json(new
                            {
                                Status = 0,
                                Message = "Image size should not exceed 2 MB."
                            });
                        }

                        ImageName = Guid.NewGuid().ToString() + ext;

                        string FolderPath = Server.MapPath("~/Content/Upload/CourseImage/");

                        if (!Directory.Exists(FolderPath))
                        {
                            Directory.CreateDirectory(FolderPath);
                        }

                        file.SaveAs(Path.Combine(FolderPath, ImageName));
                    }
                }

                #endregion

                bool CertificateAvailable =
                    Request["CertificateAvailable"] == "true" ||
                    Request["CertificateAvailable"] == "on";

                bool IsMasterCourse =
                    Request["IsMasterCourse"] == "true" ||
                    Request["IsMasterCourse"] == "on";

                bool IsStudentCourse =
                    Request["IsStudentCourse"] == "true" ||
                    Request["IsStudentCourse"] == "on";

                bool IsActive =
                    Request["IsActive"] == "true" ||
                    Request["IsActive"] == "on";

                decimal CourseFee = 0;
                decimal RegistrationFee = 0;
                decimal ExamFee = 0;

                decimal.TryParse(Request["CourseFee"], out CourseFee);
                decimal.TryParse(Request["RegistrationFee"], out RegistrationFee);
                decimal.TryParse(Request["ExamFee"], out ExamFee);

                int Duration = 0;
                int.TryParse(Request["Duration"], out Duration);
                #region Duplicate Course Code

                using (SqlCommand chk = new SqlCommand(@"
                        SELECT COUNT(*)
                        FROM CourseMaster
                        WHERE CourseCode=@CourseCode
                        AND CourseID<>@CourseID", con, trans))
                {
                    chk.Parameters.Add("@CourseCode", SqlDbType.NVarChar).Value = Request["CourseCode"];
                    chk.Parameters.Add("@CourseID", SqlDbType.Int).Value = CourseID;

                    if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                    {
                        trans.Rollback();

                        return Json(new
                        {
                            Status = 0,
                            Message = "Course Code already exists."
                        });
                    }
                }

                #endregion

                #region Insert / Update Course

                if (CourseID == 0)
                {
                    string Query = @"
                                INSERT INTO CourseMaster
                                (
                                    CourseCode,
                                    CourseTitle,
                                    CourseShortName,
                                    CourseURL,
                                    CourseImage,
                                    Duration,
                                    DurationType,
                                    CourseFee,
                                    RegistrationFee,
                                    ExamFee,
                                    CertificateAvailable,
                                    IsMasterCourse,
                                    IsActive,
                                    IsStudentCourse,
                                    CreatedDateTime,
                                    CreatedByUserID
                                )
                                VALUES
                                (
                                    @CourseCode,
                                    @CourseTitle,
                                    @CourseShortName,
                                    @CourseURL,
                                    @CourseImage,
                                    @Duration,
                                    @DurationType,
                                    @CourseFee,
                                    @RegistrationFee,
                                    @ExamFee,
                                    @CertificateAvailable,
                                    @IsMasterCourse,
                                    @IsActive,
                                    @IsStudentCourse,
                                    GETDATE(),
                                    @CreatedByUserID
                                );

                                SELECT SCOPE_IDENTITY();";

                    using (SqlCommand cmd = new SqlCommand(Query, con, trans))
                    {
                        cmd.Parameters.Add("@CourseCode", SqlDbType.NVarChar).Value = Request["CourseCode"];
                        cmd.Parameters.Add("@CourseTitle", SqlDbType.NVarChar).Value = Request["CourseTitle"];
                        cmd.Parameters.Add("@CourseShortName", SqlDbType.NVarChar).Value = Request["CourseShortName"];
                        cmd.Parameters.Add("@CourseURL", SqlDbType.NVarChar).Value = Request["CourseURL"];
                        cmd.Parameters.Add("@CourseImage", SqlDbType.NVarChar).Value = ImageName;
                        cmd.Parameters.Add("@Duration", SqlDbType.Int).Value = Duration;
                        cmd.Parameters.Add("@DurationType", SqlDbType.NVarChar).Value = Request["DurationType"];
                        cmd.Parameters.Add("@CourseFee", SqlDbType.Decimal).Value = CourseFee;
                        cmd.Parameters.Add("@RegistrationFee", SqlDbType.Decimal).Value = RegistrationFee;
                        cmd.Parameters.Add("@ExamFee", SqlDbType.Decimal).Value = ExamFee;
                        cmd.Parameters.Add("@CertificateAvailable", SqlDbType.Bit).Value = CertificateAvailable;
                        cmd.Parameters.Add("@IsMasterCourse", SqlDbType.Bit).Value = IsMasterCourse;
                        cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = IsActive;
                        cmd.Parameters.Add("@IsStudentCourse", SqlDbType.Bit).Value = IsStudentCourse;
                        cmd.Parameters.Add("@CreatedByUserID", SqlDbType.Int).Value = 1;

                        CourseID = Convert.ToInt32(cmd.ExecuteScalar());

                        FullDescription = SaveEditorFiles(FullDescription, CourseID);
                    }
                }

                else
                {
                    string OldImage = "";

                    using (SqlCommand img = new SqlCommand(
                        "SELECT CourseImage FROM CourseMaster WHERE CourseID=@ID",
                        con,
                        trans))
                    {
                        img.Parameters.Add("@ID", SqlDbType.Int).Value = CourseID;

                        object obj = img.ExecuteScalar();

                        if (obj != null)
                            OldImage = obj.ToString();
                    }

                    if (string.IsNullOrEmpty(ImageName))
                        ImageName = OldImage;

                    string Query = @"
                            UPDATE CourseMaster
                            SET
                                CourseCode=@CourseCode,
                                CourseTitle=@CourseTitle,
                                CourseShortName=@CourseShortName,
                                CourseURL=@CourseURL,
                                CourseImage=@CourseImage,
                                Duration=@Duration,
                                DurationType=@DurationType,
                                CourseFee=@CourseFee,
                                RegistrationFee=@RegistrationFee,
                                ExamFee=@ExamFee,
                                CertificateAvailable=@CertificateAvailable,
                                IsMasterCourse=@IsMasterCourse,
                                IsActive=@IsActive,                          
                                IsStudentCourse=@IsStudentCourse                          
                            WHERE CourseID=@CourseID";

                    using (SqlCommand cmd = new SqlCommand(Query, con, trans))
                    {
                        cmd.Parameters.Add("@CourseID", SqlDbType.Int).Value = CourseID;
                        cmd.Parameters.Add("@CourseCode", SqlDbType.NVarChar).Value = Request["CourseCode"];
                        cmd.Parameters.Add("@CourseTitle", SqlDbType.NVarChar).Value = Request["CourseTitle"];
                        cmd.Parameters.Add("@CourseShortName", SqlDbType.NVarChar).Value = Request["CourseShortName"];
                        cmd.Parameters.Add("@CourseURL", SqlDbType.NVarChar).Value = Request["CourseURL"];
                        cmd.Parameters.Add("@CourseImage", SqlDbType.NVarChar).Value = ImageName;
                        cmd.Parameters.Add("@Duration", SqlDbType.Int).Value = Duration;
                        cmd.Parameters.Add("@DurationType", SqlDbType.NVarChar).Value = Request["DurationType"];
                        cmd.Parameters.Add("@CourseFee", SqlDbType.Decimal).Value = CourseFee;
                        cmd.Parameters.Add("@RegistrationFee", SqlDbType.Decimal).Value = RegistrationFee;
                        cmd.Parameters.Add("@ExamFee", SqlDbType.Decimal).Value = ExamFee;
                        cmd.Parameters.Add("@CertificateAvailable", SqlDbType.Bit).Value = CertificateAvailable;
                        cmd.Parameters.Add("@IsMasterCourse", SqlDbType.Bit).Value = IsMasterCourse;
                        cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = IsActive;
                        cmd.Parameters.Add("@IsStudentCourse", SqlDbType.Bit).Value = IsStudentCourse;

                        cmd.ExecuteNonQuery();
                        CourseID = Convert.ToInt32(Request["CourseID"]);
                        FullDescription = SaveEditorFiles(FullDescription, CourseID);
                    }

                    if (!string.IsNullOrEmpty(OldImage) &&
                        OldImage != ImageName)
                    {
                        string path = Server.MapPath("~/Content/Upload/CourseImage/" + OldImage);

                        if (System.IO.File.Exists(path))
                        {
                            System.IO.File.Delete(path);
                        }
                    }
                }
                #endregion
                #region Course Description

                using (SqlCommand chk = new SqlCommand(
                    "SELECT COUNT(*) FROM CourseDescription WHERE CourseID=@CourseID",
                    con,
                    trans))
                {
                    chk.Parameters.Add("@CourseID", SqlDbType.Int).Value = CourseID;

                    int Count = Convert.ToInt32(chk.ExecuteScalar());

                    if (Count == 0)
                    {
                        string Query = @"
                                INSERT INTO CourseDescription
                                (
                                    CourseID,
                                    ShortDescription,
                                    FullDescription,
                                    Remarks,
                                    CreatedDateTime,
                                    CreatedByUserID
                                )
                                VALUES
                                (
                                    @CourseID,
                                    @ShortDescription,
                                    @FullDescription,
                                    @Remarks,
                                    GETDATE(),
                                    @CreatedByUserID
                                )";

                        using (SqlCommand cmd = new SqlCommand(Query, con, trans))
                        {
                            cmd.Parameters.Add("@CourseID", SqlDbType.Int).Value = CourseID;
                            cmd.Parameters.Add("@ShortDescription", SqlDbType.NVarChar).Value = Request["ShortDescription"];

                            cmd.Parameters.Add("@FullDescription", SqlDbType.NVarChar).Value = FullDescription;
                            cmd.Parameters.Add("@Remarks", SqlDbType.NVarChar).Value = Request["Remarks"];
                            cmd.Parameters.Add("@CreatedByUserID", SqlDbType.Int).Value = 1;

                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        string Query = @"
                                UPDATE CourseDescription
                                SET
                                    ShortDescription=@ShortDescription,
                                    FullDescription=@FullDescription,
                                    Remarks=@Remarks
                                WHERE CourseID=@CourseID";

                        using (SqlCommand cmd = new SqlCommand(Query, con, trans))
                        {
                            cmd.Parameters.Add("@CourseID", SqlDbType.Int).Value = CourseID;
                            cmd.Parameters.Add("@ShortDescription", SqlDbType.NVarChar).Value = Request["ShortDescription"];
                            cmd.Parameters.Add("@FullDescription", SqlDbType.NVarChar).Value = FullDescription;
                            cmd.Parameters.Add("@Remarks", SqlDbType.NVarChar).Value = Request["Remarks"];

                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                #endregion

                #region Master Course

                // Old Mapping Delete
                using (SqlCommand del = new SqlCommand(
                    "DELETE FROM MasterCourse WHERE CourseID=@CourseID",
                    con,
                    trans))
                {
                    del.Parameters.Add("@CourseID", SqlDbType.Int).Value = CourseID;
                    del.ExecuteNonQuery();
                }

                // Insert only if IsMasterCourse = true
                if (IsMasterCourse)
                {
                    string[] MasterCourses = Request.Form.GetValues("MasterCourse[]");
                    string[] MasterCourseFees = Request.Form.GetValues("MasterCourseFee[]");

                    if (MasterCourses != null && MasterCourses.Length > 0)
                    {
                        for (int i = 0; i < MasterCourses.Length; i++)
                        {
                            int selectCourseId = Convert.ToInt32(MasterCourses[i]);
                            decimal courseFee = 0;
                            if (MasterCourseFees != null && MasterCourseFees.Length > i)
                            {
                                decimal.TryParse(MasterCourseFees[i], out courseFee);
                            }

                            using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO MasterCourse
                (
                    CourseID,
                    SelectCourseID,
                    CourseFee,
                    CreatedDateTime,
                    CreatedByUserID
                )
                VALUES
                (
                    @CourseID,
                    @SelectCourseID,
                    @CourseFee,
                    GETDATE(),
                    @CreatedByUserID
                )", con, trans))
                            {
                                cmd.Parameters.Add("@CourseID", SqlDbType.Int).Value = CourseID;
                                cmd.Parameters.Add("@SelectCourseID", SqlDbType.Int).Value = selectCourseId;
                                cmd.Parameters.Add("@CourseFee", SqlDbType.Decimal).Value = courseFee;
                                cmd.Parameters.Add("@CreatedByUserID", SqlDbType.Int).Value = 1;

                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }

                #endregion
                trans.Commit();
                return Json(new
                {
                    Status = 1,
                    Message = "Course Saved Successfully."
                });
            }
            catch (Exception ex)
            {
                if (trans != null)
                    trans.Rollback();

                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
            finally
            {
                if (con.State == ConnectionState.Open)
                    con.Close();

                con.Dispose();
            }
        }
        private string SaveEditorFiles(string html, int courseID)
        {
            string folder = Server.MapPath("~/Content/Upload/CourseDescription/" + courseID + "/");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            int fileIndex = Directory.GetFiles(folder).Length + 1;

            // Save Base64 Images
            Regex imgRegex = new Regex("<img[^>]+src=[\"'](?<src>[^\"']+)[\"'][^>]*>",
                RegexOptions.IgnoreCase);

            foreach (Match match in imgRegex.Matches(html))
            {
                string src = match.Groups["src"].Value;

                if (!src.StartsWith("data:"))
                    continue;

                string mime = src.Substring(5, src.IndexOf(";") - 5);

                string ext = ".bin";

                switch (mime.ToLower())
                {
                    case "image/png": ext = ".png"; break;
                    case "image/jpeg": ext = ".jpg"; break;
                    case "image/jpg": ext = ".jpg"; break;
                    case "image/gif": ext = ".gif"; break;
                    case "image/webp": ext = ".webp"; break;
                    case "image/bmp": ext = ".bmp"; break;
                }

                string base64 = src.Substring(src.IndexOf(",") + 1);

                byte[] bytes = Convert.FromBase64String(base64);

                string fileName = $"Course_{courseID}_{fileIndex}{ext}";

                string savePath = Path.Combine(folder, fileName);

                System.IO.File.WriteAllBytes(savePath, bytes);

                html = html.Replace(src,
                    "/Content/Upload/CourseDescription/" + courseID + "/" + fileName);

                fileIndex++;
            }

            return html;
        }
        #endregion
        #region Material Category Master

        public ActionResult MaterialCategoryMaster()
        {
            ViewData["ParentMenu"] = "Inventory";
            ViewData["PageTitle"] = "Material Category Master";
            return View();
        }
        [HttpPost]
        public JsonResult SaveMaterialCategory(MaterialCategoryMaster model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.CategoryCode))
                {
                    return Json(new
                    {
                        Status = 0,
                        Message = "Category Code is required."
                    });
                }

                if (string.IsNullOrWhiteSpace(model.CategoryName))
                {
                    return Json(new
                    {
                        Status = 0,
                        Message = "Category Name is required."
                    });
                }

                int userID = Convert.ToInt32(Request.Cookies["UserID"].Value);

                int result = SaveMaterialCategory(model, userID);

                if (result > 0)
                {
                    return Json(new
                    {
                        Status = 1,
                        Message = model.MaterialCategoryID == 0
                            ? "Material Category Saved Successfully."
                            : "Material Category Updated Successfully."
                    });
                }

                if (result == -1)
                {
                    return Json(new
                    {
                        Status = 0,
                        Message = "Category Code already exists."
                    });
                }

                if (result == -2)
                {
                    return Json(new
                    {
                        Status = 0,
                        Message = "Category Name already exists."
                    });
                }

                return Json(new
                {
                    Status = 0,
                    Message = "Unable to save record."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
        }
        public int SaveMaterialCategory(MaterialCategoryMaster model, int UserID)
        {
            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();

                // Duplicate Category Code
                string checkCode = @"SELECT COUNT(*) FROM MaterialCategoryMaster  WHERE CategoryCode=@CategoryCode AND MaterialCategoryID<>@MaterialCategoryID";

                SqlCommand cmdCheckCode = new SqlCommand(checkCode, con);
                cmdCheckCode.Parameters.AddWithValue("@CategoryCode", model.CategoryCode);
                cmdCheckCode.Parameters.AddWithValue("@MaterialCategoryID", model.MaterialCategoryID);

                if (Convert.ToInt32(cmdCheckCode.ExecuteScalar()) > 0)
                    return -1;

                // Duplicate Category Name
                string checkName = @"SELECT COUNT(*) FROM MaterialCategoryMaster WHERE CategoryName=@CategoryName AND MaterialCategoryID<>@MaterialCategoryID";

                SqlCommand cmdCheckName = new SqlCommand(checkName, con);
                cmdCheckName.Parameters.AddWithValue("@CategoryName", model.CategoryName);
                cmdCheckName.Parameters.AddWithValue("@MaterialCategoryID", model.MaterialCategoryID);

                if (Convert.ToInt32(cmdCheckName.ExecuteScalar()) > 0)
                    return -2;

                SqlCommand cmd = new SqlCommand();
                cmd.Connection = con;

                if (model.MaterialCategoryID == 0)
                {
                    cmd.CommandText = @"INSERT INTO MaterialCategoryMaster
                                (
                                    CategoryCode,
                                    CategoryName,
                                    Description,
                                    IsActive,
                                    CreatedDateTime,
                                    CreatedByUserID
                                )
                                VALUES
                                (
                                    @CategoryCode,
                                    @CategoryName,
                                    @Description,
                                    @IsActive,
                                    GETDATE(),
                                    @UserID
                                )";
                }
                else
                {
                    cmd.CommandText = @"UPDATE MaterialCategoryMaster
                                SET CategoryCode=@CategoryCode,
                                    CategoryName=@CategoryName,
                                    Description=@Description,
                                    IsActive=@IsActive                                 
                                WHERE MaterialCategoryID=@MaterialCategoryID";
                }

                cmd.Parameters.AddWithValue("@MaterialCategoryID", model.MaterialCategoryID);
                cmd.Parameters.AddWithValue("@CategoryCode", model.CategoryCode);
                cmd.Parameters.AddWithValue("@CategoryName", model.CategoryName);
                cmd.Parameters.AddWithValue("@Description", model.Description ?? "");
                cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                cmd.Parameters.AddWithValue("@UserID", UserID);

                return cmd.ExecuteNonQuery();
            }
        }

        [HttpPost]
        public JsonResult DeleteMaterialCategory(int MaterialCategoryID)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    // Optional: Check if record exists
                    string checkQuery = @"SELECT COUNT(*) 
                                  FROM MaterialCategoryMaster
                                  WHERE MaterialCategoryID=@MaterialCategoryID";

                    SqlCommand checkCmd = new SqlCommand(checkQuery, con);
                    checkCmd.Parameters.AddWithValue("@MaterialCategoryID", MaterialCategoryID);

                    if (Convert.ToInt32(checkCmd.ExecuteScalar()) == 0)
                    {
                        return Json(new
                        {
                            Status = 0,
                            Message = "Material Category not found."
                        });
                    }

                    // Delete Record
                    string deleteQuery = @"DELETE FROM MaterialCategoryMaster
                                   WHERE MaterialCategoryID=@MaterialCategoryID";

                    SqlCommand cmd = new SqlCommand(deleteQuery, con);
                    cmd.Parameters.AddWithValue("@MaterialCategoryID", MaterialCategoryID);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Json(new
                        {
                            Status = 1,
                            Message = "Material Category Deleted Successfully."
                        });
                    }

                    return Json(new
                    {
                        Status = 0,
                        Message = "Unable to delete record."
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
        }
        [HttpPost]
        public JsonResult GetMaterialCategoryList(string Search = "", int PageNumber = 1)
        {
            try
            {
                int PageSize = 10;
                int TotalRecords = 0;

                List<MaterialCategoryMaster> list = new List<MaterialCategoryMaster>();

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    // Total Records
                    string countQuery = @"SELECT COUNT(*)
                                  FROM MaterialCategoryMaster
                                  WHERE (@Search='' OR
                                         CategoryCode LIKE '%' + @Search + '%' OR
                                         CategoryName LIKE '%' + @Search + '%')";

                    SqlCommand countCmd = new SqlCommand(countQuery, con);
                    countCmd.Parameters.AddWithValue("@Search", Search ?? "");

                    TotalRecords = Convert.ToInt32(countCmd.ExecuteScalar());

                    // Data
                    string query = @"
                SELECT *
                FROM MaterialCategoryMaster
                WHERE (@Search='' OR
                       CategoryCode LIKE '%' + @Search + '%' OR
                       CategoryName LIKE '%' + @Search + '%')
                ORDER BY MaterialCategoryID DESC
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY";

                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@Search", Search ?? "");
                    cmd.Parameters.AddWithValue("@Offset", (PageNumber - 1) * PageSize);
                    cmd.Parameters.AddWithValue("@PageSize", PageSize);

                    SqlDataReader dr = cmd.ExecuteReader();

                    while (dr.Read())
                    {
                        list.Add(new MaterialCategoryMaster
                        {
                            MaterialCategoryID = Convert.ToInt32(dr["MaterialCategoryID"]),
                            CategoryCode = dr["CategoryCode"].ToString(),
                            CategoryName = dr["CategoryName"].ToString(),
                            Description = dr["Description"].ToString(),
                            IsActive = Convert.ToBoolean(dr["IsActive"])
                        });
                    }

                    dr.Close();
                }

                int TotalPages = (int)Math.Ceiling((double)TotalRecords / PageSize);

                return Json(new
                {
                    Status = 1,
                    Data = list,
                    TotalRecords = TotalRecords,
                    TotalPages = TotalPages,
                    CurrentPage = PageNumber,
                    ShowingText = $"Showing {((PageNumber - 1) * PageSize) + 1} to {Math.Min(PageNumber * PageSize, TotalRecords)} of {TotalRecords} entries"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
        }
        [HttpPost]
        public JsonResult GetMaterialCategoryByID(int MaterialCategoryID)
        {
            try
            {
                MaterialCategoryMaster model = new MaterialCategoryMaster();

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    string query = @"SELECT *
                             FROM MaterialCategoryMaster
                             WHERE MaterialCategoryID=@MaterialCategoryID";

                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@MaterialCategoryID", MaterialCategoryID);

                    SqlDataReader dr = cmd.ExecuteReader();

                    if (dr.Read())
                    {
                        model.MaterialCategoryID = Convert.ToInt32(dr["MaterialCategoryID"]);
                        model.CategoryCode = dr["CategoryCode"].ToString();
                        model.CategoryName = dr["CategoryName"].ToString();
                        model.Description = dr["Description"].ToString();
                        model.IsActive = Convert.ToBoolean(dr["IsActive"]);
                    }

                    dr.Close();
                }

                return Json(new
                {
                    Status = 1,
                    Data = model
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
        }

        #endregion
        #region Material Master

        public ActionResult MaterialMaster()
        {
            ViewData["ParentMenu"] = "Inventory";
            ViewData["PageTitle"] = "Material Master";
            return View();
        }

        [HttpPost]
        public JsonResult GetMaterialList(string Search = "", int PageNumber = 1)
        {
            try
            {
                int PageSize = 10;
                int StartRow = (PageNumber - 1) * PageSize;

                DataTable dt = new DataTable();

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    string query = @"
               SELECT
                M.*,
                C.CategoryName,
                U.UnitName
            FROM MaterialMaster M
            LEFT JOIN MaterialCategoryMaster C
                ON M.MaterialCategoryID = C.MaterialCategoryID
            LEFT JOIN UnitMaster U
                ON M.UnitID = U.UnitID
            WHERE
                (@Search = '' OR
                 M.MaterialCode LIKE '%' + @Search + '%' OR
                 M.MaterialName LIKE '%' + @Search + '%')
            ORDER BY M.MaterialID DESC
            OFFSET @StartRow ROWS
            FETCH NEXT @PageSize ROWS ONLY;";

                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@Search", Search);
                    cmd.Parameters.AddWithValue("@StartRow", StartRow);
                    cmd.Parameters.AddWithValue("@PageSize", PageSize);

                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    da.Fill(dt);
                }

                List<MaterialMasterModel> list = new List<MaterialMasterModel>();
                foreach (DataRow dr in dt.Rows)
                {
                    list.Add(new MaterialMasterModel
                    {
                        MaterialID = Convert.ToInt32(dr["MaterialID"]),
                        MaterialCategoryID = Convert.ToInt32(dr["MaterialCategoryID"]),
                        CategoryName = dr["CategoryName"].ToString(),

                        UnitID = Convert.ToInt32(dr["UnitID"]),
                        UnitName = dr["UnitName"].ToString(),

                        MaterialCode = dr["MaterialCode"].ToString(),
                        MaterialName = dr["MaterialName"].ToString(),
                        Brand = dr["Brand"].ToString(),
                        ModelNo = dr["ModelNo"].ToString(),
                        Description = dr["Description"].ToString(),
                        PurchasePrice = Convert.ToDecimal(dr["PurchasePrice"]),
                        IssuePrice = Convert.ToDecimal(dr["IssuePrice"]),
                        MinimumStock = Convert.ToDecimal(dr["MinimumStock"]),
                        ReOrderLevel = Convert.ToDecimal(dr["ReOrderLevel"]),
                        Barcode = dr["Barcode"].ToString(),
                        PeriodName = dr["PeriodName"].ToString(),
                        IsActive = Convert.ToBoolean(dr["IsActive"])
                    });
                }

                // Total Records
                int TotalRecords = 0;

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    string countQuery = @"
                SELECT COUNT(*)
                FROM MaterialMaster
                WHERE
                    (@Search = '' OR
                     MaterialCode LIKE '%' + @Search + '%' OR
                     MaterialName LIKE '%' + @Search + '%')";

                    SqlCommand cmd = new SqlCommand(countQuery, con);
                    cmd.Parameters.AddWithValue("@Search", Search);

                    con.Open();
                    TotalRecords = Convert.ToInt32(cmd.ExecuteScalar());
                }

                int TotalPages = (int)Math.Ceiling((double)TotalRecords / PageSize);

                return Json(new
                {
                    Status = 1,
                    Data = list,
                    ShowingText = "Showing " + (list.Count == 0 ? 0 : StartRow + 1) + " to " +
                                  (StartRow + list.Count) + " of " + TotalRecords + " entries",
                    Pagination = GeneratePagination(PageNumber, TotalPages)
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        private string GeneratePagination(int currentPage, int totalPages)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 1; i <= totalPages; i++)
            {
                if (i == currentPage)
                    sb.Append($"<li class='page-item active'><span class='page-link'>{i}</span></li>");
                else
                    sb.Append($"<li class='page-item'><a href='javascript:void(0)' class='page-link' data-page='{i}'>{i}</a></li>");
            }

            return sb.ToString();
        }
        [HttpPost]
        public JsonResult GetMaterialByID(int MaterialID)
        {
            try
            {
                MaterialMasterModel model = new MaterialMasterModel();

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    string query = @"
                SELECT *
                FROM MaterialMaster
                WHERE MaterialID = @MaterialID";

                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@MaterialID", MaterialID);

                    con.Open();

                    SqlDataReader dr = cmd.ExecuteReader();

                    if (dr.Read())
                    {
                        model.MaterialID = Convert.ToInt32(dr["MaterialID"]);
                        model.MaterialCategoryID = Convert.ToInt32(dr["MaterialCategoryID"]);
                        model.PeriodName = dr["PeriodName"].ToString();
                        model.UnitID = Convert.ToInt32(dr["UnitID"]);

                        model.MaterialCode = dr["MaterialCode"].ToString();
                        model.MaterialName = dr["MaterialName"].ToString();

                        model.Brand = dr["Brand"].ToString();
                        model.ModelNo = dr["ModelNo"].ToString();
                        model.Description = dr["Description"].ToString();

                        model.PurchasePrice = dr["PurchasePrice"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["PurchasePrice"]);
                        model.IssuePrice = dr["IssuePrice"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["IssuePrice"]);

                        model.MinimumStock = dr["MinimumStock"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["MinimumStock"]);
                        model.ReOrderLevel = dr["ReOrderLevel"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["ReOrderLevel"]);

                        model.Barcode = dr["Barcode"].ToString();

                        model.IsActive = Convert.ToBoolean(dr["IsActive"]);
                    }

                    dr.Close();
                }

                return Json(new
                {
                    Status = 1,
                    Data = model
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult SaveMaterial(MaterialMasterModel model)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    // Check Duplicate Material Code
                    string checkQuery = @"SELECT COUNT(*)
                                  FROM MaterialMaster
                                  WHERE MaterialCode=@MaterialCode
                                  AND MaterialID<>@MaterialID";

                    SqlCommand checkCmd = new SqlCommand(checkQuery, con);
                    checkCmd.Parameters.AddWithValue("@MaterialCode", model.MaterialCode);
                    checkCmd.Parameters.AddWithValue("@MaterialID", model.MaterialID);

                    int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (count > 0)
                    {
                        return Json(new
                        {
                            Status = 0,
                            Message = "Material Code already exists."
                        }, JsonRequestBehavior.AllowGet);
                    }

                    SqlCommand cmd = new SqlCommand();
                    cmd.Connection = con;

                    if (model.MaterialID == 0)
                    {
                        cmd.CommandText = @"
                INSERT INTO MaterialMaster
                (
                    MaterialCategoryID,
                    PeriodName,
                    UnitID,
                    MaterialCode,
                    MaterialName,
                    Brand,
                    ModelNo,
                    Description,
                    PurchasePrice,
                    IssuePrice,
                    MinimumStock,
                    ReOrderLevel,
                    Barcode,
                    IsActive,
                    CreatedDateTime,
                    CreatedByUserID
                )
                VALUES
                (
                    @MaterialCategoryID,
                    @PeriodName,
                    @UnitID,
                    @MaterialCode,
                    @MaterialName,
                    @Brand,
                    @ModelNo,
                    @Description,
                    @PurchasePrice,
                    @IssuePrice,
                    @MinimumStock,
                    @ReOrderLevel,
                    @Barcode,
                    @IsActive,
                    GETDATE(),
                    @CreatedByUserID
                )";
                    }
                    else
                    {
                        cmd.CommandText = @"
                UPDATE MaterialMaster
                SET
                    MaterialCategoryID=@MaterialCategoryID,
                    PeriodName=@PeriodName,
                    UnitID=@UnitID,
                    MaterialCode=@MaterialCode,
                    MaterialName=@MaterialName,
                    Brand=@Brand,
                    ModelNo=@ModelNo,
                    Description=@Description,
                    PurchasePrice=@PurchasePrice,
                    IssuePrice=@IssuePrice,
                    MinimumStock=@MinimumStock,
                    ReOrderLevel=@ReOrderLevel,
                    Barcode=@Barcode,
                    IsActive=@IsActive
                WHERE MaterialID=@MaterialID";
                    }

                    cmd.Parameters.AddWithValue("@MaterialID", model.MaterialID);
                    cmd.Parameters.AddWithValue("@MaterialCategoryID", model.MaterialCategoryID);
                    cmd.Parameters.AddWithValue("@PeriodName", model.PeriodName ?? "");
                    cmd.Parameters.AddWithValue("@UnitID", model.UnitID);
                    cmd.Parameters.AddWithValue("@MaterialCode", model.MaterialCode);
                    cmd.Parameters.AddWithValue("@MaterialName", model.MaterialName);
                    cmd.Parameters.AddWithValue("@Brand", string.IsNullOrWhiteSpace(model.Brand) ? (object)DBNull.Value : model.Brand);
                    cmd.Parameters.AddWithValue("@ModelNo", string.IsNullOrWhiteSpace(model.ModelNo) ? (object)DBNull.Value : model.ModelNo);
                    cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(model.Description) ? (object)DBNull.Value : model.Description);
                    cmd.Parameters.AddWithValue("@PurchasePrice", model.PurchasePrice);
                    cmd.Parameters.AddWithValue("@IssuePrice", model.IssuePrice);
                    cmd.Parameters.AddWithValue("@MinimumStock", model.MinimumStock);
                    cmd.Parameters.AddWithValue("@ReOrderLevel", model.ReOrderLevel);
                    cmd.Parameters.AddWithValue("@Barcode", string.IsNullOrWhiteSpace(model.Barcode) ? (object)DBNull.Value : model.Barcode);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    cmd.Parameters.AddWithValue("@CreatedByUserID", Session["UserID"] ?? 1);

                    cmd.ExecuteNonQuery();
                }

                return Json(new
                {
                    Status = 1,
                    Message = model.MaterialID == 0
                        ? "Material Saved Successfully."
                        : "Material Updated Successfully."
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult DeleteMaterial(int MaterialID)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    string query = @"DELETE FROM MaterialMaster
                             WHERE MaterialID = @MaterialID";

                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@MaterialID", MaterialID);

                    con.Open();

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Json(new
                        {
                            Status = 1,
                            Message = "Material Deleted Successfully."
                        });
                    }
                    else
                    {
                        return Json(new
                        {
                            Status = 0,
                            Message = "Material not found."
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
        }

        [HttpPost]
        public JsonResult GetMaterialCategoryDropdown()
        {
            try
            {
                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    string query = @"
                SELECT
                    MaterialCategoryID,
                    CategoryName
                FROM MaterialCategoryMaster
                WHERE IsActive = 1
                ORDER BY CategoryName";

                    SqlCommand cmd = new SqlCommand(query, con);

                    con.Open();

                    SqlDataReader dr = cmd.ExecuteReader();

                    while (dr.Read())
                    {
                        list.Add(new
                        {
                            MaterialCategoryID = Convert.ToInt32(dr["MaterialCategoryID"]),
                            CategoryName = dr["CategoryName"].ToString()
                        });
                    }

                    dr.Close();
                }

                return Json(new
                {
                    Status = 1,
                    Data = list
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult GetUnitDropdown()
        {
            try
            {
                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    string query = @"
                SELECT
                    UnitID,
                    UnitName
                FROM UnitMaster
                WHERE IsActive = 1
                ORDER BY UnitName";

                    SqlCommand cmd = new SqlCommand(query, con);

                    con.Open();

                    SqlDataReader dr = cmd.ExecuteReader();

                    while (dr.Read())
                    {
                        list.Add(new
                        {
                            UnitID = Convert.ToInt32(dr["UnitID"]),
                            UnitName = dr["UnitName"].ToString()
                        });
                    }

                    dr.Close();
                }

                return Json(new
                {
                    Status = 1,
                    Data = list
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion
        #region Unit Master
        public ActionResult UnitMaster()
        {
            ViewData["ParentMenu"] = "Inventory";
            ViewData["PageTitle"] = "Unit Master";
            return View();
        }
        [HttpPost]
        public JsonResult GetUnitList(string SearchText = "", int PageNumber = 1)
        {
            List<UnitMasterModel> list = new List<UnitMasterModel>();

            int PageSize = 10;
            int TotalRecords = 0;

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"
        SELECT *
        FROM UnitMaster
        WHERE UnitName LIKE '%' + @Search + '%'
           OR ShortName LIKE '%' + @Search + '%'
        ORDER BY UnitID DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@Search", SearchText);
                cmd.Parameters.AddWithValue("@Offset", (PageNumber - 1) * PageSize);
                cmd.Parameters.AddWithValue("@PageSize", PageSize);

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);

                foreach (DataRow dr in dt.Rows)
                {
                    list.Add(new UnitMasterModel
                    {
                        UnitID = Convert.ToInt32(dr["UnitID"]),
                        UnitName = dr["UnitName"].ToString(),
                        ShortName = dr["ShortName"].ToString(),
                        IsActive = Convert.ToBoolean(dr["IsActive"])
                    });
                }

                SqlCommand countCmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM UnitMaster
            WHERE UnitName LIKE '%' + @Search + '%'
               OR ShortName LIKE '%' + @Search + '%'", con);

                countCmd.Parameters.AddWithValue("@Search", SearchText);

                TotalRecords = Convert.ToInt32(countCmd.ExecuteScalar());
            }

            return Json(new
            {
                Status = 1,
                Data = list,
                Pagination = GeneratePagination(PageNumber, PageSize, TotalRecords)
            });
        }
        private string GeneratePagination(int pageNumber, int pageSize, int totalRecords)
        {
            int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            if (totalPages <= 1)
                return "";

            StringBuilder html = new StringBuilder();

            html.Append("<nav>");
            html.Append("<ul class='pagination justify-content-end'>");

            // Previous Button
            if (pageNumber > 1)
            {
                html.Append("<li class='page-item'>");
                html.Append("<a class='page-link' href='javascript:void(0)' onclick='GetData(" + (pageNumber - 1) + ")'>Previous</a>");
                html.Append("</li>");
            }
            else
            {
                html.Append("<li class='page-item disabled'>");
                html.Append("<span class='page-link'>Previous</span>");
                html.Append("</li>");
            }

            // Page Numbers
            int startPage = Math.Max(1, pageNumber - 2);
            int endPage = Math.Min(totalPages, pageNumber + 2);

            for (int i = startPage; i <= endPage; i++)
            {
                if (i == pageNumber)
                {
                    html.Append("<li class='page-item active'>");
                    html.Append("<span class='page-link'>" + i + "</span>");
                    html.Append("</li>");
                }
                else
                {
                    html.Append("<li class='page-item'>");
                    html.Append("<a class='page-link' href='javascript:void(0)' onclick='GetData(" + i + ")'>" + i + "</a>");
                    html.Append("</li>");
                }
            }

            // Next Button
            if (pageNumber < totalPages)
            {
                html.Append("<li class='page-item'>");
                html.Append("<a class='page-link' href='javascript:void(0)' onclick='GetData(" + (pageNumber + 1) + ")'>Next</a>");
                html.Append("</li>");
            }
            else
            {
                html.Append("<li class='page-item disabled'>");
                html.Append("<span class='page-link'>Next</span>");
                html.Append("</li>");
            }

            html.Append("</ul>");
            html.Append("</nav>");

            return html.ToString();
        }
        [HttpPost]
        public JsonResult GetUnitByID(int UnitID)
        {
            UnitMasterModel model = new UnitMasterModel();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand("SELECT * FROM UnitMaster WHERE UnitID=@UnitID", con);

                cmd.Parameters.AddWithValue("@UnitID", UnitID);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    model.UnitID = Convert.ToInt32(dr["UnitID"]);
                    model.UnitName = dr["UnitName"].ToString();
                    model.ShortName = dr["ShortName"].ToString();
                    model.IsActive = Convert.ToBoolean(dr["IsActive"]);

                    return Json(new
                    {
                        Status = 1,
                        Data = model
                    });
                }
            }

            return Json(new
            {
                Status = 0,
                Message = "Record not found."
            });
        }
        [HttpPost]
        public JsonResult SaveUnit(UnitMasterModel model)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    SqlCommand check = new SqlCommand("SELECT COUNT(*) FROM UnitMaster WHERE UnitName=@UnitName AND UnitID<>@UnitID", con);

                    check.Parameters.AddWithValue("@UnitName", model.UnitName);
                    check.Parameters.AddWithValue("@UnitID", model.UnitID);

                    if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                    {
                        return Json(new
                        {
                            Status = 0,
                            Message = "Unit Name already exists."
                        });
                    }

                    if (model.UnitID == 0)
                    {
                        string insert = @"INSERT INTO UnitMaster
                (
                    UnitName,
                    ShortName,
                    IsActive,
                    CreatedDateTime,
                    CreatedByUserID
                )
                VALUES
                (
                    @UnitName,
                    @ShortName,
                    @IsActive,
                    GETDATE(),
                    @CreatedByUserID
                )";

                        SqlCommand cmd = new SqlCommand(insert, con);

                        cmd.Parameters.AddWithValue("@UnitName", model.UnitName);
                        cmd.Parameters.AddWithValue("@ShortName", model.ShortName);
                        cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                        cmd.Parameters.AddWithValue("@CreatedByUserID", Session["UserID"] ?? 1);

                        cmd.ExecuteNonQuery();

                        return Json(new
                        {
                            Status = 1,
                            Message = "Unit Saved Successfully."
                        });
                    }
                    else
                    {
                        string update = @"UPDATE UnitMaster
                SET
                    UnitName=@UnitName,
                    ShortName=@ShortName,
                    IsActive=@IsActive
                WHERE UnitID=@UnitID";

                        SqlCommand cmd = new SqlCommand(update, con);

                        cmd.Parameters.AddWithValue("@UnitID", model.UnitID);
                        cmd.Parameters.AddWithValue("@UnitName", model.UnitName);
                        cmd.Parameters.AddWithValue("@ShortName", model.ShortName);
                        cmd.Parameters.AddWithValue("@IsActive", model.IsActive);

                        cmd.ExecuteNonQuery();

                        return Json(new
                        {
                            Status = 1,
                            Message = "Unit Updated Successfully."
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
        }
        [HttpPost]
        public JsonResult DeleteUnit(int UnitID)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    SqlCommand cmd = new SqlCommand(
                        "DELETE FROM UnitMaster WHERE UnitID=@UnitID",
                        con);

                    cmd.Parameters.AddWithValue("@UnitID", UnitID);

                    int result = cmd.ExecuteNonQuery();

                    if (result > 0)
                    {
                        return Json(new
                        {
                            Status = 1,
                            Message = "Unit Deleted Successfully."
                        });
                    }

                    return Json(new
                    {
                        Status = 0,
                        Message = "Record not found."
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
        }
        #endregion
        #region SupplierPartyMaster
        [HttpPost]
        public JsonResult GetSupplierPartyList(string Search = "", int PageNumber = 1)
        {
            try
            {
                List<SupplierPartyMaster> list = new List<SupplierPartyMaster>();

                string query = @"
                            SELECT *
                            FROM SupplierPartyMaster
                            WHERE CompanyName LIKE '%' + @Search + '%'
                               OR MobileNo LIKE '%' + @Search + '%'
                               OR GSTNo LIKE '%' + @Search + '%'
                            ORDER BY PartyID DESC";

                DataTable dt = new DataTable();

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@Search", Search);

                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }
                    }
                }

                foreach (DataRow dr in dt.Rows)
                {
                    list.Add(new SupplierPartyMaster
                    {
                        PartyID = Convert.ToInt32(dr["PartyID"]),
                        PartyType = dr["PartyType"].ToString(),
                        CompanyName = dr["CompanyName"].ToString(),
                        ContactPerson = dr["ContactPerson"].ToString(),
                        MobileNo = dr["MobileNo"].ToString(),
                        EmailAddress = dr["EmailAddress"].ToString(),
                        GSTNo = dr["GSTNo"].ToString(),
                        City = dr["City"].ToString(),
                        State = dr["State"].ToString(),
                        OpeningBalance = Convert.ToDecimal(dr["OpeningBalance"]),
                        IsActive = Convert.ToBoolean(dr["IsActive"])
                    });
                }

                return Json(new
                {
                    Status = 1,
                    Data = list,
                    ShowingText = "Total : " + list.Count,
                    Pagination = ""
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult SupplierPartyMaster()
        {
            ViewData["ParentMenu"] = "Inventory";
            ViewData["PageTitle"] = "Supplier Master";
            return View();
        }
        [HttpPost]
        public JsonResult SaveSupplierParty(SupplierPartyMaster model)
        {
            try
            {
                string query = "";

                if (model.PartyID == 0)
                {
                    query = @"INSERT INTO SupplierPartyMaster
            (
                PartyType,
                CompanyName,
                ContactPerson,
                MobileNo,
                AlternateMobileNo,
                EmailAddress,
                GSTNo,
                Address,
                City,
                State,
                Pincode,
                BankName,
                AccountNo,
                IFSCCode,
                OpeningBalance,
                IsActive,
                CreatedDateTime,
                CreatedByUserID
            )
            VALUES
            (
                @PartyType,
                @CompanyName,
                @ContactPerson,
                @MobileNo,
                @AlternateMobileNo,
                @EmailAddress,
                @GSTNo,
                @Address,
                @City,
                @State,
                @Pincode,
                @BankName,
                @AccountNo,
                @IFSCCode,
                @OpeningBalance,
                @IsActive,
                GETDATE(),
                @CreatedByUserID
            )";
                }
                else
                {
                    query = @"UPDATE SupplierPartyMaster SET

                PartyType=@PartyType,
                CompanyName=@CompanyName,
                ContactPerson=@ContactPerson,
                MobileNo=@MobileNo,
                AlternateMobileNo=@AlternateMobileNo,
                EmailAddress=@EmailAddress,
                GSTNo=@GSTNo,
                Address=@Address,
                City=@City,
                State=@State,
                Pincode=@Pincode,
                BankName=@BankName,
                AccountNo=@AccountNo,
                IFSCCode=@IFSCCode,
                OpeningBalance=@OpeningBalance,
                IsActive=@IsActive

                WHERE PartyID=@PartyID";
                }
                HttpCookie cookie = Request.Cookies["Login"];

                int CreatedByUserID = 1;

                if (cookie != null)
                {
                    CreatedByUserID = Convert.ToInt32(cookie["UserID"]);
                }
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@PartyID", model.PartyID);
                        cmd.Parameters.AddWithValue("@PartyType", model.PartyType);
                        cmd.Parameters.AddWithValue("@CompanyName", model.CompanyName);
                        cmd.Parameters.AddWithValue("@ContactPerson", model.ContactPerson);
                        cmd.Parameters.AddWithValue("@MobileNo", model.MobileNo);
                        cmd.Parameters.AddWithValue("@AlternateMobileNo", model.AlternateMobileNo);
                        cmd.Parameters.AddWithValue("@EmailAddress", model.EmailAddress);
                        cmd.Parameters.AddWithValue("@GSTNo", model.GSTNo);
                        cmd.Parameters.AddWithValue("@Address", model.Address);
                        cmd.Parameters.AddWithValue("@City", model.City);
                        cmd.Parameters.AddWithValue("@State", model.State);
                        cmd.Parameters.AddWithValue("@Pincode", model.Pincode);
                        cmd.Parameters.AddWithValue("@BankName", model.BankName);
                        cmd.Parameters.AddWithValue("@AccountNo", model.AccountNo);
                        cmd.Parameters.AddWithValue("@IFSCCode", model.IFSCCode);
                        cmd.Parameters.AddWithValue("@OpeningBalance", model.OpeningBalance);
                        cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                        cmd.Parameters.AddWithValue("@CreatedByUserID", CreatedByUserID);

                        con.Open();
                        cmd.ExecuteNonQuery();
                        con.Close();
                    }
                }

                return Json(new
                {
                    Status = 1,
                    Message = model.PartyID == 0
                        ? "Supplier Added Successfully."
                        : "Supplier Updated Successfully."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                });
            }
        }
        [HttpPost]
        public JsonResult GetSupplierPartyMaster()
        {
            try
            {
                List<SupplierPartyMaster> list = new List<SupplierPartyMaster>();

                string constr = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

                using (SqlConnection con = new SqlConnection(constr))
                {
                    string query = "SELECT * FROM SupplierPartyMaster ORDER BY PartyID DESC";

                    SqlDataAdapter da = new SqlDataAdapter(query, con);

                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    foreach (DataRow dr in dt.Rows)
                    {
                        list.Add(new SupplierPartyMaster
                        {
                            PartyID = Convert.ToInt32(dr["PartyID"]),
                            PartyType = dr["PartyType"].ToString(),
                            CompanyName = dr["CompanyName"].ToString(),
                            ContactPerson = dr["ContactPerson"].ToString(),
                            MobileNo = dr["MobileNo"].ToString(),
                            AlternateMobileNo = dr["AlternateMobileNo"].ToString(),
                            EmailAddress = dr["EmailAddress"].ToString(),
                            GSTNo = dr["GSTNo"].ToString(),
                            Address = dr["Address"].ToString(),
                            City = dr["City"].ToString(),
                            State = dr["State"].ToString(),
                            Pincode = dr["Pincode"].ToString(),
                            BankName = dr["BankName"].ToString(),
                            AccountNo = dr["AccountNo"].ToString(),
                            IFSCCode = dr["IFSCCode"].ToString(),
                            OpeningBalance = Convert.ToDecimal(dr["OpeningBalance"]),
                            IsActive = Convert.ToBoolean(dr["IsActive"])
                        });
                    }
                }

                return Json(new { Status = true, listSupplierPartyMaster = list }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message });
            }
        }
        [HttpPost]
        public JsonResult GetSupplierPartyByID(int PartyID)
        {
            try
            {
                string query = "SELECT * FROM SupplierPartyMaster WHERE PartyID = @PartyID";

                DataTable dt = new DataTable();

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@PartyID", PartyID);

                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }
                    }
                }

                if (dt.Rows.Count == 0)
                {
                    return Json(new
                    {
                        Status = 0,
                        Message = "Record Not Found"
                    }, JsonRequestBehavior.AllowGet);
                }

                DataRow dr = dt.Rows[0];

                SupplierPartyMaster obj = new SupplierPartyMaster
                {
                    PartyID = Convert.ToInt32(dr["PartyID"]),
                    PartyType = dr["PartyType"].ToString(),
                    CompanyName = dr["CompanyName"].ToString(),
                    ContactPerson = dr["ContactPerson"].ToString(),
                    MobileNo = dr["MobileNo"].ToString(),
                    AlternateMobileNo = dr["AlternateMobileNo"].ToString(),
                    EmailAddress = dr["EmailAddress"].ToString(),
                    GSTNo = dr["GSTNo"].ToString(),
                    Address = dr["Address"].ToString(),
                    City = dr["City"].ToString(),
                    State = dr["State"].ToString(),
                    Pincode = dr["Pincode"].ToString(),
                    BankName = dr["BankName"].ToString(),
                    AccountNo = dr["AccountNo"].ToString(),
                    IFSCCode = dr["IFSCCode"].ToString(),
                    OpeningBalance = Convert.ToDecimal(dr["OpeningBalance"]),
                    IsActive = Convert.ToBoolean(dr["IsActive"])
                };

                return Json(new
                {
                    Status = 1,
                    Data = obj
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult DeleteSupplierParty(int PartyID)
        {
            try
            {
                string query = "DELETE FROM SupplierPartyMaster WHERE PartyID = @PartyID";

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@PartyID", PartyID);

                        con.Open();
                        int result = cmd.ExecuteNonQuery();
                        con.Close();

                        if (result > 0)
                        {
                            return Json(new
                            {
                                Status = 1,
                                Message = "Supplier Deleted Successfully."
                            }, JsonRequestBehavior.AllowGet);
                        }
                        else
                        {
                            return Json(new
                            {
                                Status = 0,
                                Message = "Record not found."
                            }, JsonRequestBehavior.AllowGet);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        #endregion
        #region PurchaseMaster

        // Purchase List
        public ActionResult PurchaseMaster()
        {
            return View();
        }
        [HttpPost]
        public JsonResult GetPurchaseMasterList(string Search = "", int PageNumber = 1, int PageSize = 10)
        {
            try
            {
                DataTable dt = new DataTable();
                int TotalRecords = 0;

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    // Total Record Count (Updated with JOIN for search support)
                    string countQuery = @"
                SELECT COUNT(p.PurchaseInvoiceID)
                FROM PurchaseMaster p
                LEFT JOIN SupplierPartyMaster s ON p.PartyId = s.PartyID
                WHERE
                (
                    @Search = ''
                    OR p.PurchaseInvoiceNo LIKE '%' + @Search + '%'
                    OR s.CompanyName LIKE '%' + @Search + '%'
                )";

                    SqlCommand countCmd = new SqlCommand(countQuery, con);
                    countCmd.Parameters.AddWithValue("@Search", Search ?? "");

                    TotalRecords = Convert.ToInt32(countCmd.ExecuteScalar());

                    // Purchase List (Updated to fetch CompanyName)
                    string query = @"
                SELECT
                    p.PurchaseInvoiceID,
                    p.PurchaseInvoiceNo,
                    CONVERT(VARCHAR(10), p.PurchaseInvoiceDate, 103) AS PurchaseInvoiceDate,
                    p.PartyId,
                    s.CompanyName,
                    p.PaymentTerm,
                    CONVERT(VARCHAR(10), p.DueDate, 103) AS DueDate,
                    p.SubTotal,
                    p.GSTTax,
                    p.TotalAmount
                FROM PurchaseMaster p
                LEFT JOIN SupplierPartyMaster s ON p.PartyId = s.PartyID
                WHERE
                (
                    @Search = ''
                    OR p.PurchaseInvoiceNo LIKE '%' + @Search + '%'
                    OR s.CompanyName LIKE '%' + @Search + '%'
                )
                ORDER BY p.PurchaseInvoiceID DESC
                OFFSET (@Offset) ROWS
                FETCH NEXT @PageSize ROWS ONLY";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@Search", Search ?? "");
                    cmd.Parameters.AddWithValue("@Offset", (PageNumber - 1) * PageSize);
                    cmd.Parameters.AddWithValue("@PageSize", PageSize);

                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    da.Fill(dt);
                    dt.Columns.Add("EditEncryptedID");
                    dt.Columns.Add("ViewEncryptedID");

                    foreach (DataRow dr in dt.Rows)
                    {
                        dr["EditEncryptedID"] = Encrypt(dr["PurchaseInvoiceID"].ToString() + "|E");
                        dr["ViewEncryptedID"] = Encrypt(dr["PurchaseInvoiceID"].ToString() + "|V");
                    }
                }

                return Json(new
                {
                    Status = true,
                    Data = DataTableToList(dt),
                    TotalRecords = TotalRecords,
                    PageNumber = PageNumber,
                    PageSize = PageSize
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = false,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        private List<Dictionary<string, object>> DataTableToList(DataTable dt)
        {
            var list = new List<Dictionary<string, object>>();

            foreach (DataRow dr in dt.Rows)
            {
                var dict = new Dictionary<string, object>();

                foreach (DataColumn col in dt.Columns)
                {
                    dict[col.ColumnName] = dr[col];
                }

                list.Add(dict);
            }

            return list;
        }
        [HttpPost]
        public JsonResult GetGSTList()
        {
            try
            {
                List<GstSlabModel> gstList = new List<GstSlabModel>();

                string query = "SELECT GSTSLABId, GSTSLABName, GST, IGST FROM dbo.GSTSLABMaster";

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        con.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                gstList.Add(new GstSlabModel
                                {
                                    GSTSLABId = Convert.ToInt32(reader["GSTSLABId"]),
                                    GSTSLABName = Convert.ToString(reader["GSTSLABName"]),
                                    GST = Convert.ToDecimal(reader["GST"]),
                                    IGST = Convert.ToDecimal(reader["IGST"])
                                });
                            }
                        }
                    }
                }

                return Json(new { Status = true, Data = gstList });
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = "Error loading GST slabs: " + ex.Message });
            }
        }
        [CheckLogin]
        public ActionResult CreatePurchase(string id)
        {
            int PurchaseInvoiceID = 0;
            string Mode = "";

            if (!string.IsNullOrEmpty(id))
            {
                string value = Decrypt(id);
                string[] arr = value.Split('|');
                PurchaseInvoiceID = Convert.ToInt32(arr[0]);
                Mode = arr[1];
            }
            int loggedInUserId = 1; // Default fallback
            if (Request.Cookies["UserID"] != null)
            {
                int.TryParse(Request.Cookies["UserID"].Value, out loggedInUserId);
            }
            // Keep the raw encrypted query string id accessible to client-side script
            ViewBag.EncryptedID = id ?? "";
            ViewBag.PurchaseInvoiceID = PurchaseInvoiceID;
            ViewBag.Mode = Mode;
            ViewBag.LoggedInUserID = loggedInUserId;

            ViewData["ParentMenu"] = "Purchase";
            ViewData["SubMenu"] = "Purchase Master";
            ViewData["SubMenuUrl"] = Url.Action("PurchaseMaster", "bctechadmin");

            switch (Mode)
            {
                case "E":
                    ViewData["PageTitle"] = "Edit Purchase";
                    break;
                case "V":
                    ViewData["PageTitle"] = "View Purchase";
                    break;
                default:
                    ViewData["PageTitle"] = "Create Purchase";
                    break;
            }

            return View();
        }

        // View Purchase
        public ActionResult ViewPurchase(int id)
        {
            ViewBag.Mode = "View";
            ViewBag.PurchaseInvoiceID = id;

            return View("CreatePurchase");
        }

        // Save
        [HttpPost]
        public JsonResult SavePurchase(PurchaseViewModel model)
        {
            if (model == null || model.PartyId <= 0)
            {
                return Json(new { Status = false, Message = "Please select a valid supplier." });
            }

            if (model.PurchaseDetails == null || model.PurchaseDetails.Count == 0)
            {
                return Json(new { Status = false, Message = "Please add at least one item detail." });
            }

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();
                using (SqlTransaction tran = con.BeginTransaction())
                {
                    try
                    {
                        // 1. Fetch Branch State and Supplier State dynamically to check if it's Out of State
                        bool isOutOfState = false;
                        string stateQuery = @"
                    SELECT 
                        (SELECT State FROM BranchMaster WHERE BranchID = @BranchId) AS BranchState,
                        (SELECT State FROM SupplierPartyMaster WHERE PartyID = @PartyId) AS SupplierState";

                        using (SqlCommand stateCmd = new SqlCommand(stateQuery, con, tran))
                        {
                            stateCmd.Parameters.AddWithValue("@BranchId", model.BranchId);
                            stateCmd.Parameters.AddWithValue("@PartyId", model.PartyId);

                            using (SqlDataReader reader = stateCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string branchState = reader["BranchState"]?.ToString()?.Trim() ?? "";
                                    string supplierState = reader["SupplierState"]?.ToString()?.Trim() ?? "";

                                    // If states do not match (case-insensitive), it's an inter-state (IGST) transaction
                                    if (!string.Equals(branchState, supplierState, StringComparison.OrdinalIgnoreCase))
                                    {
                                        isOutOfState = true;
                                    }
                                }
                            }
                        }

                        decimal totalSubTotal = 0;
                        foreach (var item in model.PurchaseDetails)
                        {
                            decimal basic = item.Qty * item.PurchasePrice;
                            decimal discAmount = (basic * item.PurchaseDiscount) / 100;
                            totalSubTotal += (basic - discAmount);
                        }

                        // ================= Purchase Master ================= //
                        string masterQuery = @"
                INSERT INTO PurchaseMaster
                (
                    DateAndTime, PurchaseInvoiceDate, PurchaseInvoiceNo, RefOrderNo,  
                    IsOutOfState, PartyId, PaymentTerm, DueDate, SubTotal,  
                    GSTTax, Amount, NotesRemarks, PurchaseBillDiscountRate,  
                    PurchaseBillDiscountPercentage, TotalAmount, FinancialYearID,  
                    BranchId, UserId, RoundOff
                )
                VALUES
                (
                    GETDATE(), @PurchaseInvoiceDate, @PurchaseInvoiceNo, @RefOrderNo,  
                    @IsOutOfState, @PartyId, @PaymentTerm, @DueDate, @SubTotal,  
                    @GSTTax, @Amount, @NotesRemarks, @PurchaseBillDiscountRate,  
                    @PurchaseBillDiscountPercentage, @TotalAmount, @FinancialYearID,  
                    @BranchId, @UserId, @RoundOff
                );
                SELECT SCOPE_IDENTITY();";

                        int purchaseInvoiceID;
                        using (SqlCommand cmd = new SqlCommand(masterQuery, con, tran))
                        {
                            cmd.Parameters.AddWithValue("@PurchaseInvoiceDate", model.PurchaseInvoiceDate);
                            cmd.Parameters.AddWithValue("@PurchaseInvoiceNo", model.PurchaseInvoiceNo ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@RefOrderNo", model.RefOrderNo ?? "");
                            cmd.Parameters.AddWithValue("@IsOutOfState", isOutOfState); // Saved based on comparison
                            cmd.Parameters.AddWithValue("@PartyId", model.PartyId);
                            cmd.Parameters.AddWithValue("@PaymentTerm", model.PaymentTerm ?? "");
                            cmd.Parameters.AddWithValue("@DueDate", model.DueDate.HasValue ? (object)model.DueDate.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@SubTotal", model.SubTotal > 0 ? model.SubTotal : totalSubTotal);
                            cmd.Parameters.AddWithValue("@GSTTax", model.GSTTax);
                            cmd.Parameters.AddWithValue("@Amount", model.Amount);
                            cmd.Parameters.AddWithValue("@NotesRemarks", model.NotesRemarks ?? "");
                            cmd.Parameters.AddWithValue("@PurchaseBillDiscountRate", model.PurchaseBillDiscountRate);
                            cmd.Parameters.AddWithValue("@PurchaseBillDiscountPercentage", model.PurchaseBillDiscountPercentage);
                            cmd.Parameters.AddWithValue("@TotalAmount", model.TotalAmount);
                            cmd.Parameters.AddWithValue("@FinancialYearID", model.FinancialYearID);
                            cmd.Parameters.AddWithValue("@BranchId", model.BranchId);
                            cmd.Parameters.AddWithValue("@UserId", model.UserId);
                            cmd.Parameters.AddWithValue("@RoundOff", model.RoundOff);

                            purchaseInvoiceID = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        // ================= Purchase Detail ================= //
                        string detailQuery = @"
                INSERT INTO PurchaseDetail
                (
                    PurchaseInvoiceID, MaterialID, MaterialDescription, HSNCode, Qty, 
                    PurchasePrice, PurchaseDiscount, PurchaseRateAfterDiscount, GSTSLABId, 
                    CGST, CGSTRate, SGST, SGSTRate, IGST, IGSTPer, Amount, 
                    FinancialYearID, BranchId, CreatedDateTime, CreatedByUserID
                )
                VALUES
                (
                    @PurchaseInvoiceID, @MaterialID, @MaterialDescription, @HSNCode, @Qty, 
                    @PurchasePrice, @PurchaseDiscount, @PurchaseRateAfterDiscount, @GSTSLABId, 
                    @CGST, @CGSTRate, @SGST, @SGSTRate, @IGST, @IGSTPer, @Amount, 
                    @FinancialYearID, @BranchId, GETDATE(), @CreatedByUserID
                )";

                        using (SqlCommand detailCmd = new SqlCommand(detailQuery, con, tran))
                        {
                            detailCmd.Parameters.Add("@PurchaseInvoiceID", SqlDbType.BigInt);
                            detailCmd.Parameters.Add("@MaterialID", SqlDbType.BigInt);
                            detailCmd.Parameters.Add("@MaterialDescription", SqlDbType.VarChar);
                            detailCmd.Parameters.Add("@HSNCode", SqlDbType.VarChar);
                            detailCmd.Parameters.Add("@Qty", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@PurchasePrice", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@PurchaseDiscount", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@PurchaseRateAfterDiscount", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@GSTSLABId", SqlDbType.Int);
                            detailCmd.Parameters.Add("@CGST", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@CGSTRate", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@SGST", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@SGSTRate", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@IGST", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@IGSTPer", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@Amount", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@FinancialYearID", SqlDbType.Int);
                            detailCmd.Parameters.Add("@BranchId", SqlDbType.Int);
                            detailCmd.Parameters.Add("@CreatedByUserID", SqlDbType.Int);

                            foreach (var item in model.PurchaseDetails)
                            {
                                decimal basic = item.Qty * item.PurchasePrice;
                                decimal discAmt = (basic * item.PurchaseDiscount) / 100;
                                decimal taxableAmt = basic - discAmt;
                                decimal netRateAfterDiscount = item.Qty > 0 ? taxableAmt / item.Qty : item.PurchasePrice;

                                decimal cgstAmt = 0, sgstAmt = 0, igstAmt = 0;
                                decimal cgstRate = 0, sgstRate = 0, igstPer = 0;

                                decimal gstRateVal = item.IGSTPer;

                                if (isOutOfState)
                                {
                                    // Different states -> Apply IGST, reset CGST/SGST
                                    igstPer = gstRateVal;
                                    igstAmt = (taxableAmt * igstPer) / 100;
                                }
                                else
                                {
                                    // Same states -> Split into CGST & SGST, reset IGST
                                    cgstRate = gstRateVal / 2;
                                    sgstRate = gstRateVal / 2;
                                    cgstAmt = (taxableAmt * cgstRate) / 100;
                                    sgstAmt = (taxableAmt * sgstRate) / 100;
                                }

                                decimal lineTotalAmount = taxableAmt + cgstAmt + sgstAmt + igstAmt;

                                detailCmd.Parameters["@PurchaseInvoiceID"].Value = purchaseInvoiceID;
                                detailCmd.Parameters["@MaterialID"].Value = item.MaterialID;
                                detailCmd.Parameters["@MaterialDescription"].Value = item.MaterialDescription ?? "";
                                detailCmd.Parameters["@HSNCode"].Value = item.HSNCode ?? "";
                                detailCmd.Parameters["@Qty"].Value = item.Qty;
                                detailCmd.Parameters["@PurchasePrice"].Value = item.PurchasePrice;
                                detailCmd.Parameters["@PurchaseDiscount"].Value = item.PurchaseDiscount;
                                detailCmd.Parameters["@PurchaseRateAfterDiscount"].Value = netRateAfterDiscount;
                                detailCmd.Parameters["@GSTSLABId"].Value = item.GSTSLABId;
                                detailCmd.Parameters["@CGST"].Value = cgstAmt;
                                detailCmd.Parameters["@CGSTRate"].Value = cgstRate;
                                detailCmd.Parameters["@SGST"].Value = sgstAmt;
                                detailCmd.Parameters["@SGSTRate"].Value = sgstRate;
                                detailCmd.Parameters["@IGST"].Value = igstAmt;
                                detailCmd.Parameters["@IGSTPer"].Value = igstPer;
                                detailCmd.Parameters["@Amount"].Value = lineTotalAmount;
                                detailCmd.Parameters["@FinancialYearID"].Value = item.FinancialYearID > 0 ? item.FinancialYearID : model.FinancialYearID;
                                detailCmd.Parameters["@BranchId"].Value = item.BranchId > 0 ? item.BranchId : model.BranchId;
                                detailCmd.Parameters["@CreatedByUserID"].Value = model.UserId;

                                detailCmd.ExecuteNonQuery();
                            }
                        }

                        tran.Commit();

                        return Json(new
                        {
                            Status = true,
                            PurchaseInvoiceID = purchaseInvoiceID,
                            Message = "Purchase Saved Successfully."
                        });
                    }
                    catch (Exception ex)
                    {
                        tran.Rollback();
                        return Json(new
                        {
                            Status = false,
                            Message = "Error saving purchase: " + ex.Message
                        });
                    }
                }
            }
        }

        // Update
        [HttpPost]
        public JsonResult UpdatePurchase(PurchaseViewModel model)
        {
            if (model == null || model.PurchaseInvoiceID <= 0)
            {
                return Json(new { Status = false, Message = "Invalid purchase invoice ID." });
            }

            if (model.PartyId <= 0)
            {
                return Json(new { Status = false, Message = "Please select a valid supplier." });
            }

            if (model.PurchaseDetails == null || model.PurchaseDetails.Count == 0)
            {
                return Json(new { Status = false, Message = "Please add at least one item detail." });
            }

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();
                using (SqlTransaction tran = con.BeginTransaction())
                {
                    try
                    {

                        bool isOutOfState = false;
                        string stateQuery = @"
                    SELECT 
                        (SELECT State FROM BranchMaster WHERE BranchID = @BranchId) AS BranchState,
                        (SELECT State FROM SupplierPartyMaster WHERE PartyID = @PartyId) AS SupplierState";

                        using (SqlCommand stateCmd = new SqlCommand(stateQuery, con, tran))
                        {
                            stateCmd.Parameters.AddWithValue("@BranchId", model.BranchId);
                            stateCmd.Parameters.AddWithValue("@PartyId", model.PartyId);

                            using (SqlDataReader reader = stateCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string branchState = reader["BranchState"]?.ToString()?.Trim() ?? "";
                                    string supplierState = reader["SupplierState"]?.ToString()?.Trim() ?? "";


                                    if (!string.Equals(branchState, supplierState, StringComparison.OrdinalIgnoreCase))
                                    {
                                        isOutOfState = true;
                                    }
                                }
                            }
                        }


                        string masterQuery = @"
                UPDATE PurchaseMaster
                SET
                    PurchaseInvoiceDate = @PurchaseInvoiceDate,
                    PurchaseInvoiceNo = @PurchaseInvoiceNo,
                    RefOrderNo = @RefOrderNo,
                    IsOutOfState = @IsOutOfState,
                    PartyId = @PartyId,
                    PaymentTerm = @PaymentTerm,
                    DueDate = @DueDate,
                    SubTotal = @SubTotal,
                    GSTTax = @GSTTax,
                    Amount = @Amount,
                    NotesRemarks = @NotesRemarks,
                    PurchaseBillDiscountRate = @PurchaseBillDiscountRate,
                    PurchaseBillDiscountPercentage = @PurchaseBillDiscountPercentage,
                    TotalAmount = @TotalAmount,
                    FinancialYearID = @FinancialYearID,
                    BranchId = @BranchId,
                    UserId = @UserId,
                    RoundOff = @RoundOff
                WHERE PurchaseInvoiceID = @PurchaseInvoiceID";

                        using (SqlCommand cmd = new SqlCommand(masterQuery, con, tran))
                        {
                            cmd.Parameters.AddWithValue("@PurchaseInvoiceID", model.PurchaseInvoiceID);
                            cmd.Parameters.AddWithValue("@PurchaseInvoiceDate", model.PurchaseInvoiceDate);
                            cmd.Parameters.AddWithValue("@PurchaseInvoiceNo", model.PurchaseInvoiceNo ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@RefOrderNo", model.RefOrderNo ?? "");
                            cmd.Parameters.AddWithValue("@IsOutOfState", isOutOfState);
                            cmd.Parameters.AddWithValue("@PartyId", model.PartyId);
                            cmd.Parameters.AddWithValue("@PaymentTerm", model.PaymentTerm ?? "");
                            cmd.Parameters.AddWithValue("@DueDate", model.DueDate.HasValue ? (object)model.DueDate.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@SubTotal", model.SubTotal);
                            cmd.Parameters.AddWithValue("@GSTTax", model.GSTTax);
                            cmd.Parameters.AddWithValue("@Amount", model.Amount);
                            cmd.Parameters.AddWithValue("@NotesRemarks", model.NotesRemarks ?? "");
                            cmd.Parameters.AddWithValue("@PurchaseBillDiscountRate", model.PurchaseBillDiscountRate);
                            cmd.Parameters.AddWithValue("@PurchaseBillDiscountPercentage", model.PurchaseBillDiscountPercentage);
                            cmd.Parameters.AddWithValue("@TotalAmount", model.TotalAmount);
                            cmd.Parameters.AddWithValue("@FinancialYearID", model.FinancialYearID);
                            cmd.Parameters.AddWithValue("@BranchId", model.BranchId);
                            cmd.Parameters.AddWithValue("@UserId", model.UserId);
                            cmd.Parameters.AddWithValue("@RoundOff", model.RoundOff);

                            cmd.ExecuteNonQuery();
                        }

                        // ================= Delete Old Details ================= //
                        string deleteQuery = "DELETE FROM PurchaseDetail WHERE PurchaseInvoiceID = @PurchaseInvoiceID";
                        using (SqlCommand deleteCmd = new SqlCommand(deleteQuery, con, tran))
                        {
                            deleteCmd.Parameters.AddWithValue("@PurchaseInvoiceID", model.PurchaseInvoiceID);
                            deleteCmd.ExecuteNonQuery();
                        }

                        // ================= Insert New Details ================= //
                        string detailQuery = @"
                INSERT INTO PurchaseDetail
                (
                    PurchaseInvoiceID, MaterialID, MaterialDescription, HSNCode, Qty, 
                    PurchasePrice, PurchaseDiscount, PurchaseRateAfterDiscount, GSTSLABId, 
                    CGST, CGSTRate, SGST, SGSTRate, IGST, IGSTPer, Amount, 
                    FinancialYearID, BranchId, CreatedDateTime, CreatedByUserID
                )
                VALUES
                (
                    @PurchaseInvoiceID, @MaterialID, @MaterialDescription, @HSNCode, @Qty, 
                    @PurchasePrice, @PurchaseDiscount, @PurchaseRateAfterDiscount, @GSTSLABId, 
                    @CGST, @CGSTRate, @SGST, @SGSTRate, @IGST, @IGSTPer, @Amount, 
                    @FinancialYearID, @BranchId, GETDATE(), @CreatedByUserID
                )";

                        using (SqlCommand detailCmd = new SqlCommand(detailQuery, con, tran))
                        {
                            detailCmd.Parameters.Add("@PurchaseInvoiceID", SqlDbType.BigInt);
                            detailCmd.Parameters.Add("@MaterialID", SqlDbType.BigInt);
                            detailCmd.Parameters.Add("@MaterialDescription", SqlDbType.VarChar);
                            detailCmd.Parameters.Add("@HSNCode", SqlDbType.VarChar);
                            detailCmd.Parameters.Add("@Qty", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@PurchasePrice", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@PurchaseDiscount", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@PurchaseRateAfterDiscount", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@GSTSLABId", SqlDbType.Int);
                            detailCmd.Parameters.Add("@CGST", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@CGSTRate", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@SGST", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@SGSTRate", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@IGST", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@IGSTPer", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@Amount", SqlDbType.Decimal);
                            detailCmd.Parameters.Add("@FinancialYearID", SqlDbType.Int);
                            detailCmd.Parameters.Add("@BranchId", SqlDbType.Int);
                            detailCmd.Parameters.Add("@CreatedByUserID", SqlDbType.Int);

                            foreach (var item in model.PurchaseDetails)
                            {
                                decimal basic = item.Qty * item.PurchasePrice;
                                decimal discAmt = (basic * item.PurchaseDiscount) / 100;
                                decimal taxableAmt = basic - discAmt;
                                decimal netRateAfterDiscount = item.Qty > 0 ? taxableAmt / item.Qty : item.PurchasePrice;

                                decimal gstRateVal = item.IGSTPer > 0 ? item.IGSTPer : 0;
                                decimal cgstAmt = 0, sgstAmt = 0, igstAmt = 0;
                                decimal cgstRate = 0, sgstRate = 0, igstPer = 0;

                                if (isOutOfState)
                                {
                                    // Inter-state: Apply IGST only, clear CGST/SGST
                                    igstPer = gstRateVal;
                                    igstAmt = (taxableAmt * igstPer) / 100;
                                }
                                else
                                {
                                    // Intra-state (Local): Split evenly between CGST & SGST, clear IGST
                                    cgstRate = gstRateVal / 2;
                                    sgstRate = gstRateVal / 2;
                                    cgstAmt = (taxableAmt * cgstRate) / 100;
                                    sgstAmt = (taxableAmt * sgstRate) / 100;
                                }

                                decimal lineTotalAmount = taxableAmt + cgstAmt + sgstAmt + igstAmt;

                                detailCmd.Parameters["@PurchaseInvoiceID"].Value = model.PurchaseInvoiceID;
                                detailCmd.Parameters["@MaterialID"].Value = item.MaterialID;
                                detailCmd.Parameters["@MaterialDescription"].Value = item.MaterialDescription ?? "";
                                detailCmd.Parameters["@HSNCode"].Value = item.HSNCode ?? "";
                                detailCmd.Parameters["@Qty"].Value = item.Qty;
                                detailCmd.Parameters["@PurchasePrice"].Value = item.PurchasePrice;
                                detailCmd.Parameters["@PurchaseDiscount"].Value = item.PurchaseDiscount;
                                detailCmd.Parameters["@PurchaseRateAfterDiscount"].Value = netRateAfterDiscount;
                                detailCmd.Parameters["@GSTSLABId"].Value = item.GSTSLABId;
                                detailCmd.Parameters["@CGST"].Value = cgstAmt;
                                detailCmd.Parameters["@CGSTRate"].Value = cgstRate;
                                detailCmd.Parameters["@SGST"].Value = sgstAmt;
                                detailCmd.Parameters["@SGSTRate"].Value = sgstRate;
                                detailCmd.Parameters["@IGST"].Value = igstAmt;
                                detailCmd.Parameters["@IGSTPer"].Value = igstPer;
                                detailCmd.Parameters["@Amount"].Value = lineTotalAmount;
                                detailCmd.Parameters["@FinancialYearID"].Value = item.FinancialYearID > 0 ? item.FinancialYearID : model.FinancialYearID;
                                detailCmd.Parameters["@BranchId"].Value = item.BranchId > 0 ? item.BranchId : model.BranchId;
                                detailCmd.Parameters["@CreatedByUserID"].Value = model.UserId;

                                detailCmd.ExecuteNonQuery();
                            }
                        }

                        tran.Commit();

                        return Json(new
                        {
                            Status = true,
                            Message = "Purchase Updated Successfully."
                        });
                    }
                    catch (Exception ex)
                    {
                        if (tran != null)
                            tran.Rollback();

                        return Json(new
                        {
                            Status = false,
                            Message = "Error updating purchase: " + ex.Message
                        });
                    }
                }
            }
        }

        // Delete
        [HttpPost]
        public JsonResult DeletePurchase(int PurchaseInvoiceID)
        {
            try
            {
                string query = "DELETE FROM PurchaseMaster WHERE PurchaseInvoiceID=@PurchaseInvoiceID";

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@PurchaseInvoiceID", PurchaseInvoiceID);

                        con.Open();
                        cmd.ExecuteNonQuery();
                        con.Close();
                    }
                }

                return Json(new
                {
                    Status = true,
                    Message = "Purchase deleted successfully."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

        // Get By ID
        [HttpPost]
        public JsonResult GetPurchaseByID(string id)
        {
            try
            {
                DataSet ds = new DataSet();
                string decryptedValue = Decrypt(id);
                int PurchaseInvoiceID = 0;

                if (decryptedValue.Contains("|"))
                {
                    string[] arr = decryptedValue.Split('|');
                    PurchaseInvoiceID = Convert.ToInt32(arr[0]);
                }
                else
                {
                    PurchaseInvoiceID = Convert.ToInt32(decryptedValue);
                }

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    SqlDataAdapter daMaster = new SqlDataAdapter(@"
                SELECT *
                FROM PurchaseMaster
                WHERE PurchaseInvoiceID=@PurchaseInvoiceID", con);

                    daMaster.SelectCommand.Parameters.AddWithValue("@PurchaseInvoiceID", PurchaseInvoiceID);
                    daMaster.Fill(ds, "Master");

                    // FIX: Join with MaterialMaster and UnitMaster to pull MaterialName, HSN, and Unit names properly
                    SqlDataAdapter daDetail = new SqlDataAdapter(@"SELECT d.*, m.MaterialName, m.HSNCode AS MasterHSN FROM PurchaseDetail d
                                                      LEFT JOIN MaterialMaster m ON d.MaterialID = m.MaterialID WHERE d.PurchaseInvoiceID = @PurchaseInvoiceID
                                                                    ORDER BY d.PurchaseInvoiceDetailID", con);

                    daDetail.SelectCommand.Parameters.AddWithValue("@PurchaseInvoiceID", PurchaseInvoiceID);
                    daDetail.Fill(ds, "Detail");
                }

                return Json(new
                {
                    Status = true,
                    Master = DataTableToList(ds.Tables["Master"]),
                    Detail = DataTableToList(ds.Tables["Detail"])
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = false,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        // Print
        public ActionResult PrintPurchase(int id)
        {
            return View();
        }
        [HttpPost]
        public JsonResult GetMaterialItem()
        {
            try
            {
                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    string query = @"
            SELECT
                MM.MaterialID,
                MM.MaterialName,  
                MM.HSNCode,
                MM.PurchasePrice,
                MM.GSTSLABId,
                GSM.GSTSLABName,               
                GSM.GST AS GSTPercentage              
            FROM MaterialMaster MM
            LEFT JOIN GSTSLABMaster GSM
                ON MM.GSTSLABId = GSM.GSTSLABId
            ORDER BY MM.MaterialName";

                    SqlDataAdapter da = new SqlDataAdapter(query, con);

                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    foreach (DataRow dr in dt.Rows)
                    {
                        list.Add(new
                        {
                            MaterialID = Convert.ToInt32(dr["MaterialID"]),
                            MaterialName = dr["MaterialName"].ToString(),
                            HSNCode = dr["HSNCode"].ToString(),
                            PurchasePrice = Convert.ToDecimal(dr["PurchasePrice"]),
                            GSTSLABId = dr["GSTSLABId"] == DBNull.Value ? 0 : Convert.ToInt32(dr["GSTSLABId"]),
                            GSTSLABName = dr["GSTSLABName"].ToString(),
                            GSTPercentage = dr["GSTPercentage"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["GSTPercentage"])
                        });
                    }
                }

                return Json(new
                {
                    Status = true,
                    Data = list
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = false,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult GetPurchaseInvoiceNo()
        {
            string InvoiceNo = "";

            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand(@"
            SELECT ISNULL(MAX(PurchaseInvoiceID),0)+1
            FROM PurchaseMaster", con);

                InvoiceNo = cmd.ExecuteScalar().ToString();
            }

            return Json(new
            {
                Status = true,
                InvoiceNo = InvoiceNo
            });
        }
        [HttpPost]
        public JsonResult GetSupplierList()
        {
            try
            {
                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    string query = @"
                    SELECT
                         PartyID
                        ,PartyType
                        ,CompanyName
                        ,ContactPerson
                        ,MobileNo
                        ,AlternateMobileNo
                        ,EmailAddress
                        ,GSTNo
                        ,Address
                        ,City
                        ,State
                        ,Pincode
                        ,BankName
                        ,AccountNo
                        ,IFSCCode
                        ,OpeningBalance
                        ,IsActive
                        ,CreatedDateTime
                        ,CreatedByUserID
                    FROM SupplierPartyMaster
                    WHERE PartyType = 'Supplier'
                    ORDER BY CompanyName";

                    SqlCommand cmd = new SqlCommand(query, con);
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    foreach (DataRow dr in dt.Rows)
                    {
                        list.Add(new
                        {
                            PartyID = Convert.ToInt32(dr["PartyID"]),
                            CompanyName = dr["CompanyName"].ToString(),
                            MobileNo = dr["MobileNo"].ToString(),
                            EmailAddress = dr["EmailAddress"].ToString(),
                            GSTNo = dr["GSTNo"].ToString(),
                            Address = dr["Address"].ToString(),
                            State = dr["State"].ToString(),
                            City = dr["City"].ToString(),
                            Balance = dr["OpeningBalance"] != DBNull.Value ? Convert.ToDecimal(dr["OpeningBalance"]) : 0.00m
                        });
                    }
                }

                return Json(new
                {
                    Status = true,
                    Data = list
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = false,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult BranchList()
        {
            try
            {
                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();
                    string query = "SELECT BranchId, BranchName FROM BranchMaster WHERE IsActive = 1 ORDER BY BranchName";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new
                                {
                                    BranchId = Convert.ToInt32(reader["BranchId"]),
                                    BranchName = reader["BranchName"].ToString()
                                });
                            }
                        }
                    }
                }

                return Json(new { Status = true, Data = list }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult FinancialYearList()
        {
            try
            {
                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();
                    string query = "SELECT FinancialYearID, FinancialYear, IsDefault FROM FinancialYearMaster where IsDefault=1  ORDER BY FinancialYearID DESC";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new
                                {
                                    FinancialYearID = Convert.ToInt32(reader["FinancialYearID"]),
                                    FinancialYear = reader["FinancialYear"].ToString(),
                                    IsDefault = Convert.ToInt32(reader["IsDefault"])
                                });
                            }
                        }
                    }
                }

                return Json(new { Status = true, Data = list }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult GetLoggedInUserInfo()
        {
            try
            {
                int loggedInUserId = Request.Cookies["UserID"] != null ? Convert.ToInt32(Request.Cookies["UserID"].Value) : 0;
                if (loggedInUserId == 0)
                {
                    return Json(new { Status = false, Message = "Session expired or user not logged in." }, JsonRequestBehavior.AllowGet);
                }

                string userCategory = "";
                int branchId = 0;

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();
                    // Fetch UserCategory from UserMaster and BranchId from BranchMaster where BranchHeadUserID matches
                    string query = @"SELECT u.UserCategory, b.BranchID AS BranchId 
                             FROM UserMaster u 
                             LEFT JOIN BranchMaster b ON b.BranchHeadUserID = u.UserID 
                             WHERE u.UserID = @UserID";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@UserID", loggedInUserId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                userCategory = reader["UserCategory"].ToString();
                                branchId = reader["BranchID"] != DBNull.Value ? Convert.ToInt32(reader["BranchID"]) : 0;
                            }
                        }
                    }
                }

                return Json(new
                {
                    Status = true,
                    UserId = loggedInUserId,
                    UserCategory = userCategory,
                    BranchId = branchId
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        #endregion
        #region Student 

        #region View Actions

        /// <summary>
        /// Renders the Student Enquiry Listing Page.
        /// </summary>
        public ActionResult StudentEnquiryMaster()
        {
            ViewData["ParentMenu"] = "Student";
            ViewData["PageTitle"] = "Student Enquiry List";
            return View("~/Views/bctechadmin/StudentEnquiryMaster.cshtml");
        }

        /// <summary>
        /// Renders the Create / Edit / View Student Enquiry Form.
        /// </summary>
        public ActionResult CreateStudentEnquiry(string id = "", string mode = "A")
        {
            int studentEnquiryID = 0;
            string currentMode = mode;

            if (!string.IsNullOrEmpty(id))
            {
                try
                {
                    string decryptedValue = Decrypt(id);
                    if (!string.IsNullOrEmpty(decryptedValue))
                    {
                        string[] parts = decryptedValue.Split('|');
                        studentEnquiryID = Convert.ToInt32(parts[0]);
                        if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                        {
                            currentMode = parts[1];
                        }
                    }
                }
                catch
                {
                    studentEnquiryID = 0;
                    currentMode = "A";
                }
            }

            ViewBag.EncryptedID = id ?? "";
            ViewBag.StudentEnquiryID = studentEnquiryID;
            ViewBag.Mode = currentMode;

            // Fetch User ID safely from Cookie or Session
            int loggedInUserID = 0;
            if (Request.Cookies["UserID"] != null && !string.IsNullOrEmpty(Request.Cookies["UserID"].Value))
            {
                int.TryParse(Request.Cookies["UserID"].Value, out loggedInUserID);
            }
            ViewBag.LoggedInUserID = loggedInUserID;

            ViewData["ParentMenu"] = "Student";
            ViewData["SubMenu"] = "Student Enquiry List";
            ViewData["SubMenuUrl"] = Url.Action("StudentEnquiryMaster", "bctechadmin");

            switch (currentMode)
            {
                case "E":
                    ViewData["PageTitle"] = "Edit Student Enquiry";
                    break;
                case "V":
                    ViewData["PageTitle"] = "View Student Enquiry";
                    break;
                default:
                    ViewData["PageTitle"] = "Create Student Enquiry";
                    break;
            }

            return View("~/Views/bctechadmin/CreateStudentEnquiry.cshtml");
        }

        public ActionResult StudentFollowUp()
        {
            ViewData["ParentMenu"] = "Student";
            ViewData["PageTitle"] = "Follow Up";
            return View();
        }

        public ActionResult StudentAdmissionMaster()
        {
            ViewData["ParentMenu"] = "Student";
            ViewData["PageTitle"] = "Admission";
            return View();
        }
        private int GetOrganizationID()
        {
            int orgID = 1; // Default fallback OrganizationID

            // Check from Cookie
            if (Request.Cookies["OrganizationID"] != null && !string.IsNullOrEmpty(Request.Cookies["OrganizationID"].Value))
            {
                int.TryParse(Request.Cookies["OrganizationID"].Value, out orgID);
            }
            // Or Check from Session if you use Session["OrganizationID"]
            else if (Session["OrganizationID"] != null)
            {
                int.TryParse(Session["OrganizationID"].ToString(), out orgID);
            }

            return orgID;
        }
        private int GetLoggedInUserID()
        {
            int userID = 1; // Default fallback UserID

            if (Request.Cookies["UserID"] != null && !string.IsNullOrEmpty(Request.Cookies["UserID"].Value))
            {
                int.TryParse(Request.Cookies["UserID"].Value, out userID);
            }
            else if (Session["UserID"] != null)
            {
                int.TryParse(Session["UserID"].ToString(), out userID);
            }

            return userID;
        }
        [HttpPost]
        public JsonResult GetNextEnquiryNo(int branchID, int orgID = 0)
        {
            // Fallback to session/cookie OrganizationID if not explicitly passed
            if (orgID == 0)
            {
                orgID = GetOrganizationID();
            }

            string enquiryNo = "";
            using (SqlConnection conn = new SqlConnection(CS))
            {
                conn.Open();
                string branchCode = "ENQ";

                // 1. Fetch Branch Code
                string codeSql = "SELECT ISNULL(BranchCode, 'ENQ') FROM BranchMaster WHERE BranchID = @BranchID;";
                using (SqlCommand cmdCode = new SqlCommand(codeSql, conn))
                {
                    cmdCode.Parameters.AddWithValue("@BranchID", branchID);
                    object res = cmdCode.ExecuteScalar();
                    if (res != null)
                    {
                        branchCode = res.ToString().Trim();
                    }
                }

                // 2. Format Next Serial No (BranchCode + SerialNo)
                string sql = @"
            SELECT @BranchCode + '-' + RIGHT('0' + CAST(ISNULL(MAX(StudentEnquiryID), 0) + 1 AS VARCHAR(10)), 5) 
            FROM StudentEnquiryMaster 
            WHERE OrganizationID = @OrganizationID;";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@BranchCode", branchCode);
                    cmd.Parameters.AddWithValue("@OrganizationID", orgID);
                    object scalarResult = cmd.ExecuteScalar();
                    enquiryNo = scalarResult != null ? scalarResult.ToString() : branchCode + "-00001";
                }
            }

            // Return as an object so res.EnquiryNo works properly in JavaScript
            return Json(new { EnquiryNo = enquiryNo }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult GetQualificationAndSchoolAutoComplete()
        {
            try
            {
                List<string> qualifications = new List<string> {
                    "10th", "12th", "B.A.", "B.Com", "B.Sc", "B.Tech", "BCA", "MCA", "M.Tech", "M.Sc", "M.Com", "Diploma", "Post Graduate", "Other"
                };
                List<string> schools = new List<string>();

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    // Fetch distinct qualifications from DB
                    string qQuery = @"
                        SELECT DISTINCT RTRIM(LTRIM(EducationQualification)) AS Qual 
                        FROM (
                            SELECT EducationQualification FROM StudentEnquiryMaster WHERE EducationQualification IS NOT NULL AND RTRIM(LTRIM(EducationQualification)) <> ''
                            UNION
                            SELECT EducationQualification FROM StudentMaster WHERE EducationQualification IS NOT NULL AND RTRIM(LTRIM(EducationQualification)) <> ''
                        ) t";
                    using (SqlCommand cmdQ = new SqlCommand(qQuery, con))
                    {
                        using (SqlDataReader drQ = cmdQ.ExecuteReader())
                        {
                            while (drQ.Read())
                            {
                                string val = drQ["Qual"]?.ToString()?.Trim();
                                if (!string.IsNullOrEmpty(val) && !qualifications.Contains(val, StringComparer.OrdinalIgnoreCase))
                                {
                                    qualifications.Add(val);
                                }
                            }
                        }
                    }

                    // Fetch distinct school / college names from DB
                    string sQuery = @"
                        SELECT DISTINCT RTRIM(LTRIM(SchoolCollegeName)) AS School 
                        FROM (
                            SELECT SchoolCollegeName FROM StudentEnquiryMaster WHERE SchoolCollegeName IS NOT NULL AND RTRIM(LTRIM(SchoolCollegeName)) <> ''
                            UNION
                            SELECT SchoolCollegeName FROM StudentMaster WHERE SchoolCollegeName IS NOT NULL AND RTRIM(LTRIM(SchoolCollegeName)) <> ''
                        ) t";
                    using (SqlCommand cmdS = new SqlCommand(sQuery, con))
                    {
                        using (SqlDataReader drS = cmdS.ExecuteReader())
                        {
                            while (drS.Read())
                            {
                                string val = drS["School"]?.ToString()?.Trim();
                                if (!string.IsNullOrEmpty(val) && !schools.Contains(val, StringComparer.OrdinalIgnoreCase))
                                {
                                    schools.Add(val);
                                }
                            }
                        }
                    }
                }

                return Json(new { Qualifications = qualifications, Schools = schools }, JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new { Qualifications = new List<string>(), Schools = new List<string>() }, JsonRequestBehavior.AllowGet);
            }
        }
        #endregion
        #region Master Save Operation (Plain SQL Transaction)        
        [HttpPost]
        public JsonResult SaveStudentEnquiry(StudentEnquiryModel model)
        {
            ProcessResult result = new ProcessResult();

            // Set User ID from Session if available
            if (Request.Cookies["UserID"] != null && !string.IsNullOrEmpty(Request.Cookies["UserID"].Value))
            {
                model.CreatedByUserID = Convert.ToInt32(Request.Cookies["UserID"].Value);
            }

            using (SqlConnection conn = new SqlConnection(CS))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Safe DateTime parser local function
                    object ToDbDateTime(string dateStr)
                    {
                        if (DateTime.TryParse(dateStr, out DateTime dt))
                            return dt;
                        return DBNull.Value;
                    }

                    // 1. Get BranchCode AND OrganizationID for selected BranchID
                    string branchCode = "ENQ"; // Fallback prefix
                    string getBranchSql = @"
                        SELECT ISNULL(BranchCode, 'ENQ') AS BranchCode, OrganizationID 
                        FROM BranchMaster 
                        WHERE BranchID = @BranchID;";

                    using (SqlCommand cmdBranch = new SqlCommand(getBranchSql, conn, transaction))
                    {
                        cmdBranch.Parameters.AddWithValue("@BranchID", model.BranchID);
                        using (SqlDataReader reader = cmdBranch.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                branchCode = reader["BranchCode"] != DBNull.Value ? reader["BranchCode"].ToString().Trim() : "ENQ";

                                // Fixes Foreign Key issue by auto-populating OrganizationID from Branch
                                if (model.OrganizationID <= 0 && reader["OrganizationID"] != DBNull.Value)
                                {
                                    model.OrganizationID = Convert.ToInt32(reader["OrganizationID"]);
                                }
                            }
                        }
                    }

                    // Fallback to Session OrganizationID if BranchMaster didn't set it
                    if (model.OrganizationID <= 0 && Session["OrganizationID"] != null)
                    {
                        model.OrganizationID = Convert.ToInt32(Session["OrganizationID"]);
                    }

                    // 2. Generate Next Serial Number if model.EnquiryNo is empty
                    if (string.IsNullOrWhiteSpace(model.EnquiryNo))
                    {
                        string enquiryNoSql = @"
                            SELECT @BranchCode + '-' + RIGHT('0000' + CAST(ISNULL(MAX(StudentEnquiryID), 0) + 1 AS VARCHAR(10)), 5) 
                            FROM StudentEnquiryMaster 
                            WHERE OrganizationID = @OrganizationID;";

                        using (SqlCommand cmdEnqNo = new SqlCommand(enquiryNoSql, conn, transaction))
                        {
                            cmdEnqNo.Parameters.AddWithValue("@BranchCode", branchCode);
                            cmdEnqNo.Parameters.AddWithValue("@OrganizationID", model.OrganizationID);
                            model.EnquiryNo = Convert.ToString(cmdEnqNo.ExecuteScalar());
                        }
                    }

                    // 3. Insert into StudentEnquiryMaster
                    string insertMasterSql = @"
                        INSERT INTO StudentEnquiryMaster
                        (
                            OrganizationID, BranchID, EnquiryNo, EnquiryDate, EnquiryTakenByUserID,
                            StudentName, StudentMobileNo, WhatsAppNo, StudentEmail, DOB,
                            Gender, BloodGroup, EducationQualification, SchoolCollegeName, FatherName,
                            FatherMobileNo, FatherAlternateMobileNo, FatherOccupation, FatherEmail, MotherName,
                            MotherMobileNo, MotherOccupation, MotherEmail, Address, AreaLocation,
                            City, State, Pincode, EnquirySourceID, ReferencePersonName,
                            ExpectedJoiningDate, Remarks, IsFollowUpRequired, IsConvertedToAdmission, IsActive,
                            CreatedDateTime, CreatedByUserID
                        )
                        VALUES
                        (
                            @OrganizationID, @BranchID, @EnquiryNo, @EnquiryDate, @EnquiryTakenByUserID,
                            @StudentName, @StudentMobileNo, @WhatsAppNo, @StudentEmail, @DOB,
                            @Gender, @BloodGroup, @EducationQualification, @SchoolCollegeName, @FatherName,
                            @FatherMobileNo, @FatherAlternateMobileNo, @FatherOccupation, @FatherEmail, @MotherName,
                            @MotherMobileNo, @MotherOccupation, @MotherEmail, @Address, @AreaLocation,
                            @City, @State, @Pincode, @EnquirySourceID, @ReferencePersonName,
                            @ExpectedJoiningDate, @Remarks, @IsFollowUpRequired, 0, 1,
                            GETDATE(), @CreatedByUserID
                        );
                        SELECT SCOPE_IDENTITY();";

                    int newEnquiryID = 0;
                    using (SqlCommand cmdMaster = new SqlCommand(insertMasterSql, conn, transaction))
                    {
                        cmdMaster.Parameters.AddWithValue("@OrganizationID", model.OrganizationID);
                        cmdMaster.Parameters.AddWithValue("@BranchID", model.BranchID);
                        cmdMaster.Parameters.AddWithValue("@EnquiryNo", model.EnquiryNo ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@EnquiryDate", ToDbDateTime(model.EnquiryDate));
                        cmdMaster.Parameters.AddWithValue("@EnquiryTakenByUserID", model.EnquiryTakenByUserID);
                        cmdMaster.Parameters.AddWithValue("@StudentName", model.StudentName ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@StudentMobileNo", model.StudentMobileNo ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@WhatsAppNo", model.WhatsAppNo ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@StudentEmail", model.StudentEmail ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@DOB", ToDbDateTime(model.DOB));
                        cmdMaster.Parameters.AddWithValue("@Gender", model.Gender ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@BloodGroup", model.BloodGroup ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@EducationQualification", model.EducationQualification ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@SchoolCollegeName", model.SchoolCollegeName ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@FatherName", model.FatherName ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@FatherMobileNo", model.FatherMobileNo ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@FatherAlternateMobileNo", model.FatherAlternateMobileNo ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@FatherOccupation", model.FatherOccupation ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@FatherEmail", model.FatherEmail ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@MotherName", model.MotherName ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@MotherMobileNo", model.MotherMobileNo ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@MotherOccupation", model.MotherOccupation ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@MotherEmail", model.MotherEmail ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@Address", model.Address ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@AreaLocation", model.AreaLocation ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@City", model.City ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@State", model.State ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@Pincode", model.Pincode ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@EnquirySourceID", model.EnquirySourceID > 0 ? (object)model.EnquirySourceID : DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@ReferencePersonName", model.ReferencePersonName ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@ExpectedJoiningDate", ToDbDateTime(model.ExpectedJoiningDate));
                        cmdMaster.Parameters.AddWithValue("@Remarks", model.Remarks ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@IsFollowUpRequired", model.IsFollowUpRequired);
                        cmdMaster.Parameters.AddWithValue("@CreatedByUserID", model.CreatedByUserID);

                        newEnquiryID = Convert.ToInt32(cmdMaster.ExecuteScalar());
                    }

                    // 4. Insert Course List items
                    if (model.CourseList != null && model.CourseList.Count > 0)
                    {
                        string insertCourseSql = @"
                            INSERT INTO StudentEnquiryCourse
                            (StudentEnquiryID, CourseID, CourseFee, Discount, FinalFee, Remarks, CreatedDateTime, CreatedByUserID)
                            VALUES
                            (@StudentEnquiryID, @CourseID, @CourseFee, @Discount, @FinalFee, @Remarks, GETDATE(), @CreatedByUserID);";

                        foreach (var course in model.CourseList)
                        {
                            using (SqlCommand cmdCourse = new SqlCommand(insertCourseSql, conn, transaction))
                            {
                                cmdCourse.Parameters.AddWithValue("@StudentEnquiryID", newEnquiryID);
                                cmdCourse.Parameters.AddWithValue("@CourseID", course.CourseID);
                                cmdCourse.Parameters.AddWithValue("@CourseFee", course.CourseFee);
                                cmdCourse.Parameters.AddWithValue("@Discount", course.Discount);
                                cmdCourse.Parameters.AddWithValue("@FinalFee", course.FinalFee);
                                cmdCourse.Parameters.AddWithValue("@Remarks", course.Remarks ?? (object)DBNull.Value);
                                cmdCourse.Parameters.AddWithValue("@CreatedByUserID", model.CreatedByUserID);

                                cmdCourse.ExecuteNonQuery();
                            }
                        }
                    }

                    // 5. Insert FollowUp details if required
                    if (model.IsFollowUpRequired && model.FollowUpObj != null)
                    {
                        string insertFollowUpSql = @"
                            INSERT INTO StudentEnquiryFollowUp
                            (
                                StudentEnquiryID, FollowUpDate, NextFollowUpDate, FollowUpType, Discussion,
                                NextActionRemark, FollowUpByUserID, CreatedDateTime, CreatedByUserID
                            )
                            VALUES
                            (
                                @StudentEnquiryID, @FollowUpDate, @NextFollowUpDate, @FollowUpType, @Discussion,
                                @NextActionRemark, @FollowUpByUserID, GETDATE(), @CreatedByUserID
                            );";

                        using (SqlCommand cmdFollowUp = new SqlCommand(insertFollowUpSql, conn, transaction))
                        {
                            cmdFollowUp.Parameters.AddWithValue("@StudentEnquiryID", newEnquiryID);
                            cmdFollowUp.Parameters.AddWithValue("@FollowUpDate", ToDbDateTime(model.FollowUpObj.FollowUpDate));
                            cmdFollowUp.Parameters.AddWithValue("@NextFollowUpDate", ToDbDateTime(model.FollowUpObj.NextFollowUpDate));
                            cmdFollowUp.Parameters.AddWithValue("@FollowUpType", model.FollowUpObj.FollowUpType ?? (object)DBNull.Value);
                            cmdFollowUp.Parameters.AddWithValue("@Discussion", model.FollowUpObj.Discussion ?? (object)DBNull.Value);
                            cmdFollowUp.Parameters.AddWithValue("@NextActionRemark", model.FollowUpObj.NextActionRemark ?? (object)DBNull.Value);
                            cmdFollowUp.Parameters.AddWithValue("@FollowUpByUserID", model.FollowUpObj.FollowUpByUserID);
                            cmdFollowUp.Parameters.AddWithValue("@CreatedByUserID", model.CreatedByUserID);

                            cmdFollowUp.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                    result.IsSuccess = true;
                    result.Message = "Student Enquiry saved successfully with Enquiry No: " + model.EnquiryNo;
                    result.PrimaryID = newEnquiryID;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    result.IsSuccess = false;
                    result.Message = "Database Error: " + ex.Message;
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region Master Update Operation (Plain SQL Transaction)        
        [HttpPost]
        public JsonResult UpdateStudentEnquiry(StudentEnquiryModel model)
        {
            ProcessResult result = new ProcessResult();

            // 1. Get OrganizationID & UserID purely from Request Cookies if not set on model
            if (model.OrganizationID <= 0)
            {
                HttpCookie orgCookie = Request.Cookies["OrganizationID"];
                if (orgCookie != null && !string.IsNullOrEmpty(orgCookie.Value))
                {
                    int.TryParse(orgCookie.Value, out int orgId);
                    model.OrganizationID = orgId;
                }
            }

            if (model.CreatedByUserID <= 0)
            {
                HttpCookie userCookie = Request.Cookies["UserID"];
                if (userCookie != null && !string.IsNullOrEmpty(userCookie.Value))
                {
                    int.TryParse(userCookie.Value, out int userId);
                    model.CreatedByUserID = userId;
                }
            }

            // Default fallback if cookie is missing or invalid
            if (model.OrganizationID <= 0)
            {
                model.OrganizationID = 1;
            }

            using (SqlConnection conn = new SqlConnection(CS))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Helper function for safe DateTime conversion
                    object ToDbDateTime(string dateStr)
                    {
                        if (DateTime.TryParse(dateStr, out DateTime dt))
                            return dt;
                        return DBNull.Value;
                    }

                    // 1. Update Master Record
                    string updateMasterSql = @"
                UPDATE StudentEnquiryMaster
                SET
                    BranchID = @BranchID,
                    EnquiryDate = @EnquiryDate,
                    EnquiryTakenByUserID = @EnquiryTakenByUserID,
                    StudentName = @StudentName,
                    StudentMobileNo = @StudentMobileNo,
                    WhatsAppNo = @WhatsAppNo,
                    StudentEmail = @StudentEmail,
                    DOB = @DOB,
                    Gender = @Gender,
                    BloodGroup = @BloodGroup,
                    EducationQualification = @EducationQualification,
                    SchoolCollegeName = @SchoolCollegeName,
                    FatherName = @FatherName,
                    FatherMobileNo = @FatherMobileNo,
                    FatherAlternateMobileNo = @FatherAlternateMobileNo,
                    FatherOccupation = @FatherOccupation,
                    FatherEmail = @FatherEmail,
                    MotherName = @MotherName,
                    MotherMobileNo = @MotherMobileNo,
                    MotherOccupation = @MotherOccupation,
                    MotherEmail = @MotherEmail,
                    Address = @Address,
                    AreaLocation = @AreaLocation,
                    City = @City,
                    State = @State,
                    Pincode = @Pincode,
                    EnquirySourceID = @EnquirySourceID,
                    ReferencePersonName = @ReferencePersonName,
                    ExpectedJoiningDate = @ExpectedJoiningDate,
                    Remarks = @Remarks,
                    IsFollowUpRequired = @IsFollowUpRequired                    
                WHERE StudentEnquiryID = @StudentEnquiryID AND OrganizationID = @OrganizationID;";

                    using (SqlCommand cmdMaster = new SqlCommand(updateMasterSql, conn, transaction))
                    {
                        cmdMaster.Parameters.AddWithValue("@StudentEnquiryID", model.StudentEnquiryID);
                        cmdMaster.Parameters.AddWithValue("@OrganizationID", model.OrganizationID);
                        cmdMaster.Parameters.AddWithValue("@BranchID", model.BranchID);
                        cmdMaster.Parameters.AddWithValue("@EnquiryDate", ToDbDateTime(model.EnquiryDate));
                        cmdMaster.Parameters.AddWithValue("@EnquiryTakenByUserID", model.EnquiryTakenByUserID);
                        cmdMaster.Parameters.AddWithValue("@StudentName", model.StudentName ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@StudentMobileNo", model.StudentMobileNo ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@WhatsAppNo", model.WhatsAppNo ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@StudentEmail", model.StudentEmail ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@DOB", ToDbDateTime(model.DOB));
                        cmdMaster.Parameters.AddWithValue("@Gender", model.Gender ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@BloodGroup", model.BloodGroup ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@EducationQualification", model.EducationQualification ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@SchoolCollegeName", model.SchoolCollegeName ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@FatherName", model.FatherName ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@FatherMobileNo", model.FatherMobileNo ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@FatherAlternateMobileNo", model.FatherAlternateMobileNo ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@FatherOccupation", model.FatherOccupation ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@FatherEmail", model.FatherEmail ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@MotherName", model.MotherName ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@MotherMobileNo", model.MotherMobileNo ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@MotherOccupation", model.MotherOccupation ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@MotherEmail", model.MotherEmail ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@Address", model.Address ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@AreaLocation", model.AreaLocation ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@City", model.City ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@State", model.State ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@Pincode", model.Pincode ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@EnquirySourceID", model.EnquirySourceID > 0 ? (object)model.EnquirySourceID : DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@ReferencePersonName", model.ReferencePersonName ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@ExpectedJoiningDate", ToDbDateTime(model.ExpectedJoiningDate));
                        cmdMaster.Parameters.AddWithValue("@Remarks", model.Remarks ?? (object)DBNull.Value);
                        cmdMaster.Parameters.AddWithValue("@IsFollowUpRequired", model.IsFollowUpRequired);

                        cmdMaster.ExecuteNonQuery();
                    }

                    // 2. Clear Existing Courses and Re-insert
                    string deleteCoursesSql = "DELETE FROM StudentEnquiryCourse WHERE StudentEnquiryID = @StudentEnquiryID;";
                    using (SqlCommand cmdDelCourse = new SqlCommand(deleteCoursesSql, conn, transaction))
                    {
                        cmdDelCourse.Parameters.AddWithValue("@StudentEnquiryID", model.StudentEnquiryID);
                        cmdDelCourse.ExecuteNonQuery();
                    }

                    if (model.CourseList != null && model.CourseList.Count > 0)
                    {
                        string insertCourseSql = @"
                    INSERT INTO StudentEnquiryCourse
                    (StudentEnquiryID, CourseID, CourseFee, Discount, FinalFee, Remarks, CreatedDateTime, CreatedByUserID)
                    VALUES
                    (@StudentEnquiryID, @CourseID, @CourseFee, @Discount, @FinalFee, @Remarks, GETDATE(), @CreatedByUserID);";

                        foreach (var course in model.CourseList)
                        {
                            using (SqlCommand cmdCourse = new SqlCommand(insertCourseSql, conn, transaction))
                            {
                                cmdCourse.Parameters.AddWithValue("@StudentEnquiryID", model.StudentEnquiryID);
                                cmdCourse.Parameters.AddWithValue("@CourseID", course.CourseID);
                                cmdCourse.Parameters.AddWithValue("@CourseFee", course.CourseFee);
                                cmdCourse.Parameters.AddWithValue("@Discount", course.Discount);
                                cmdCourse.Parameters.AddWithValue("@FinalFee", course.FinalFee);
                                cmdCourse.Parameters.AddWithValue("@Remarks", course.Remarks ?? (object)DBNull.Value);
                                cmdCourse.Parameters.AddWithValue("@CreatedByUserID", model.CreatedByUserID);

                                cmdCourse.ExecuteNonQuery();
                            }
                        }
                    }

                    // 3. Insert or Update FollowUp details
                    if (model.IsFollowUpRequired && model.FollowUpObj != null)
                    {
                        if (model.FollowUpObj.StudentEnquiryFollowUpID == 0)
                        {
                            // New FollowUp Record
                            string insertFollowUpSql = @"
                        INSERT INTO StudentEnquiryFollowUp
                        (
                            StudentEnquiryID, FollowUpDate, NextFollowUpDate, FollowUpType, Discussion,
                            NextActionRemark, FollowUpByUserID, CreatedDateTime, CreatedByUserID
                        )
                        VALUES
                        (
                            @StudentEnquiryID, @FollowUpDate, @NextFollowUpDate, @FollowUpType, @Discussion,
                            @NextActionRemark, @FollowUpByUserID, GETDATE(), @CreatedByUserID
                        );";

                            using (SqlCommand cmdFollowUp = new SqlCommand(insertFollowUpSql, conn, transaction))
                            {
                                cmdFollowUp.Parameters.AddWithValue("@StudentEnquiryID", model.StudentEnquiryID);
                                cmdFollowUp.Parameters.AddWithValue("@FollowUpDate", ToDbDateTime(model.FollowUpObj.FollowUpDate));
                                cmdFollowUp.Parameters.AddWithValue("@NextFollowUpDate", ToDbDateTime(model.FollowUpObj.NextFollowUpDate));
                                cmdFollowUp.Parameters.AddWithValue("@FollowUpType", model.FollowUpObj.FollowUpType ?? (object)DBNull.Value);
                                cmdFollowUp.Parameters.AddWithValue("@Discussion", model.FollowUpObj.Discussion ?? (object)DBNull.Value);
                                cmdFollowUp.Parameters.AddWithValue("@NextActionRemark", model.FollowUpObj.NextActionRemark ?? (object)DBNull.Value);
                                cmdFollowUp.Parameters.AddWithValue("@FollowUpByUserID", model.FollowUpObj.FollowUpByUserID);
                                cmdFollowUp.Parameters.AddWithValue("@CreatedByUserID", model.CreatedByUserID);

                                cmdFollowUp.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // Update Existing FollowUp Record
                            string updateFollowUpSql = @"
                        UPDATE StudentEnquiryFollowUp
                        SET
                            FollowUpDate = @FollowUpDate,
                            NextFollowUpDate = @NextFollowUpDate,
                            FollowUpType = @FollowUpType,
                            Discussion = @Discussion,
                            NextActionRemark = @NextActionRemark,
                            FollowUpByUserID = @FollowUpByUserID                          
                        WHERE StudentEnquiryFollowUpID = @StudentEnquiryFollowUpID;";

                            using (SqlCommand cmdFollowUp = new SqlCommand(updateFollowUpSql, conn, transaction))
                            {
                                cmdFollowUp.Parameters.AddWithValue("@StudentEnquiryFollowUpID", model.FollowUpObj.StudentEnquiryFollowUpID);
                                cmdFollowUp.Parameters.AddWithValue("@FollowUpDate", ToDbDateTime(model.FollowUpObj.FollowUpDate));
                                cmdFollowUp.Parameters.AddWithValue("@NextFollowUpDate", ToDbDateTime(model.FollowUpObj.NextFollowUpDate));
                                cmdFollowUp.Parameters.AddWithValue("@FollowUpType", model.FollowUpObj.FollowUpType ?? (object)DBNull.Value);
                                cmdFollowUp.Parameters.AddWithValue("@Discussion", model.FollowUpObj.Discussion ?? (object)DBNull.Value);
                                cmdFollowUp.Parameters.AddWithValue("@NextActionRemark", model.FollowUpObj.NextActionRemark ?? (object)DBNull.Value);
                                cmdFollowUp.Parameters.AddWithValue("@FollowUpByUserID", model.FollowUpObj.FollowUpByUserID);


                                cmdFollowUp.ExecuteNonQuery();
                            }
                        }
                    }

                    transaction.Commit();
                    result.IsSuccess = true;
                    result.Message = "Student Enquiry updated successfully.";
                    result.PrimaryID = model.StudentEnquiryID;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    result.IsSuccess = false;
                    result.Message = "Database Error: " + ex.Message;
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region Soft Delete Operation        
        [HttpPost]
        public JsonResult DeleteStudentEnquiry(string id, int deletedByUserID = 0)
        {
            ProcessResult result = new ProcessResult();

            // 1. Decrypt incoming ID string
            int studentEnquiryID = 0;
            if (!string.IsNullOrEmpty(id))
            {
                string decryptedStr = Decrypt(id); // Your Decrypt helper
                if (decryptedStr.Contains("|"))
                {
                    decryptedStr = decryptedStr.Split('|')[0];
                }
                int.TryParse(decryptedStr, out studentEnquiryID);
            }

            if (studentEnquiryID <= 0)
            {
                result.IsSuccess = false;
                result.Message = "Invalid Record ID.";
                return Json(result, JsonRequestBehavior.AllowGet);
            }

            // 2. Fetch UserID from cookie if not provided
            if (deletedByUserID <= 0)
            {
                HttpCookie userCookie = Request.Cookies["UserID"];
                if (userCookie != null && !string.IsNullOrEmpty(userCookie.Value))
                {
                    int.TryParse(userCookie.Value, out deletedByUserID);
                }
            }

            // 3. Soft Delete Query
            string sql = @"
                UPDATE StudentEnquiryMaster
                SET IsActive = 0                   
                WHERE StudentEnquiryID = @StudentEnquiryID AND IsActive = 1;";

            using (SqlConnection conn = new SqlConnection(CS))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@StudentEnquiryID", studentEnquiryID);

                    try
                    {
                        conn.Open();
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            result.IsSuccess = true;
                            result.Message = "Student Enquiry deleted successfully.";
                            result.PrimaryID = studentEnquiryID;
                        }
                        else
                        {
                            result.IsSuccess = false;
                            result.Message = "Record not found or already deleted.";
                        }
                    }
                    catch (Exception ex)
                    {
                        result.IsSuccess = false;
                        result.Message = "Database Error: " + ex.Message;
                    }
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region List Fetch Operation (With Pagination & Search)       
        [HttpGet]
        public JsonResult GetStudentEnquiryList(StudentEnquiryFilterParam param, int orgID = 0)
        {
            StudentEnquiryListResponse response = new StudentEnquiryListResponse();
            response.Data = new List<StudentEnquiryModel>();

            // 1. Get OrganizationID purely from Request Cookies (Fallback order: parameter -> Cookie -> Default)
            if (orgID <= 0)
            {
                HttpCookie orgCookie = Request.Cookies["OrganizationID"];
                if (orgCookie != null && !string.IsNullOrEmpty(orgCookie.Value))
                {
                    int.TryParse(orgCookie.Value, out orgID);
                }
            }

            // Default fallback if cookie is missing or invalid
            if (orgID <= 0)
            {
                orgID = 1;
            }

            int branchId = 0;
            if (Request.Cookies["BranchID"] != null)
            {
                int.TryParse(Request.Cookies["BranchID"].Value, out branchId);
            }
            if (branchId == 0 && Session["BranchID"] != null)
            {
                int.TryParse(Session["BranchID"].ToString(), out branchId);
            }

            string branchFilter = (branchId > 0) ? " AND e.BranchID = @BranchID " : "";

            // 2. Pagination parameter safety
            int pageNumber = (param != null && param.PageNumber > 0) ? param.PageNumber : 1;
            int pageSize = (param != null && param.PageSize > 0) ? param.PageSize : 10;
            int offset = (pageNumber - 1) * pageSize;
            string searchText = (param != null && !string.IsNullOrWhiteSpace(param.SearchText)) ? param.SearchText.Trim() : "";

            // 3. Dynamic Search Filter
            string searchFilter = "";
            if (!string.IsNullOrEmpty(searchText))
            {
                searchFilter = @" AND (e.StudentName LIKE '%' + @SearchText + '%' 
                            OR e.StudentMobileNo LIKE '%' + @SearchText + '%' 
                            OR e.EnquiryNo LIKE '%' + @SearchText + '%') ";
            }

            // 4. SQL Queries
            string countSql = $@"
        SELECT COUNT(1) 
        FROM StudentEnquiryMaster e 
        WHERE e.IsConvertedToAdmission=0  and e.IsActive = 1 AND e.OrganizationID = @OrganizationID {branchFilter} {searchFilter};";

            string dataSql = $@"
        SELECT 
            e.StudentEnquiryID,
            e.EnquiryNo,
            e.EnquiryDate,
            e.StudentName,
            e.StudentMobileNo,
            e.IsFollowUpRequired,
            e.IsConvertedToAdmission,
            b.BranchName,
            s.EnquirySourceType,
            STUFF((
                SELECT ', ' + c.CourseTitle
                FROM StudentEnquiryCourse ec
                INNER JOIN CourseMaster c ON ec.CourseID = c.CourseID
                WHERE ec.StudentEnquiryID = e.StudentEnquiryID
                FOR XML PATH('')
            ), 1, 2, '') AS CourseNames
        FROM StudentEnquiryMaster e
        LEFT JOIN BranchMaster b ON e.BranchID = b.BranchID
        LEFT JOIN EnquirySourceMaster s ON e.EnquirySourceID = s.EnquirySourceID
        WHERE e.IsActive = 1 AND e.OrganizationID = @OrganizationID {branchFilter} {searchFilter}
        ORDER BY e.StudentEnquiryID DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            try
            {
                using (SqlConnection conn = new SqlConnection(CS))
                {
                    conn.Open();

                    // 5. Execute Count Query
                    using (SqlCommand cmdCount = new SqlCommand(countSql, conn))
                    {
                        cmdCount.Parameters.AddWithValue("@OrganizationID", orgID);
                        if (branchId > 0)
                        {
                            cmdCount.Parameters.AddWithValue("@BranchID", branchId);
                        }
                        if (!string.IsNullOrEmpty(searchText))
                        {
                            cmdCount.Parameters.AddWithValue("@SearchText", searchText);
                        }
                        response.TotalRecords = Convert.ToInt32(cmdCount.ExecuteScalar());
                    }

                    // 6. Execute Data Fetch Query
                    using (SqlCommand cmdData = new SqlCommand(dataSql, conn))
                    {
                        cmdData.Parameters.AddWithValue("@OrganizationID", orgID);
                        if (branchId > 0)
                        {
                            cmdData.Parameters.AddWithValue("@BranchID", branchId);
                        }
                        if (!string.IsNullOrEmpty(searchText))
                        {
                            cmdData.Parameters.AddWithValue("@SearchText", searchText);
                        }
                        cmdData.Parameters.AddWithValue("@Offset", offset);
                        cmdData.Parameters.AddWithValue("@PageSize", pageSize);

                        using (SqlDataReader reader = cmdData.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int enquiryId = Convert.ToInt32(reader["StudentEnquiryID"]);

                                // ✅ Direct object mapping with mode-based encryption
                                StudentEnquiryModel item = new StudentEnquiryModel
                                {
                                    StudentEnquiryID = enquiryId,
                                    EditEncryptedID = Encrypt(enquiryId + "|E"),
                                    ViewEncryptedID = Encrypt(enquiryId + "|V"),
                                    EnquiryNo = Convert.ToString(reader["EnquiryNo"]),
                                    EnquiryDate = reader["EnquiryDate"] != DBNull.Value
                                        ? Convert.ToDateTime(reader["EnquiryDate"]).ToString("yyyy-MM-dd")
                                        : "",
                                    StudentName = Convert.ToString(reader["StudentName"]),
                                    StudentMobileNo = Convert.ToString(reader["StudentMobileNo"]),
                                    IsFollowUpRequired = reader["IsFollowUpRequired"] != DBNull.Value && Convert.ToBoolean(reader["IsFollowUpRequired"]),
                                    IsConvertedToAdmission = reader["IsConvertedToAdmission"] != DBNull.Value && Convert.ToBoolean(reader["IsConvertedToAdmission"]),
                                    BranchName = Convert.ToString(reader["BranchName"]),
                                    SourceName = Convert.ToString(reader["EnquirySourceType"]),
                                    CourseNames = Convert.ToString(reader["CourseNames"])
                                };

                                response.Data.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = "Database Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }

            return Json(response, JsonRequestBehavior.AllowGet);
        }
        #endregion

        #region Get By ID Operation        
        [HttpGet]
        public ActionResult GetStudentEnquiryByID(string id)
        {
            try
            {
                string decryptedStr = Decrypt(id);
                if (!string.IsNullOrEmpty(decryptedStr) && decryptedStr.Contains("|"))
                {
                    decryptedStr = decryptedStr.Split('|')[0];
                }

                int enquiryId = 0;
                if (!int.TryParse(decryptedStr, out enquiryId))
                {
                    int.TryParse(id, out enquiryId);
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    // 1. Fetch Student Enquiry Master Details
                    string query = @"
                SELECT 
                    se.StudentEnquiryID,
                    se.EnquiryNo,
                    se.EnquiryDate,
                    se.EnquiryTakenByUserID,
                    se.BranchID,
                    se.StudentName,
                    se.StudentMobileNo,
                    se.WhatsAppNo,
                    se.StudentEmail,
                    se.DOB,
                    se.Gender,
                    se.BloodGroup,
                    se.EducationQualification,
                    se.SchoolCollegeName,
                    se.FatherName,
                    se.FatherMobileNo,
                    se.FatherAlternateMobileNo,
                    se.FatherOccupation,
                    se.FatherEmail,
                    se.MotherName,
                    se.MotherMobileNo,
                    se.MotherOccupation,
                    se.MotherEmail,
                    se.Address,
                    se.AreaLocation,
                    se.City,
                    se.State,
                    se.Pincode,
                    se.EnquirySourceID,
                    se.ReferencePersonName,
                    se.ExpectedJoiningDate,
                    se.Remarks,
                    se.IsFollowUpRequired
                FROM StudentEnquiryMaster se
                WHERE se.StudentEnquiryID = @StudentEnquiryID";

                    var data = new Dictionary<string, object>();

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@StudentEnquiryID", enquiryId);

                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                data["StudentEnquiryID"] = Convert.ToInt32(dr["StudentEnquiryID"]);
                                data["EnquiryNo"] = dr["EnquiryNo"]?.ToString() ?? "";
                                data["EnquiryDate"] = dr["EnquiryDate"] != DBNull.Value ? Convert.ToDateTime(dr["EnquiryDate"]).ToString("yyyy-MM-dd") : "";
                                data["EnquiryTakenByUserID"] = dr["EnquiryTakenByUserID"] != DBNull.Value ? Convert.ToInt32(dr["EnquiryTakenByUserID"]) : 0;
                                data["BranchID"] = dr["BranchID"] != DBNull.Value ? Convert.ToInt32(dr["BranchID"]) : 0;
                                data["StudentName"] = dr["StudentName"]?.ToString() ?? "";
                                data["StudentMobileNo"] = dr["StudentMobileNo"]?.ToString() ?? "";
                                data["WhatsAppNo"] = dr["WhatsAppNo"]?.ToString() ?? "";
                                data["StudentEmail"] = dr["StudentEmail"]?.ToString() ?? "";
                                data["DOB"] = dr["DOB"] != DBNull.Value ? Convert.ToDateTime(dr["DOB"]).ToString("yyyy-MM-dd") : "";
                                data["Gender"] = dr["Gender"]?.ToString() ?? "";
                                data["BloodGroup"] = dr["BloodGroup"]?.ToString() ?? "";
                                data["EducationQualification"] = dr["EducationQualification"]?.ToString() ?? "";
                                data["SchoolCollegeName"] = dr["SchoolCollegeName"]?.ToString() ?? "";
                                data["FatherName"] = dr["FatherName"]?.ToString() ?? "";
                                data["FatherMobileNo"] = dr["FatherMobileNo"]?.ToString() ?? "";
                                data["FatherAlternateMobileNo"] = dr["FatherAlternateMobileNo"]?.ToString() ?? "";
                                data["FatherOccupation"] = dr["FatherOccupation"]?.ToString() ?? "";
                                data["FatherEmail"] = dr["FatherEmail"]?.ToString() ?? "";
                                data["MotherName"] = dr["MotherName"]?.ToString() ?? "";
                                data["MotherMobileNo"] = dr["MotherMobileNo"]?.ToString() ?? "";
                                data["MotherOccupation"] = dr["MotherOccupation"]?.ToString() ?? "";
                                data["MotherEmail"] = dr["MotherEmail"]?.ToString() ?? "";
                                data["Address"] = dr["Address"]?.ToString() ?? "";
                                data["AreaLocation"] = dr["AreaLocation"]?.ToString() ?? "";
                                data["City"] = dr["City"]?.ToString() ?? "";
                                data["State"] = dr["State"]?.ToString() ?? "";
                                data["Pincode"] = dr["Pincode"]?.ToString() ?? "";
                                data["EnquirySourceID"] = dr["EnquirySourceID"] != DBNull.Value ? Convert.ToInt32(dr["EnquirySourceID"]) : 0;
                                data["ReferencePersonName"] = dr["ReferencePersonName"]?.ToString() ?? "";
                                data["ExpectedJoiningDate"] = dr["ExpectedJoiningDate"] != DBNull.Value ? Convert.ToDateTime(dr["ExpectedJoiningDate"]).ToString("yyyy-MM-dd") : "";
                                data["Remarks"] = dr["Remarks"]?.ToString() ?? "";
                                data["IsFollowUpRequired"] = dr["IsFollowUpRequired"] != DBNull.Value ? Convert.ToBoolean(dr["IsFollowUpRequired"]) : false;
                            }
                            else
                            {
                                return Json(new { IsSuccess = false, Message = "Enquiry record not found." }, JsonRequestBehavior.AllowGet);
                            }
                        }
                    }

                    // 2. Fetch Enquiry Courses
                    List<object> courseList = new List<object>();
                    string courseQuery = @"
                SELECT 
                    CourseID, 
                    ISNULL(CourseFee, 0) AS CourseFee, 
                    ISNULL(Discount, 0) AS Discount, 
                    ISNULL(FinalFee, 0) AS FinalFee, 
                    ISNULL(Remarks, '') AS Remarks
                FROM StudentEnquiryCourse
                WHERE StudentEnquiryID = @StudentEnquiryID";

                    using (SqlCommand cmdCourse = new SqlCommand(courseQuery, con))
                    {
                        cmdCourse.Parameters.AddWithValue("@StudentEnquiryID", enquiryId);

                        using (SqlDataReader drCourse = cmdCourse.ExecuteReader())
                        {
                            while (drCourse.Read())
                            {
                                courseList.Add(new
                                {
                                    CourseID = Convert.ToInt32(drCourse["CourseID"]),
                                    CourseFee = Convert.ToDecimal(drCourse["CourseFee"]),
                                    Discount = Convert.ToDecimal(drCourse["Discount"]),
                                    FinalFee = Convert.ToDecimal(drCourse["FinalFee"]),
                                    Remarks = drCourse["Remarks"]?.ToString() ?? ""
                                });
                            }
                        }
                    }

                    data["CourseList"] = courseList;

                    // 3. Fetch All FollowUp Records if exist
                    List<object> followUpList = new List<object>();
                    try
                    {
                        string followUpQuery = @"
                    SELECT 
                        f.StudentEnquiryFollowUpID,
                        CONVERT(VARCHAR(16), f.FollowUpDate, 120) AS FollowUpDate,
                        CONVERT(VARCHAR(16), f.NextFollowUpDate, 120) AS NextFollowUpDate,
                        ISNULL(f.FollowUpType, '') AS FollowUpType,
                        ISNULL(f.Discussion, '') AS Discussion,
                        ISNULL(f.NextActionRemark, '') AS NextActionRemark,
                        ISNULL(f.FollowUpByUserID, 0) AS FollowUpByUserID,
                        ISNULL(u.Username, 'Admin') AS FollowUpByUserName
                    FROM StudentEnquiryFollowUp f
                    LEFT JOIN UserMaster u ON f.FollowUpByUserID = u.UserID
                    WHERE f.StudentEnquiryID = @StudentEnquiryID
                    ORDER BY f.StudentEnquiryFollowUpID DESC";

                        using (SqlCommand cmdFU = new SqlCommand(followUpQuery, con))
                        {
                            cmdFU.Parameters.AddWithValue("@StudentEnquiryID", enquiryId);
                            using (SqlDataReader drFU = cmdFU.ExecuteReader())
                            {
                                while (drFU.Read())
                                {
                                    followUpList.Add(new
                                    {
                                        StudentEnquiryFollowUpID = Convert.ToInt32(drFU["StudentEnquiryFollowUpID"]),
                                        FollowUpDate = drFU["FollowUpDate"]?.ToString() ?? "",
                                        NextFollowUpDate = drFU["NextFollowUpDate"]?.ToString() ?? "",
                                        FollowUpType = drFU["FollowUpType"]?.ToString() ?? "",
                                        Discussion = drFU["Discussion"]?.ToString() ?? "",
                                        NextActionRemark = drFU["NextActionRemark"]?.ToString() ?? "",
                                        FollowUpByUserID = Convert.ToInt32(drFU["FollowUpByUserID"]),
                                        FollowUpByUserName = drFU["FollowUpByUserName"]?.ToString() ?? ""
                                    });
                                }
                            }
                        }
                        data["FollowUpList"] = followUpList;
                        data["FollowUpObj"] = followUpList.FirstOrDefault();
                    }
                    catch
                    {
                        data["FollowUpList"] = followUpList;
                        data["FollowUpObj"] = null;
                    }

                    return Json(new { IsSuccess = true, Data = data }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        #endregion

        #region Dropdown Lookup Queries (Plain SQL)
        [HttpPost]
        public JsonResult GetCourseDropdown(int orgID = 0)
        {
            List<dynamic> list = new List<dynamic>();
            string sql = "SELECT CourseID, CourseTitle, ISNULL(CourseFee, 0) AS CourseFee FROM CourseMaster WHERE IsActive = 1 ORDER BY CourseTitle;";

            using (SqlConnection conn = new SqlConnection(CS))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new
                            {
                                ID = Convert.ToInt32(reader["CourseID"]),
                                Name = Convert.ToString(reader["CourseTitle"]),
                                Fee = Convert.ToDecimal(reader["CourseFee"])
                            });
                        }
                    }
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public JsonResult GetIsStudentCourseDropdown(int orgID = 0)
        {
            List<dynamic> list = new List<dynamic>();
            string sql = "SELECT CourseID, CourseTitle, ISNULL(CourseFee, 0) AS CourseFee FROM CourseMaster WHERE IsActive = 1 and IsStudentCourse = 1 ORDER BY CourseTitle;";

            using (SqlConnection conn = new SqlConnection(CS))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new
                            {
                                ID = Convert.ToInt32(reader["CourseID"]),
                                Name = Convert.ToString(reader["CourseTitle"]),
                                Fee = Convert.ToDecimal(reader["CourseFee"])
                            });
                        }
                    }
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }


        // ==========================================
        // Branch Dropdown
        // ==========================================
        [HttpPost]
        public ActionResult GetBranchDropdown()
        {
            try
            {
                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(CS))
                {
                    // Ensure BranchID and BranchName are explicitly queried
                    string query = @"SELECT BranchID, BranchName FROM BranchMaster WHERE ISNULL(IsActive, 1) = 1 ORDER BY BranchID ASC";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        con.Open();
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new
                                {
                                    BranchID = Convert.ToInt32(dr["BranchID"]), // Explicit ID
                                    BranchName = dr["BranchName"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);
            }
        }

        // ==========================================
        // User Dropdown
        // ==========================================
        [HttpPost]
        public JsonResult GetUserDropdown(int orgID = 0)
        {
            List<DropdownItem> list = new List<DropdownItem>();

            using (SqlConnection conn = new SqlConnection(CS))
            {
                string sql = @"
                    SELECT
                        UserID,
                        LTRIM(RTRIM(
                            ISNULL(FirstName,'') +
                            CASE WHEN ISNULL(MiddleName,'')<>'' THEN ' '+MiddleName ELSE '' END +
                            CASE WHEN ISNULL(LastName,'')<>'' THEN ' '+LastName ELSE '' END
                        )) AS FullName
                    FROM UserMaster
                    WHERE IsActive = 1
                    ORDER BY FirstName, LastName";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();

                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string name = dr["FullName"].ToString();

                            list.Add(new DropdownItem
                            {
                                ID = Convert.ToInt32(dr["UserID"]),
                                Name = string.IsNullOrWhiteSpace(name)
                                        ? "User #" + dr["UserID"].ToString()
                                        : name
                            });
                        }
                    }
                }
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }

        // ==========================================
        // Enquiry Source Dropdown
        // ==========================================
        [HttpPost]
        public JsonResult GetEnquirySourceDropdown(int orgID = 0)
        {
            List<DropdownItem> list = new List<DropdownItem>();

            using (SqlConnection conn = new SqlConnection(CS))
            {
                string sql = @"SELECT EnquirySourceID,
                                      EnquirySourceType
                               FROM EnquirySourceMaster
                               ORDER BY EnquirySourceID DESC";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();

                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            list.Add(new DropdownItem
                            {
                                ID = Convert.ToInt32(dr["EnquirySourceID"]),
                                Name = dr["EnquirySourceType"].ToString()
                            });
                        }
                    }
                }
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetLocationDropdown()
        {
            List<dynamic> list = new List<dynamic>();
            string sql = "SELECT LocationID, AreaLocation, City, State, Pincode FROM LocationMaster WHERE IsActive = 1 ORDER BY AreaLocation;";

            using (SqlConnection conn = new SqlConnection(CS))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new
                            {
                                LocationID = Convert.ToInt32(reader["LocationID"]),
                                AreaLocation = Convert.ToString(reader["AreaLocation"]),
                                City = Convert.ToString(reader["City"]),
                                State = Convert.ToString(reader["State"]),
                                Pincode = Convert.ToString(reader["Pincode"])
                            });
                        }
                    }
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult SaveQuickFollowUp(StudentEnquiryModel model)
        {
            ProcessResult result = new ProcessResult();

            // 1. Decrypt incoming Enquiry ID from encrypted string
            int studentEnquiryID = 0;
            string encryptedString = model.EncryptedID ?? model.FollowUpObj?.EncryptedID ?? "";

            if (!string.IsNullOrEmpty(encryptedString))
            {
                string decryptedStr = Decrypt(encryptedString); // Uses your Decrypt helper
                if (decryptedStr.Contains("|"))
                {
                    decryptedStr = decryptedStr.Split('|')[0];
                }
                int.TryParse(decryptedStr, out studentEnquiryID);
            }

            if (studentEnquiryID <= 0 && model.StudentEnquiryID > 0)
            {
                studentEnquiryID = model.StudentEnquiryID;
            }

            if (studentEnquiryID <= 0)
            {
                result.IsSuccess = false;
                result.Message = "Invalid Student Enquiry Record.";
                return Json(result, JsonRequestBehavior.AllowGet);
            }

            // 2. Get FollowUpByUserID / CreatedByUserID from Request Cookie if not passed
            int actionUserID = model.CreatedByUserID;
            if (actionUserID <= 0)
            {
                HttpCookie userCookie = Request.Cookies["UserID"];
                if (userCookie != null && !string.IsNullOrEmpty(userCookie.Value))
                {
                    int.TryParse(userCookie.Value, out actionUserID);
                }
            }

            if (model.FollowUpObj != null && model.FollowUpObj.FollowUpByUserID <= 0)
            {
                model.FollowUpObj.FollowUpByUserID = actionUserID;
            }

            using (SqlConnection conn = new SqlConnection(CS))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Safe DateTime helper function
                    object ToDbDateTime(string dateStr)
                    {
                        if (DateTime.TryParse(dateStr, out DateTime dt))
                            return dt;
                        return DBNull.Value;
                    }

                    // 3. Insert into StudentEnquiryFollowUp table
                    string insertFollowUpSql = @"
                INSERT INTO StudentEnquiryFollowUp
                (
                    StudentEnquiryID, FollowUpDate, NextFollowUpDate, FollowUpType, Discussion,
                    NextActionRemark, FollowUpByUserID, CreatedDateTime, CreatedByUserID
                )
                VALUES
                (
                    @StudentEnquiryID, @FollowUpDate, @NextFollowUpDate, @FollowUpType, @Discussion,
                    @NextActionRemark, @FollowUpByUserID, GETDATE(), @CreatedByUserID
                );
                SELECT SCOPE_IDENTITY();";

                    int followUpID = 0;
                    using (SqlCommand cmdFollowUp = new SqlCommand(insertFollowUpSql, conn, transaction))
                    {
                        cmdFollowUp.Parameters.AddWithValue("@StudentEnquiryID", studentEnquiryID);
                        cmdFollowUp.Parameters.AddWithValue("@FollowUpDate", ToDbDateTime(model.FollowUpObj.FollowUpDate));
                        cmdFollowUp.Parameters.AddWithValue("@NextFollowUpDate", ToDbDateTime(model.FollowUpObj.NextFollowUpDate));
                        cmdFollowUp.Parameters.AddWithValue("@FollowUpType", model.FollowUpObj.FollowUpType ?? "Call");
                        cmdFollowUp.Parameters.AddWithValue("@Discussion", model.FollowUpObj.Discussion ?? (object)DBNull.Value);
                        cmdFollowUp.Parameters.AddWithValue("@NextActionRemark", model.FollowUpObj.NextActionRemark ?? (object)DBNull.Value);
                        cmdFollowUp.Parameters.AddWithValue("@FollowUpByUserID", model.FollowUpObj.FollowUpByUserID > 0 ? (object)model.FollowUpObj.FollowUpByUserID : DBNull.Value);
                        cmdFollowUp.Parameters.AddWithValue("@CreatedByUserID", actionUserID > 0 ? (object)actionUserID : DBNull.Value);

                        followUpID = Convert.ToInt32(cmdFollowUp.ExecuteScalar());
                    }

                    // 4. Update IsFollowUpRequired flag on Master
                    bool isClosed = model.IsClosed || (model.FollowUpObj != null && model.FollowUpObj.IsClosed);
                    string updateMasterSql = @"
                UPDATE StudentEnquiryMaster SET IsFollowUpRequired = @IsFollowUpRequired WHERE StudentEnquiryID = @StudentEnquiryID;";

                    using (SqlCommand cmdMaster = new SqlCommand(updateMasterSql, conn, transaction))
                    {
                        cmdMaster.Parameters.AddWithValue("@IsFollowUpRequired", isClosed ? 0 : 1);
                        cmdMaster.Parameters.AddWithValue("@StudentEnquiryID", studentEnquiryID);
                        cmdMaster.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    result.IsSuccess = true;
                    result.Message = "Follow up recorded successfully.";
                    result.PrimaryID = followUpID;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    result.IsSuccess = false;
                    result.Message = "Database Error: " + ex.Message;
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #endregion
        #region BranchBatchMaster
        [HttpGet]
        public ActionResult BranchBatchMaster()
        {
            return View();
        }
        [HttpGet]
        public ActionResult GetBranchBatchList(string SearchText, int PageNumber = 1, int PageSize = 10)
        {
            try
            {
                List<object> list = new List<object>();
                int totalRecords = 0;

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    // Count total records for pagination
                    string countQuery = @"
                SELECT COUNT(1) 
                FROM BranchBatchMaster bb
                LEFT JOIN BranchMaster bm ON bb.BranchID = bm.BranchID
                WHERE (@SearchText = '' OR bm.BranchName LIKE '%' + @SearchText + '%' OR bb.BatchTitle LIKE '%' + @SearchText + '%')";

                    using (SqlCommand cmdCount = new SqlCommand(countQuery, con))
                    {
                        cmdCount.Parameters.AddWithValue("@SearchText", SearchText ?? "");
                        totalRecords = Convert.ToInt32(cmdCount.ExecuteScalar());
                    }

                    // Fetch paginated records
                    string dataQuery = @"
                SELECT 
                    bb.BatchID,
                    bb.BatchTitle,
                    bb.BatchTimeStart,
                    bb.BatchTimeEnd,
                    bb.BatchTimeDuration,
                    bm.BranchName
                FROM BranchBatchMaster bb
                LEFT JOIN BranchMaster bm ON bb.BranchID = bm.BranchID
                WHERE (@SearchText = '' OR bm.BranchName LIKE '%' + @SearchText + '%' OR bb.BatchTitle LIKE '%' + @SearchText + '%')
                ORDER BY bb.BatchID DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                    using (SqlCommand cmd = new SqlCommand(dataQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@SearchText", SearchText ?? "");
                        cmd.Parameters.AddWithValue("@Offset", (PageNumber - 1) * PageSize);
                        cmd.Parameters.AddWithValue("@PageSize", PageSize);

                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                TimeSpan? startTime = dr["BatchTimeStart"] != DBNull.Value ? (TimeSpan?)dr["BatchTimeStart"] : null;
                                TimeSpan? endTime = dr["BatchTimeEnd"] != DBNull.Value ? (TimeSpan?)dr["BatchTimeEnd"] : null;

                                list.Add(new
                                {
                                    BatchID = Convert.ToInt32(dr["BatchID"]),
                                    BranchName = dr["BranchName"]?.ToString() ?? "",
                                    BatchTitle = dr["BatchTitle"]?.ToString() ?? "",
                                    BatchTimeStartStr = startTime.HasValue ? DateTime.Today.Add(startTime.Value).ToString("hh:mm tt") : "-",
                                    BatchTimeEndStr = endTime.HasValue ? DateTime.Today.Add(endTime.Value).ToString("hh:mm tt") : "-",
                                    BatchTimeDuration = dr["BatchTimeDuration"]?.ToString() ?? "-"
                                });
                            }
                        }
                    }
                }

                return Json(new { Data = list, TotalRecords = totalRecords }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Data = new List<object>(), TotalRecords = 0, Error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        // POST: Save or Update Branch Batch
        [HttpPost]
        public ActionResult SaveBranchBatch(BranchBatchViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    int currentUserId = 0;
                    if (Request.Cookies["UserID"] != null)
                    {
                        int.TryParse(Request.Cookies["UserID"].Value, out currentUserId);
                    }

                    using (SqlConnection con = new SqlConnection(CS))
                    {
                        con.Open();
                        SqlCommand cmd = new SqlCommand();
                        cmd.Connection = con;

                        if (model.BatchID == 0)
                        {
                            cmd.CommandText = @"
                                INSERT INTO BranchBatchMaster 
                                (BatchTitle, BatchTimeStart, BatchTimeEnd, BatchTimeDuration, BranchID, CreatedDateTime, CreatedByUserID)
                                VALUES 
                                (@BatchTitle, @BatchTimeStart, @BatchTimeEnd, @BatchTimeDuration, @BranchID, GETDATE(), @CreatedByUserID)";
                        }
                        else
                        {
                            cmd.CommandText = @"
                                UPDATE BranchBatchMaster 
                                SET BatchTitle = @BatchTitle,
                                    BatchTimeStart = @BatchTimeStart,
                                    BatchTimeEnd = @BatchTimeEnd,
                                    BatchTimeDuration = @BatchTimeDuration,
                                    BranchID = @BranchID
                                WHERE BatchID = @BatchID";

                            cmd.Parameters.AddWithValue("@BatchID", model.BatchID);
                        }

                        cmd.Parameters.AddWithValue("@BatchTitle", model.BatchTitle ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@BatchTimeStart", (object)model.BatchTimeStart ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@BatchTimeEnd", (object)model.BatchTimeEnd ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@BatchTimeDuration", model.BatchTimeDuration ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@BranchID", model.BranchID);
                        cmd.Parameters.AddWithValue("@CreatedByUserID", currentUserId);

                        int rows = cmd.ExecuteNonQuery();

                        if (rows > 0)
                        {
                            return Json(new { success = true, message = "Batch saved successfully!" });
                        }
                    }
                }

                return Json(new { success = false, message = "Please fill in all required fields." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
        // POST: /bctechadmin/DeleteBranchBatch
        [HttpPost]
        public ActionResult DeleteBranchBatch(int id)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(CS))
                {
                    string query = "DELETE FROM BranchBatchMaster WHERE BatchID = @BatchID";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@BatchID", id);
                        con.Open();
                        int rows = cmd.ExecuteNonQuery();

                        if (rows > 0)
                        {
                            return Json(new { success = true, message = "Branch Batch deleted successfully." });
                        }
                        else
                        {
                            return Json(new { success = false, message = "Record not found." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Delete failed: " + ex.Message });
            }
        }
        // GET: Dropdown List for Branches
        [HttpGet]
        public ActionResult GetBranchListDropdown()
        {
            List<object> branches = new List<object>();
            using (SqlConnection con = new SqlConnection(CS))
            {
                string query = "SELECT BranchID, BranchName FROM BranchMaster WHERE IsActive = 1 ORDER BY BranchName";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    con.Open();
                    SqlDataReader dr = cmd.ExecuteReader();
                    while (dr.Read())
                    {
                        branches.Add(new
                        {
                            BranchID = Convert.ToInt32(dr["BranchID"]),
                            BranchName = dr["BranchName"].ToString()
                        });
                    }
                }
            }
            return Json(branches, JsonRequestBehavior.AllowGet);
        }

        // GET: Fetch Batch by ID for Edit
        [HttpGet]
        public ActionResult GetBranchBatchByID(int id)
        {
            using (SqlConnection con = new SqlConnection(CS))
            {
                string query = "SELECT BatchID, BranchID, BatchTitle, BatchTimeStart, BatchTimeEnd, BatchTimeDuration FROM BranchBatchMaster WHERE BatchID = @BatchID";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@BatchID", id);
                    con.Open();
                    SqlDataReader dr = cmd.ExecuteReader();
                    if (dr.Read())
                    {
                        TimeSpan? startTime = dr["BatchTimeStart"] != DBNull.Value ? (TimeSpan?)dr["BatchTimeStart"] : null;
                        TimeSpan? endTime = dr["BatchTimeEnd"] != DBNull.Value ? (TimeSpan?)dr["BatchTimeEnd"] : null;

                        var data = new
                        {
                            BatchID = Convert.ToInt32(dr["BatchID"]),
                            BranchID = Convert.ToInt32(dr["BranchID"]),
                            BatchTitle = dr["BatchTitle"]?.ToString(),
                            BatchTimeStartStr = startTime.HasValue ? DateTime.Today.Add(startTime.Value).ToString("HH:mm") : "",
                            BatchTimeEndStr = endTime.HasValue ? DateTime.Today.Add(endTime.Value).ToString("HH:mm") : "",
                            BatchTimeDuration = dr["BatchTimeDuration"]?.ToString()
                        };
                        return Json(new { success = true, Data = data }, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            return Json(new { success = false, message = "Record not found" }, JsonRequestBehavior.AllowGet);
        }
        #endregion
        #region Student Master (Single Page / Modal Operations)

        // GET: /bctechadmin/StudentMaster
        [HttpGet]
        public ActionResult StudentList()
        {
            return View();
        }

        // GET: Fetch Paginated & Filtered Student List
        [HttpGet]
        public ActionResult GetStudentList(string SearchText = "", int PageNumber = 1, int PageSize = 10)
        {
            try
            {
                List<object> list = new List<object>();
                int totalRecords = 0;

                int branchId = 0;
                if (Request.Cookies["BranchID"] != null)
                {
                    int.TryParse(Request.Cookies["BranchID"].Value, out branchId);
                }
                if (branchId == 0 && Session["BranchID"] != null)
                {
                    int.TryParse(Session["BranchID"].ToString(), out branchId);
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    string branchCond = (branchId > 0) ? " AND sm.BranchID = @BranchID " : "";

                    // 1. Total Count Query
                    string countQuery = $@"
                SELECT COUNT(1) 
                FROM StudentMaster sm
                LEFT JOIN BranchMaster bm ON sm.BranchID = bm.BranchID
                LEFT JOIN BranchBatchMaster bbm ON sm.BatchID = bbm.BatchID
                WHERE ISNULL(sm.IsActive, 1) = 1 {branchCond}
                  AND (@SearchText = '' 
                       OR sm.StudentFullName LIKE '%' + @SearchText + '%' 
                       OR sm.StudentMobileNo LIKE '%' + @SearchText + '%' 
                       OR sm.StudentStringEnrollmentNo LIKE '%' + @SearchText + '%')";

                    using (SqlCommand cmdCount = new SqlCommand(countQuery, con))
                    {
                        if (branchId > 0) cmdCount.Parameters.AddWithValue("@BranchID", branchId);
                        cmdCount.Parameters.AddWithValue("@SearchText", SearchText ?? "");
                        totalRecords = Convert.ToInt32(cmdCount.ExecuteScalar());
                    }

                    // 2. Paginated Data Query
                    string dataQuery = $@"
                SELECT 
                    sm.StudentID,
                    sm.StudentEnrollmentNo,
                    sm.StudentStringEnrollmentNo,
                    sm.StudentFullName,
                    sm.StudentMobileNo,
                    sm.StudentEmail,
                    sm.AdminssionDateTime,
                    bm.BranchName,
                    bbm.BatchTitle
                FROM StudentMaster sm
                LEFT JOIN BranchMaster bm ON sm.BranchID = bm.BranchID
                LEFT JOIN BranchBatchMaster bbm ON sm.BatchID = bbm.BatchID
                WHERE ISNULL(sm.IsActive, 1) = 1 {branchCond}
                  AND (@SearchText = '' 
                       OR sm.StudentFullName LIKE '%' + @SearchText + '%' 
                       OR sm.StudentMobileNo LIKE '%' + @SearchText + '%' 
                       OR sm.StudentStringEnrollmentNo LIKE '%' + @SearchText + '%')
                ORDER BY sm.StudentID DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                    using (SqlCommand cmd = new SqlCommand(dataQuery, con))
                    {
                        if (branchId > 0) cmd.Parameters.AddWithValue("@BranchID", branchId);
                        cmd.Parameters.AddWithValue("@SearchText", SearchText ?? "");
                        cmd.Parameters.AddWithValue("@Offset", (PageNumber - 1) * PageSize);
                        cmd.Parameters.AddWithValue("@PageSize", PageSize);

                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                int studentId = Convert.ToInt32(dr["StudentID"]);
                                string encryptedId = Encrypt(studentId.ToString()); // Encrypt ID for Security

                                list.Add(new
                                {
                                    StudentID = studentId,
                                    EncryptedID = encryptedId,
                                    ViewEncryptedID = encryptedId,
                                    EditEncryptedID = encryptedId,
                                    DeleteEncryptedID = encryptedId,
                                    EnrollmentNo = !string.IsNullOrEmpty(dr["StudentStringEnrollmentNo"]?.ToString())
                                                    ? dr["StudentStringEnrollmentNo"].ToString()
                                                    : (dr["StudentEnrollmentNo"]?.ToString() ?? "-"),
                                    StudentFullName = dr["StudentFullName"]?.ToString() ?? "-",
                                    StudentMobileNo = dr["StudentMobileNo"]?.ToString() ?? "-",
                                    StudentEmail = dr["StudentEmail"]?.ToString() ?? "-",
                                    AdmissionDate = dr["AdminssionDateTime"] != DBNull.Value
                                                    ? Convert.ToDateTime(dr["AdminssionDateTime"]).ToString("dd/MM/yyyy")
                                                    : "-",
                                    BranchName = dr["BranchName"]?.ToString() ?? "-",
                                    BatchTitle = dr["BatchTitle"]?.ToString() ?? "-"
                                });
                            }
                        }
                    }
                }

                return Json(new { Data = list, TotalRecords = totalRecords }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Data = new List<object>(), TotalRecords = 0, Error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // 3. Delete Student Endpoint (Soft Delete)
        [HttpPost]
        public ActionResult DeleteStudent(string id)
        {
            try
            {
                string decryptedStr = Decrypt(id);
                if (!string.IsNullOrEmpty(decryptedStr) && decryptedStr.Contains("|"))
                {
                    decryptedStr = decryptedStr.Split('|')[0];
                }

                int studentId = 0;
                if (!int.TryParse(decryptedStr, out studentId))
                {
                    int.TryParse(id, out studentId);
                }

                if (studentId == 0)
                {
                    return Json(new { IsSuccess = false, Message = "Invalid record ID." });
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = "UPDATE StudentMaster SET IsActive = 0 WHERE StudentID = @StudentID";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@StudentID", studentId);
                        int rows = cmd.ExecuteNonQuery();

                        if (rows > 0)
                        {
                            return Json(new { IsSuccess = true, Message = "Student record deleted successfully!" });
                        }
                    }
                }

                return Json(new { IsSuccess = false, Message = "Failed to delete record." });
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = "Error: " + ex.Message });
            }
        }



        [HttpGet]
        public ActionResult GetMasterCourseStudentList(string CourseID, string StudentID = "")
        {
            try
            {
                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    int studentId = 0;
                    if (!string.IsNullOrEmpty(StudentID))
                    {
                        if (!int.TryParse(StudentID, out studentId))
                        {
                            try
                            {
                                string decryptedIdStr = Decrypt(StudentID);
                                if (!string.IsNullOrEmpty(decryptedIdStr) && decryptedIdStr.Contains("|"))
                                {
                                    decryptedIdStr = decryptedIdStr.Split('|')[0];
                                }
                                int.TryParse(decryptedIdStr, out studentId);
                            }
                            catch { }
                        }
                    }

                    int courseId = 0;
                    int.TryParse(CourseID, out courseId);

                    // 1. Agar StudentID ho to pehle StudentMasterCourse se data fetch kare
                    if (studentId > 0 && courseId > 0)
                    {
                        string studentCourseQuery = @"
                            SELECT SMC.SelectCourseID, CM.CourseTitle, SMC.CourseFee
                            FROM StudentMasterCourse SMC
                            LEFT JOIN CourseMaster CM ON CM.CourseID = SMC.SelectCourseID
                            WHERE SMC.StudentID = @StudentID AND SMC.CourseID = @CourseID
                            ORDER BY SMC.StudentMasterCourseID ASC";

                        using (SqlCommand cmdStudent = new SqlCommand(studentCourseQuery, con))
                        {
                            cmdStudent.Parameters.AddWithValue("@StudentID", studentId);
                            cmdStudent.Parameters.AddWithValue("@CourseID", courseId);

                            using (SqlDataReader drStudent = cmdStudent.ExecuteReader())
                            {
                                while (drStudent.Read())
                                {
                                    list.Add(new
                                    {
                                        CourseID = Convert.ToInt32(drStudent["SelectCourseID"]),
                                        CourseTitle = drStudent["CourseTitle"]?.ToString() ?? "",
                                        CourseFee = drStudent["CourseFee"]?.ToString() ?? ""
                                    });
                                }
                            }
                        }
                    }

                    // 2. Agar StudentMasterCourse me data na ho (new course add kiya ho ya new admission ho), to MasterCourse se fetch kare
                    if (list.Count == 0 && courseId > 0)
                    {
                        string dataQuery = @"
                            SELECT MC.SelectCourseID, CM.CourseTitle, MC.CourseFee
                            FROM MasterCourse MC
                            LEFT JOIN CourseMaster CM ON CM.CourseID = MC.SelectCourseID
                            WHERE MC.courseID = @courseID";

                        using (SqlCommand cmd = new SqlCommand(dataQuery, con))
                        {
                            cmd.Parameters.AddWithValue("@courseID", courseId);

                            using (SqlDataReader dr = cmd.ExecuteReader())
                            {
                                while (dr.Read())
                                {
                                    list.Add(new
                                    {
                                        CourseID = Convert.ToInt32(dr["SelectCourseID"]),
                                        CourseTitle = dr["CourseTitle"]?.ToString() ?? "",
                                        CourseFee = dr["CourseFee"]?.ToString() ?? ""
                                    });
                                }
                            }
                        }
                    }
                }

                return Json(new { Data = list }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Data = new List<object>(), TotalRecords = 0, Error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }




        [HttpGet]
        public ActionResult CreateStudentAdmission(string id = "", string mode = "")
        {
            ViewBag.StudentID = id;
            ViewBag.Mode = mode; // Stores 'V' for View Mode
            return View();
        }


        // POST: Fetch active pending student enquiries for dropdown
        // POST: Fetch active pending student enquiries for dropdown
        [HttpPost]
        public ActionResult GetEnquiryDropdown()
        {
            try
            {
                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(CS))
                {
                    string query = @"
                                       SELECT 
                            se.StudentEnquiryID, 
                            se.EnquiryNo,
                            se.StudentName,
                            se.StudentMobileNo,
                            ISNULL(es.EnquirySourceType, '') AS SourceName
                        FROM StudentEnquiryMaster se
                        LEFT JOIN EnquirySourceMaster es 
                            ON se.EnquirySourceID = es.EnquirySourceID
                        WHERE ISNULL(se.IsConvertedToAdmission, 0) = 0
                            AND ISNULL(se.IsFollowUpRequired, 1) = 1
                        ORDER BY se.StudentEnquiryID DESC;";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        con.Open();
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                int enquiryId = Convert.ToInt32(dr["StudentEnquiryID"]);

                                list.Add(new
                                {
                                    StudentEnquiryID = enquiryId,
                                    EncryptedID = enquiryId.ToString(), // Pass plain or encrypted ID
                                    StudentName = dr["StudentName"]?.ToString() ?? "",
                                    CleanStudentName = dr["StudentName"]?.ToString() ?? "",
                                    EnquiryNo = dr["EnquiryNo"]?.ToString() ?? "",
                                    StudentMobileNo = dr["StudentMobileNo"]?.ToString() ?? "",
                                    SourceName = dr["SourceName"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);
            }
        }
        // GET: Fetch Single Student Record by ID (for View / Edit Modal)
        [HttpGet]
        public ActionResult GetStudentByID(string id)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    string decryptedIdStr = Decrypt(id);
                    if (!string.IsNullOrEmpty(decryptedIdStr) && decryptedIdStr.Contains("|"))
                    {
                        decryptedIdStr = decryptedIdStr.Split('|')[0];
                    }

                    int studentId = 0;
                    if (!int.TryParse(decryptedIdStr, out studentId))
                    {
                        int.TryParse(id, out studentId);
                    }

                    if (studentId == 0)
                    {
                        return Json(new { IsSuccess = false, Message = "Invalid Student ID." }, JsonRequestBehavior.AllowGet);
                    }

                    // 2. Fetch Master Record
                    string query = @"
                SELECT sm.*, bm.BranchName, bbm.BatchTitle
                FROM StudentMaster sm
                LEFT JOIN BranchMaster bm ON sm.BranchID = bm.BranchID
                LEFT JOIN BranchBatchMaster bbm ON sm.BatchID = bbm.BatchID
                WHERE sm.StudentID = @StudentID";

                    Dictionary<string, object> studentData = null;

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@StudentID", studentId);

                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                studentData = new Dictionary<string, object>
                        {
                            { "StudentID", Convert.ToInt32(dr["StudentID"]) },
                            { "StudentEnquiryID", dr["StudentEnquiryID"] != DBNull.Value ? Convert.ToInt32(dr["StudentEnquiryID"]) : (int?)null },
                            { "OrganizationID", dr["OrganizationID"] != DBNull.Value ? Convert.ToInt32(dr["OrganizationID"]) : 0 },
                            { "BranchID", dr["BranchID"] != DBNull.Value ? Convert.ToInt32(dr["BranchID"]) : 0 },
                            { "BatchID", dr["BatchID"] != DBNull.Value ? Convert.ToInt32(dr["BatchID"]) : (int?)null },
                            { "FinancialYearID", dr["FinancialYearID"] != DBNull.Value ? Convert.ToInt32(dr["FinancialYearID"]) : 0 },
                            { "AdmissionDateTime", dr["AdminssionDateTime"] != DBNull.Value ? Convert.ToDateTime(dr["AdminssionDateTime"]).ToString("yyyy-MM-dd") : "" },
                            { "StudentEnrollmentNo", dr["StudentEnrollmentNo"]?.ToString() ?? "" },
                            { "StudentStringEnrollmentNo", dr["StudentStringEnrollmentNo"]?.ToString() ?? "" },
                            { "StudentFullName", dr["StudentFullName"]?.ToString() ?? "" },
                            { "StudentMobileNo", dr["StudentMobileNo"]?.ToString() ?? "" },
                            { "WhatsAppNo", dr["WhatsAppNo"]?.ToString() ?? "" },
                            { "StudentEmail", dr["StudentEmail"]?.ToString() ?? "" },
                            { "StudentPhoto", dr["StudentPhoto"]?.ToString() ?? "" },
                           {
                                "StudentAadhar",
                                HasColumn(dr, "StudentAadhar") && !dr.IsDBNull(dr.GetOrdinal("StudentAadhar"))
                                    ? dr["StudentAadhar"].ToString()
                                    : ""
                            },
                            { "DOB", dr["DOB"] != DBNull.Value ? Convert.ToDateTime(dr["DOB"]).ToString("yyyy-MM-dd") : "" },
                            { "Gender", dr["Gender"]?.ToString() ?? "" },
                            { "BloodGroup", dr["BloodGroup"]?.ToString() ?? "" },
                            { "EducationQualification", dr["EducationQualification"]?.ToString() ?? "" },
                            { "SchoolCollegeName", dr["SchoolCollegeName"]?.ToString() ?? "" },
                            { "FatherName", dr["FatherName"]?.ToString() ?? "" },
                            { "FatherMobileNo", dr["FatherMobileNo"]?.ToString() ?? "" },
                            { "FatherAlternateMobileNo", dr["FatherAlternateMobileNo"]?.ToString() ?? "" },
                            { "FatherOccupation", dr["FatherOccupation"]?.ToString() ?? "" },
                            { "FatherEmail", dr["FatherEmail"]?.ToString() ?? "" },
                            { "MotherName", dr["MotherName"]?.ToString() ?? "" },
                            { "MotherMobileNo", dr["MotherMobileNo"]?.ToString() ?? "" },
                            { "MotherOccupation", dr["MotherOccupation"]?.ToString() ?? "" },
                            { "MotherEmail", dr["MotherEmail"]?.ToString() ?? "" },
                            { "Address", dr["Address"]?.ToString() ?? "" },
                            { "AreaLocation", dr["AreaLocation"]?.ToString() ?? "" },
                            { "City", dr["City"]?.ToString() ?? "" },
                            { "State", dr["State"]?.ToString() ?? "" },
                            { "Pincode", dr["Pincode"]?.ToString() ?? "" }
                        };
                            }
                        }
                    }

                    if (studentData == null)
                    {
                        return Json(new { IsSuccess = false, Message = "Student record not found." }, JsonRequestBehavior.AllowGet);
                    }

                    // 3. Fetch Registered Courses from StudentRegistrationCourse
                    List<object> courseList = new List<object>();
                    string courseQuery = @"
                SELECT 
                    StudentCourseRegistrationID,
                    CourseID, 
                    ISNULL(CourseFee, 0) AS CourseFee, 
                    ISNULL(Discount, 0) AS Discount, 
                    ISNULL(FinalFee, 0) AS FinalFee, 
                    ISNULL(Remarks, '') AS Remarks 
                FROM StudentRegistrationCourse 
                WHERE StudentID = @StudentID";

                    using (SqlCommand cmdCourse = new SqlCommand(courseQuery, con))
                    {
                        cmdCourse.Parameters.AddWithValue("@StudentID", studentId);

                        using (SqlDataReader drCourse = cmdCourse.ExecuteReader())
                        {
                            while (drCourse.Read())
                            {
                                courseList.Add(new
                                {
                                    StudentCourseRegistrationID = Convert.ToInt32(drCourse["StudentCourseRegistrationID"]),
                                    CourseID = Convert.ToInt32(drCourse["CourseID"]),
                                    CourseFee = Convert.ToDecimal(drCourse["CourseFee"]),
                                    Discount = Convert.ToDecimal(drCourse["Discount"]),
                                    FinalFee = Convert.ToDecimal(drCourse["FinalFee"]),
                                    Remarks = drCourse["Remarks"]?.ToString() ?? ""
                                });
                            }
                        }
                    }

                    studentData["CourseList"] = courseList;

                    // 4. Fetch Installments from FeesInstallmentMaster
                    List<object> installmentList = new List<object>();
                    string instQuery = @"
                        SELECT 
                            fi.FeesInstallmentID,
                            fi.StudentID,
                            CONVERT(VARCHAR(10), fi.FeesDate, 120) AS DueDate,
                            fi.FeesTypeID,
                            ISNULL(ft.FeesTypeTitle, '') AS FeesTitle,
                            ISNULL(fi.FeesAmount, 0) AS FeesAmount
                        FROM FeesInstallmentMaster fi
                        LEFT JOIN FeesTypeMaster ft ON fi.FeesTypeID = ft.FeesTypeID
                        WHERE fi.StudentID = @StudentID
                        ORDER BY fi.FeesInstallmentID ASC";

                    using (SqlCommand cmdInst = new SqlCommand(instQuery, con))
                    {
                        cmdInst.Parameters.AddWithValue("@StudentID", studentId);
                        using (SqlDataReader drInst = cmdInst.ExecuteReader())
                        {
                            while (drInst.Read())
                            {
                                installmentList.Add(new
                                {
                                    FeesInstallmentID = Convert.ToInt32(drInst["FeesInstallmentID"]),
                                    StudentID = Convert.ToInt32(drInst["StudentID"]),
                                    DueDate = drInst["DueDate"]?.ToString() ?? "",
                                    FeesTypeID = drInst["FeesTypeID"] != DBNull.Value ? Convert.ToInt32(drInst["FeesTypeID"]) : (int?)null,
                                    FeesTitle = drInst["FeesTitle"]?.ToString() ?? "",
                                    Amount = Convert.ToDecimal(drInst["FeesAmount"]),
                                    FeesAmount = Convert.ToDecimal(drInst["FeesAmount"])
                                });
                            }
                        }
                    }

                    studentData["InstallmentList"] = installmentList;

                    // 5. Fetch Documents from StudentDocuments
                    List<object> documentList = new List<object>();
                    string docQuery = @"
                        SELECT 
                            sd.StudentDocumentDetailID,
                            sd.StudentID,
                            sd.StudentDocumentID,
                            ISNULL(sdm.StudentDocumentTitle, '') AS StudentDocumentTitle,
                            ISNULL(sd.DocumentPath, '') AS DocumentPath,
                            ISNULL(sd.DocumentName, '') AS DocumentName
                        FROM StudentDocuments sd
                        LEFT JOIN StudentDocumentMaster sdm ON sd.StudentDocumentID = sdm.StudentDocumentID
                        WHERE sd.StudentID = @StudentID
                        ORDER BY sd.StudentDocumentDetailID ASC";

                    using (SqlCommand cmdDoc = new SqlCommand(docQuery, con))
                    {
                        cmdDoc.Parameters.AddWithValue("@StudentID", studentId);
                        using (SqlDataReader drDoc = cmdDoc.ExecuteReader())
                        {
                            while (drDoc.Read())
                            {
                                documentList.Add(new
                                {
                                    StudentDocumentDetailID = Convert.ToInt32(drDoc["StudentDocumentDetailID"]),
                                    StudentID = Convert.ToInt32(drDoc["StudentID"]),
                                    StudentDocumentID = Convert.ToInt32(drDoc["StudentDocumentID"]),
                                    StudentDocumentTitle = drDoc["StudentDocumentTitle"]?.ToString() ?? "",
                                    DocumentPath = drDoc["DocumentPath"]?.ToString() ?? "",
                                    DocumentName = drDoc["DocumentName"]?.ToString() ?? ""
                                });
                            }
                        }
                    }

                    studentData["DocumentList"] = documentList;

                    // 6. Fetch Payment Mode & Transaction No from StudentFeeReceipt (if any)
                    int? receiptPaymentTypeId = 1;
                    string receiptTransactionNo = "";
                    string payQuery = @"SELECT TOP 1 PaymentTypeID, ISNULL(TransactionNo, '') AS TransactionNo FROM StudentFeeReceipt WHERE StudentID = @StudentID ORDER BY StudentFeeReceiptID ASC";
                    using (SqlCommand cmdPay = new SqlCommand(payQuery, con))
                    {
                        cmdPay.Parameters.AddWithValue("@StudentID", studentId);
                        using (SqlDataReader drPay = cmdPay.ExecuteReader())
                        {
                            if (drPay.Read())
                            {
                                if (drPay["PaymentTypeID"] != DBNull.Value)
                                {
                                    receiptPaymentTypeId = Convert.ToInt32(drPay["PaymentTypeID"]);
                                }
                                receiptTransactionNo = drPay["TransactionNo"]?.ToString() ?? "";
                            }
                        }
                    }
                    studentData["PaymentTypeID"] = receiptPaymentTypeId;
                    studentData["TransactionNo"] = receiptTransactionNo;

                    return Json(new { IsSuccess = true, Data = studentData }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public ActionResult GetEncryptStudentByID(string id)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    string decryptedIdStr = Decrypt(id);
                    if (!string.IsNullOrEmpty(decryptedIdStr) && decryptedIdStr.Contains("|"))
                    {
                        decryptedIdStr = decryptedIdStr.Split('|')[0];
                    }

                    int studentId = 0;
                    if (!int.TryParse(decryptedIdStr, out studentId))
                    {
                        int.TryParse(id, out studentId);
                    }

                    if (studentId == 0)
                    {
                        return Json(new { IsSuccess = false, Message = "Invalid Student ID." }, JsonRequestBehavior.AllowGet);
                    }
                    else
                    {
                        return Json(new { IsSuccess = true, Message = studentId }, JsonRequestBehavior.AllowGet);
                    }


                    //return Json(new { IsSuccess = true, Data = studentData }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult UploadStudentPhoto()
        {
            try
            {
                if (Request.Files.Count > 0)
                {
                    HttpPostedFileBase file = Request.Files[0];
                    if (file != null && file.ContentLength > 0)
                    {
                        string folderPath = Server.MapPath("~/Content/profile");
                        if (!System.IO.Directory.Exists(folderPath))
                        {
                            System.IO.Directory.CreateDirectory(folderPath);
                        }

                        string extension = System.IO.Path.GetExtension(file.FileName);
                        string fileName = "student_photo_" + DateTime.Now.Ticks + extension;
                        string fullPath = System.IO.Path.Combine(folderPath, fileName);

                        file.SaveAs(fullPath);

                        string relativePath = "/Content/profile/" + fileName;
                        return Json(new { IsSuccess = true, FilePath = relativePath, Message = "Photo uploaded successfully." });
                    }
                }
                return Json(new { IsSuccess = false, Message = "No file selected." });
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = "Upload error: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult UploadStudentDocumentFile()
        {
            try
            {
                if (Request.Files.Count > 0)
                {
                    HttpPostedFileBase file = Request.Files[0];
                    if (file != null && file.ContentLength > 0)
                    {
                        string extension = System.IO.Path.GetExtension(file.FileName).ToLower();
                        string[] allowedExtensions = new string[] { ".pdf", ".jpg", ".jpeg", ".png" };
                        if (!allowedExtensions.Contains(extension))
                        {
                            return Json(new { IsSuccess = false, Message = "Only PDF, JPG, and PNG files are allowed." });
                        }

                        if (file.ContentLength > 10 * 1024 * 1024)
                        {
                            return Json(new { IsSuccess = false, Message = "File size cannot exceed 10MB." });
                        }

                        string folderPath = Server.MapPath("~/Content/Upload/StudentDocuments");
                        if (!System.IO.Directory.Exists(folderPath))
                        {
                            System.IO.Directory.CreateDirectory(folderPath);
                        }

                        string originalName = System.IO.Path.GetFileName(file.FileName);
                        string fileName = "doc_" + Guid.NewGuid().ToString("N").Substring(0, 8) + "_" + DateTime.Now.Ticks + extension;
                        string fullPath = System.IO.Path.Combine(folderPath, fileName);

                        file.SaveAs(fullPath);

                        string relativePath = "/Content/Upload/StudentDocuments/" + fileName;
                        return Json(new { IsSuccess = true, FilePath = relativePath, FileName = originalName, FileExtension = extension, Message = "Document uploaded successfully." });
                    }
                }
                return Json(new { IsSuccess = false, Message = "No file selected." });
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = "Upload error: " + ex.Message });
            }
        }

        private static bool HasColumn(SqlDataReader dr, string columnName)
        {
            for (int i = 0; i < dr.FieldCount; i++)
            {
                if (dr.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        [HttpPost]
        public ActionResult SaveStudent(StudentMasterViewModel model)
        {
            return ExecuteSaveOrUpdate(model, isUpdate: false);
        }
        [HttpPost]
        public ActionResult UpdateStudent(StudentMasterViewModel model)
        {
            return ExecuteSaveOrUpdate(model, isUpdate: true);
        }
        private ActionResult ExecuteSaveOrUpdate(StudentMasterViewModel model, bool isUpdate)
        {
            try
            {
                int currentUserId = 0;
                if (Request.Cookies["UserID"] != null)
                {
                    int.TryParse(Request.Cookies["UserID"].Value, out currentUserId);
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    // Ensure StudentPhoto and StudentAadhar columns exist in StudentMaster
                    using (SqlCommand cmdInit = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StudentMaster') AND name = 'StudentPhoto')
                        BEGIN
                            ALTER TABLE StudentMaster ADD StudentPhoto NVARCHAR(MAX) NULL;
                        END;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StudentMaster') AND name = 'StudentAadhar')
                        BEGIN
                            ALTER TABLE StudentMaster ADD StudentAadhar VARCHAR(100) NULL;
                        END;", con))
                    {
                        cmdInit.ExecuteNonQuery();
                    }

                    // Transaction ensures StudentMaster & StudentRegistrationCourse save together
                    using (SqlTransaction transaction = con.BeginTransaction())
                    {
                        try
                        {
                            SqlCommand cmd = new SqlCommand();
                            cmd.Connection = con;
                            cmd.Transaction = transaction;

                            int studentId = model.StudentID;

                            if (!isUpdate || studentId == 0)
                            {
                                // 1. INSERT STUDENT MASTER
                                cmd.CommandText = @"
                            INSERT INTO StudentMaster 
                            (
                                StudentEnquiryID, OrganizationID, BranchID, BatchID, AdmissionTakenByUserID, FinancialYearID, 
                                AdminssionDateTime, StudentEnrollmentNo, StudentStringEnrollmentNo, StudentFullName, StudentMobileNo, 
                                WhatsAppNo, StudentEmail, StudentPhoto, StudentAadhar, DOB, Gender, BloodGroup, EducationQualification, 
                                SchoolCollegeName, FatherName, FatherMobileNo, FatherAlternateMobileNo, FatherOccupation, FatherEmail, 
                                MotherName, MotherMobileNo, MotherOccupation, MotherEmail, Address, AreaLocation, City, State, 
                                Pincode, ProofOfIdentityType, ProofOfIdentity, ProofOfIdentityNumber, Remarks, AdmissionRemark, 
                                IsGeneratediCard, IsOldStudent, IsActive, CreatedDateTime, CreatedByUserID
                            )
                            VALUES 
                            (
                                @StudentEnquiryID, @OrganizationID, @BranchID, @BatchID, @AdmissionTakenByUserID, @FinancialYearID, 
                                GETDATE(), @StudentEnrollmentNo, @StudentStringEnrollmentNo, @StudentFullName, @StudentMobileNo, 
                                @WhatsAppNo, @StudentEmail, @StudentPhoto, @StudentAadhar, @DOB, @Gender, @BloodGroup, @EducationQualification, 
                                @SchoolCollegeName, @FatherName, @FatherMobileNo, @FatherAlternateMobileNo, @FatherOccupation, @FatherEmail, 
                                @MotherName, @MotherMobileNo, @MotherOccupation, @MotherEmail, @Address, @AreaLocation, @City, @State, 
                                @Pincode, @ProofOfIdentityType, @ProofOfIdentity, @ProofOfIdentityNumber, @Remarks, @AdmissionRemark, 
                                @IsGeneratediCard, @IsOldStudent, @IsActive, GETDATE(), @CreatedByUserID
                            );
                            SELECT SCOPE_IDENTITY();";
                            }
                            else
                            {
                                // 1. UPDATE STUDENT MASTER
                                cmd.CommandText = @"
                            UPDATE StudentMaster 
                            SET 
                                BranchID = @BranchID,
                                BatchID = @BatchID,
                                StudentFullName = @StudentFullName,
                                StudentMobileNo = @StudentMobileNo,
                                WhatsAppNo = @WhatsAppNo,
                                StudentEmail = @StudentEmail,
                                StudentPhoto = ISNULL(@StudentPhoto, StudentPhoto),
                                StudentAadhar = @StudentAadhar,
                                DOB = @DOB,
                                Gender = @Gender,
                                BloodGroup = @BloodGroup,
                                EducationQualification = @EducationQualification,
                                SchoolCollegeName = @SchoolCollegeName,
                                FatherName = @FatherName,
                                FatherMobileNo = @FatherMobileNo,
                                FatherAlternateMobileNo = @FatherAlternateMobileNo,
                                FatherOccupation = @FatherOccupation,
                                FatherEmail = @FatherEmail,
                                MotherName = @MotherName,
                                MotherMobileNo = @MotherMobileNo,
                                MotherOccupation = @MotherOccupation,
                                MotherEmail = @MotherEmail,
                                Address = @Address,
                                AreaLocation = @AreaLocation,
                                City = @City,
                                State = @State,
                                Pincode = @Pincode,
                                Remarks = @Remarks,
                                AdmissionRemark = @AdmissionRemark,
                                IsActive = @IsActive
                            WHERE StudentID = @StudentID";

                                cmd.Parameters.AddWithValue("@StudentID", studentId);
                            }

                            // Parameters Assignment
                            cmd.Parameters.AddWithValue("@StudentEnquiryID", model.StudentEnquiryID.HasValue ? (object)model.StudentEnquiryID.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@OrganizationID", model.OrganizationID > 0 ? model.OrganizationID : 1);
                            cmd.Parameters.AddWithValue("@BranchID", model.BranchID);
                            cmd.Parameters.AddWithValue("@BatchID", model.BatchID.HasValue ? (object)model.BatchID.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@AdmissionTakenByUserID", currentUserId);
                            cmd.Parameters.AddWithValue("@FinancialYearID", model.FinancialYearID > 0 ? model.FinancialYearID : 1);

                            // Enrollment & Identity Properties
                            cmd.Parameters.AddWithValue("@StudentEnrollmentNo", model.StudentEnrollmentNo > 0 ? model.StudentEnrollmentNo : DateTime.Now.Ticks);
                            cmd.Parameters.AddWithValue("@StudentStringEnrollmentNo", !string.IsNullOrEmpty(model.StudentStringEnrollmentNo) ? model.StudentStringEnrollmentNo : "AUTO-" + DateTime.Now.Ticks);
                            cmd.Parameters.AddWithValue("@StudentFullName", model.StudentFullName ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@StudentMobileNo", model.StudentMobileNo ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@WhatsAppNo", model.WhatsAppNo ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@StudentEmail", model.StudentEmail ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@StudentPhoto", model.StudentPhoto ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@StudentAadhar", model.StudentAadhar ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@DOB", model.DOB.HasValue ? (object)model.DOB.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@Gender", model.Gender ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@BloodGroup", model.BloodGroup ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@EducationQualification", model.EducationQualification ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@SchoolCollegeName", model.SchoolCollegeName ?? (object)DBNull.Value);

                            // Parent Details
                            cmd.Parameters.AddWithValue("@FatherName", model.FatherName ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@FatherMobileNo", model.FatherMobileNo ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@FatherAlternateMobileNo", model.FatherAlternateMobileNo ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@FatherOccupation", model.FatherOccupation ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@FatherEmail", model.FatherEmail ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@MotherName", model.MotherName ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@MotherMobileNo", model.MotherMobileNo ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@MotherOccupation", model.MotherOccupation ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@MotherEmail", model.MotherEmail ?? (object)DBNull.Value);

                            // Address Details
                            cmd.Parameters.AddWithValue("@Address", model.Address ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@AreaLocation", model.AreaLocation ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@City", model.City ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@State", model.State ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Pincode", model.Pincode ?? (object)DBNull.Value);

                            // Proof & System Status
                            cmd.Parameters.AddWithValue("@ProofOfIdentityType", model.ProofOfIdentityType ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@ProofOfIdentity", model.ProofOfIdentity ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@ProofOfIdentityNumber", model.ProofOfIdentityNumber ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Remarks", model.Remarks ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@AdmissionRemark", model.AdmissionRemark ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@IsGeneratediCard", model.IsGeneratediCard);
                            cmd.Parameters.AddWithValue("@IsOldStudent", model.IsOldStudent);
                            cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                            cmd.Parameters.AddWithValue("@CreatedByUserID", currentUserId);

                            if (!isUpdate || studentId == 0)
                            {
                                object newId = cmd.ExecuteScalar();
                                studentId = Convert.ToInt32(newId);
                            }
                            else
                            {
                                cmd.ExecuteNonQuery();
                            }

                            // 2. SAVE CHILD COURSE REGISTRATION (StudentRegistrationCourse)
                            if (studentId > 0 && model.CourseList != null && model.CourseList.Count > 0)
                            {
                                if (isUpdate)
                                {
                                    // 1. Fetch existing course registration IDs for this student ordered by StudentCourseRegistrationID ASC
                                    List<int> existingCourseIDs = new List<int>();
                                    using (SqlCommand cmdGetExisting = new SqlCommand("SELECT StudentCourseRegistrationID FROM StudentRegistrationCourse WHERE StudentID = @StudentID ORDER BY StudentCourseRegistrationID ASC", con, transaction))
                                    {
                                        cmdGetExisting.Parameters.AddWithValue("@StudentID", studentId);
                                        using (SqlDataReader drCourse = cmdGetExisting.ExecuteReader())
                                        {
                                            while (drCourse.Read())
                                            {
                                                existingCourseIDs.Add(Convert.ToInt32(drCourse["StudentCourseRegistrationID"]));
                                            }
                                        }
                                    }

                                    int existingCount = existingCourseIDs.Count;
                                    int incomingCount = model.CourseList.Count;

                                    // 2. Update matching rows
                                    int updateCount = Math.Min(existingCount, incomingCount);
                                    for (int i = 0; i < updateCount; i++)
                                    {
                                        var course = model.CourseList[i];
                                        int courseRegIdToUpdate = existingCourseIDs[i];

                                        using (SqlCommand cmdUpdate = new SqlCommand(@"
                                            UPDATE StudentRegistrationCourse
                                            SET CourseID = @CourseID,
                                                BatchID = @BatchID,
                                                CourseFee = @CourseFee,
                                                Discount = @Discount,
                                                FinalFee = @FinalFee,
                                                Remarks = @Remarks
                                            WHERE StudentCourseRegistrationID = @StudentCourseRegistrationID AND StudentID = @StudentID", con, transaction))
                                        {
                                            cmdUpdate.Parameters.AddWithValue("@CourseID", course.CourseID);
                                            cmdUpdate.Parameters.AddWithValue("@BatchID", model.BatchID.HasValue ? model.BatchID.Value : 0);
                                            cmdUpdate.Parameters.AddWithValue("@CourseFee", course.CourseFee);
                                            cmdUpdate.Parameters.AddWithValue("@Discount", course.Discount);
                                            cmdUpdate.Parameters.AddWithValue("@FinalFee", course.FinalFee);
                                            cmdUpdate.Parameters.AddWithValue("@Remarks", course.Remarks ?? (object)DBNull.Value);
                                            cmdUpdate.Parameters.AddWithValue("@StudentCourseRegistrationID", courseRegIdToUpdate);
                                            cmdUpdate.Parameters.AddWithValue("@StudentID", studentId);

                                            cmdUpdate.ExecuteNonQuery();
                                        }



                                        using (SqlCommand cmdGetExisting = new SqlCommand("select * from StudentMasterCourse where CourseID = @CourseID and StudentID = @StudentID", con, transaction))
                                        {
                                            cmdGetExisting.Parameters.AddWithValue("@CourseID", course.CourseID);
                                            cmdGetExisting.Parameters.AddWithValue("@StudentID", studentId);

                                            using (SqlDataReader drCourse = cmdGetExisting.ExecuteReader())
                                            {

                                                if (!drCourse.HasRows)
                                                {
                                                    using (SqlCommand cmdGetExistings = new SqlCommand("select * from MasterCourse where CourseID = @CourseID", con, transaction))
                                                    {
                                                        cmdGetExistings.Parameters.AddWithValue("@CourseID", course.CourseID);
                                                        using (SqlDataReader drCourses = cmdGetExistings.ExecuteReader())
                                                        {

                                                            while (drCourses.Read())
                                                            {
                                                                //existingCourseIDs.Add(Convert.ToInt32(drCourse["StudentCourseRegistrationID"]));
                                                                using (SqlCommand cmdCourse = new SqlCommand(@"
                                                                    INSERT INTO StudentMasterCourse 
                                                                    (StudentID, SelectCourseID, CourseID, CreatedDateTime, CreatedByUserID, CourseFee)
                                                                    VALUES 
                                                                    (@StudentID, @SelectCourseID, @CourseID, GETDATE(), @CreatedByUserID, @CourseFee)", con, transaction))
                                                                {
                                                                    cmdCourse.Parameters.AddWithValue("@StudentID", studentId);
                                                                    cmdCourse.Parameters.AddWithValue("@SelectCourseID", drCourses["SelectCourseID"]);
                                                                    cmdCourse.Parameters.AddWithValue("@CourseID", course.CourseID);
                                                                    cmdCourse.Parameters.AddWithValue("@CreatedByUserID", currentUserId);
                                                                    cmdCourse.Parameters.AddWithValue("@CourseFee", drCourses["CourseFee"]);
                                                                    cmdCourse.ExecuteNonQuery();
                                                                }
                                                            }


                                                        }
                                                    }


                                                }


                                                //while (drCourse.Read())
                                                //{





                                                //}
                                            }
                                        }

                                    }


                                    using (SqlCommand cmdDel = new SqlCommand("delete from StudentMasterCourse where StudentID = @StudentID and   CourseID Not IN  ( select CourseID from StudentRegistrationCourse where studentID = @StudentID)", con, transaction))
                                    {
                                        cmdDel.Parameters.AddWithValue("@StudentID", studentId);
                                        cmdDel.ExecuteNonQuery();
                                    }



                                    // 3. If incoming rows > existing rows: Insert extra new rows
                                    if (incomingCount > existingCount)
                                    {
                                        for (int i = existingCount; i < incomingCount; i++)
                                        {
                                            var course = model.CourseList[i];
                                            using (SqlCommand cmdCourse = new SqlCommand(@"
                                                INSERT INTO StudentRegistrationCourse 
                                                (StudentID, CourseID, BatchID, AdmissionDate, CourseFee, Discount, FinalFee, Remarks, CourseStatus, CreatedDateTime, CreatedByUserID)
                                                VALUES 
                                                (@StudentID, @CourseID, @BatchID, GETDATE(), @CourseFee, @Discount, @FinalFee, @Remarks, 'Active', GETDATE(), @CreatedByUserID)", con, transaction))
                                            {
                                                cmdCourse.Parameters.AddWithValue("@StudentID", studentId);
                                                cmdCourse.Parameters.AddWithValue("@CourseID", course.CourseID);
                                                cmdCourse.Parameters.AddWithValue("@BatchID", model.BatchID.HasValue ? model.BatchID.Value : 0);
                                                cmdCourse.Parameters.AddWithValue("@CourseFee", course.CourseFee);
                                                cmdCourse.Parameters.AddWithValue("@Discount", course.Discount);
                                                cmdCourse.Parameters.AddWithValue("@FinalFee", course.FinalFee);
                                                cmdCourse.Parameters.AddWithValue("@Remarks", course.Remarks ?? (object)DBNull.Value);
                                                cmdCourse.Parameters.AddWithValue("@CreatedByUserID", currentUserId);

                                                cmdCourse.ExecuteNonQuery();

                                            }



                                            using (SqlCommand cmdGetExisting = new SqlCommand("select * from MasterCourse where CourseID = @CourseID", con, transaction))
                                            {
                                                cmdGetExisting.Parameters.AddWithValue("@CourseID", course.CourseID);
                                                using (SqlDataReader drCourse = cmdGetExisting.ExecuteReader())
                                                {
                                                    while (drCourse.Read())
                                                    {
                                                        //existingCourseIDs.Add(Convert.ToInt32(drCourse["StudentCourseRegistrationID"]));
                                                        using (SqlCommand cmdCourse = new SqlCommand(@"
                                                    INSERT INTO StudentMasterCourse 
                                                    (StudentID, SelectCourseID, CourseID, CreatedDateTime, CreatedByUserID, CourseFee)
                                                    VALUES 
                                                    (@StudentID, @SelectCourseID, @CourseID, GETDATE(), @CreatedByUserID, @CourseFee)", con, transaction))
                                                        {
                                                            cmdCourse.Parameters.AddWithValue("@StudentID", studentId);
                                                            cmdCourse.Parameters.AddWithValue("@SelectCourseID", drCourse["SelectCourseID"]);
                                                            cmdCourse.Parameters.AddWithValue("@CourseID", course.CourseID);
                                                            cmdCourse.Parameters.AddWithValue("@CreatedByUserID", currentUserId);
                                                            cmdCourse.Parameters.AddWithValue("@CourseFee", drCourse["CourseFee"]);
                                                            cmdCourse.ExecuteNonQuery();
                                                        }



                                                    }
                                                }
                                            }



                                        }
                                    }
                                    // 4. If incoming rows < existing rows: Delete extra remaining rows from the end
                                    else if (incomingCount < existingCount)
                                    {
                                        for (int i = incomingCount; i < existingCount; i++)
                                        {
                                            int courseRegIdToDelete = existingCourseIDs[i];




                                            using (SqlCommand cmdGetExisting = new SqlCommand("select * from StudentRegistrationCourse where StudentCourseRegistrationID = @StudentCourseRegistrationID and StudentID = @StudentID", con, transaction))
                                            {
                                                cmdGetExisting.Parameters.AddWithValue("@StudentCourseRegistrationID", courseRegIdToDelete);
                                                cmdGetExisting.Parameters.AddWithValue("@StudentID", studentId);
                                                using (SqlDataReader drCourse = cmdGetExisting.ExecuteReader())
                                                {
                                                    while (drCourse.Read())
                                                    {
                                                        //existingCourseIDs.Add(Convert.ToInt32(drCourse["StudentCourseRegistrationID"]));
                                                        using (SqlCommand cmdDel = new SqlCommand("delete from StudentMasterCourse where StudentID = @StudentID and  CourseID = @CourseID", con, transaction))
                                                        {
                                                            cmdDel.Parameters.AddWithValue("@CourseID", drCourse["CourseID"]);
                                                            cmdDel.Parameters.AddWithValue("@StudentID", studentId);
                                                            cmdDel.ExecuteNonQuery();
                                                        }



                                                    }
                                                }
                                            }


                                            using (SqlCommand cmdDel = new SqlCommand("DELETE FROM StudentRegistrationCourse WHERE StudentCourseRegistrationID = @StudentCourseRegistrationID AND StudentID = @StudentID", con, transaction))
                                            {
                                                cmdDel.Parameters.AddWithValue("@StudentCourseRegistrationID", courseRegIdToDelete);
                                                cmdDel.Parameters.AddWithValue("@StudentID", studentId);
                                                cmdDel.ExecuteNonQuery();
                                            }







                                        }
                                    }
                                }
                                else
                                {
                                    // New admission (!isUpdate): Insert all courses
                                    foreach (var course in model.CourseList)
                                    {
                                        using (SqlCommand cmdCourse = new SqlCommand(@"
                                            INSERT INTO StudentRegistrationCourse 
                                            (StudentID, CourseID, BatchID, AdmissionDate, CourseFee, Discount, FinalFee, Remarks, CourseStatus, CreatedDateTime, CreatedByUserID)
                                            VALUES 
                                            (@StudentID, @CourseID, @BatchID, GETDATE(), @CourseFee, @Discount, @FinalFee, @Remarks, 'Active', GETDATE(), @CreatedByUserID)", con, transaction))
                                        {
                                            cmdCourse.Parameters.AddWithValue("@StudentID", studentId);
                                            cmdCourse.Parameters.AddWithValue("@CourseID", course.CourseID);
                                            cmdCourse.Parameters.AddWithValue("@BatchID", model.BatchID.HasValue ? model.BatchID.Value : 0);
                                            cmdCourse.Parameters.AddWithValue("@CourseFee", course.CourseFee);
                                            cmdCourse.Parameters.AddWithValue("@Discount", course.Discount);
                                            cmdCourse.Parameters.AddWithValue("@FinalFee", course.FinalFee);
                                            cmdCourse.Parameters.AddWithValue("@Remarks", course.Remarks ?? (object)DBNull.Value);
                                            cmdCourse.Parameters.AddWithValue("@CreatedByUserID", currentUserId);

                                            cmdCourse.ExecuteNonQuery();
                                        }

                                        using (SqlCommand cmdGetExisting = new SqlCommand("select * from MasterCourse where CourseID = @CourseID", con, transaction))
                                        {
                                            cmdGetExisting.Parameters.AddWithValue("@CourseID", course.CourseID);
                                            using (SqlDataReader drCourse = cmdGetExisting.ExecuteReader())
                                            {
                                                while (drCourse.Read())
                                                {
                                                    //existingCourseIDs.Add(Convert.ToInt32(drCourse["StudentCourseRegistrationID"]));
                                                    using (SqlCommand cmdCourse = new SqlCommand(@"
                                                    INSERT INTO StudentMasterCourse 
                                                    (StudentID, SelectCourseID, CourseID, CreatedDateTime, CreatedByUserID, CourseFee)
                                                    VALUES 
                                                    (@StudentID, @SelectCourseID, @CourseID, GETDATE(), @CreatedByUserID, @CourseFee)", con, transaction))
                                                    {
                                                        cmdCourse.Parameters.AddWithValue("@StudentID", studentId);
                                                        cmdCourse.Parameters.AddWithValue("@SelectCourseID", drCourse["SelectCourseID"]);
                                                        cmdCourse.Parameters.AddWithValue("@CourseID", course.CourseID);
                                                        cmdCourse.Parameters.AddWithValue("@CreatedByUserID", currentUserId);
                                                        cmdCourse.Parameters.AddWithValue("@CourseFee", drCourse["CourseFee"]);
                                                        cmdCourse.ExecuteNonQuery();
                                                    }



                                                }
                                            }
                                        }





                                    }




                                }
                            }

                            // 3. SAVE FEES INSTALLMENTS (FeesInstallmentMaster)
                            if (studentId > 0 && model.InstallmentList != null && model.InstallmentList.Count > 0)
                            {

                                if (isUpdate)
                                {
                                    // 1. Fetch existing installment IDs for this student ordered by FeesInstallmentID ASC
                                    List<int> existingInstIDs = new List<int>();
                                    using (SqlCommand cmdGetExisting = new SqlCommand("SELECT FeesInstallmentID FROM FeesInstallmentMaster WHERE StudentID = @StudentID ORDER BY FeesInstallmentID ASC", con, transaction))
                                    {
                                        cmdGetExisting.Parameters.AddWithValue("@StudentID", studentId);
                                        using (SqlDataReader drInst = cmdGetExisting.ExecuteReader())
                                        {
                                            while (drInst.Read())
                                            {
                                                existingInstIDs.Add(Convert.ToInt32(drInst["FeesInstallmentID"]));
                                            }
                                        }
                                    }

                                    int existingCount = existingInstIDs.Count;
                                    int incomingCount = model.InstallmentList.Count;

                                    // 2. Update matching rows
                                    int updateCount = Math.Min(existingCount, incomingCount);
                                    for (int i = 0; i < updateCount; i++)
                                    {
                                        var inst = model.InstallmentList[i];
                                        DateTime? dtFeesDate = null;
                                        if (inst.FeesDate.HasValue) dtFeesDate = inst.FeesDate.Value;
                                        else if (DateTime.TryParse(inst.DueDate, out DateTime parsedDate)) dtFeesDate = parsedDate;

                                        decimal amount = inst.FeesAmount > 0 ? inst.FeesAmount : inst.Amount;
                                        int instIdToUpdate = existingInstIDs[i];

                                        using (SqlCommand cmdUpdate = new SqlCommand(@"
                                            UPDATE FeesInstallmentMaster
                                            SET FeesDate = @FeesDate,
                                                FeesTypeID = @FeesTypeID,
                                                FeesAmount = @FeesAmount,
                                                FinancialYearID = @FinancialYearID
                                            WHERE FeesInstallmentID = @FeesInstallmentID AND StudentID = @StudentID", con, transaction))
                                        {
                                            cmdUpdate.Parameters.AddWithValue("@FeesDate", dtFeesDate.HasValue ? (object)dtFeesDate.Value : DBNull.Value);
                                            cmdUpdate.Parameters.AddWithValue("@FeesTypeID", inst.FeesTypeID.HasValue && inst.FeesTypeID.Value > 0 ? (object)inst.FeesTypeID.Value : DBNull.Value);
                                            cmdUpdate.Parameters.AddWithValue("@FeesAmount", amount);
                                            cmdUpdate.Parameters.AddWithValue("@FinancialYearID", model.FinancialYearID > 0 ? model.FinancialYearID : 1);
                                            cmdUpdate.Parameters.AddWithValue("@FeesInstallmentID", instIdToUpdate);
                                            cmdUpdate.Parameters.AddWithValue("@StudentID", studentId);

                                            cmdUpdate.ExecuteNonQuery();
                                        }
                                    }
                                }
                                else
                                {
                                    // New admission: Insert all installments
                                        foreach (var inst in model.InstallmentList)
                                        {
                                            DateTime? dtFeesDate = null;
                                            if (inst.FeesDate.HasValue) dtFeesDate = inst.FeesDate.Value;
                                            else if (DateTime.TryParse(inst.DueDate, out DateTime parsedDate)) dtFeesDate = parsedDate;

                                            decimal amount = inst.FeesAmount > 0 ? inst.FeesAmount : inst.Amount;

                                            using (SqlCommand cmdInst = new SqlCommand(@"
                                            INSERT INTO FeesInstallmentMaster
                                            (FeesDate, FeesTypeID, FeesAmount, StudentID, FinancialYearID, CreatedDateTime, CreatedByUserID)
                                            VALUES
                                            (@FeesDate, @FeesTypeID, @FeesAmount, @StudentID, @FinancialYearID, GETDATE(), @CreatedByUserID)", con, transaction))
                                            {
                                                cmdInst.Parameters.AddWithValue("@FeesDate", dtFeesDate.HasValue ? (object)dtFeesDate.Value : DBNull.Value);
                                                cmdInst.Parameters.AddWithValue("@FeesTypeID", inst.FeesTypeID.HasValue && inst.FeesTypeID.Value > 0 ? (object)inst.FeesTypeID.Value : DBNull.Value);
                                                cmdInst.Parameters.AddWithValue("@FeesAmount", amount);
                                                cmdInst.Parameters.AddWithValue("@StudentID", studentId);
                                                cmdInst.Parameters.AddWithValue("@FinancialYearID", model.FinancialYearID > 0 ? model.FinancialYearID : 1);
                                                cmdInst.Parameters.AddWithValue("@CreatedByUserID", currentUserId);

                                                cmdInst.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                }

                            // 3.1 SAVE STUDENT DOCUMENTS (StudentDocuments)
                            if (studentId > 0)
                            {
                                // 1. Fetch old document paths from DB for this StudentID
                                List<string> oldDocPaths = new List<string>();
                                using (SqlCommand cmdGetOldDoc = new SqlCommand("SELECT DocumentPath FROM StudentDocuments WHERE StudentID = @StudentID AND DocumentPath IS NOT NULL AND DocumentPath <> ''", con, transaction))
                                {
                                    cmdGetOldDoc.Parameters.AddWithValue("@StudentID", studentId);
                                    using (SqlDataReader drOldDoc = cmdGetOldDoc.ExecuteReader())
                                    {
                                        while (drOldDoc.Read())
                                        {
                                            string p = drOldDoc["DocumentPath"]?.ToString();
                                            if (!string.IsNullOrEmpty(p))
                                            {
                                                oldDocPaths.Add(p);
                                            }
                                        }
                                    }
                                }

                                // 2. Identify new document paths to preserve (if user kept existing documents)
                                HashSet<string> newDocPathsToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                if (model.DocumentList != null)
                                {
                                    foreach (var d in model.DocumentList)
                                    {
                                        if (!string.IsNullOrEmpty(d.DocumentPath))
                                        {
                                            newDocPathsToKeep.Add(d.DocumentPath.Trim());
                                        }
                                    }
                                }

                                // 3. Delete physical files from folder that are removed or replaced
                                foreach (string oldPath in oldDocPaths)
                                {
                                    if (!newDocPathsToKeep.Contains(oldPath.Trim()))
                                    {
                                        try
                                        {
                                            string cleanPath = oldPath.Trim();
                                            if (!cleanPath.StartsWith("~") && cleanPath.StartsWith("/"))
                                            {
                                                cleanPath = "~" + cleanPath;
                                            }
                                            string physicalPath = Server.MapPath(cleanPath);
                                            if (System.IO.File.Exists(physicalPath))
                                            {
                                                System.IO.File.Delete(physicalPath);
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            // Ignore if file is already deleted or in use
                                        }
                                    }
                                }

                                // 4. Delete existing records from StudentDocuments table
                                SqlCommand cmdDeleteDoc = new SqlCommand("DELETE FROM StudentDocuments WHERE StudentID = @StudentID", con, transaction);
                                cmdDeleteDoc.Parameters.AddWithValue("@StudentID", studentId);
                                cmdDeleteDoc.ExecuteNonQuery();

                                // 5. Insert new records into StudentDocuments table
                                if (model.DocumentList != null && model.DocumentList.Count > 0)
                                {
                                    foreach (var doc in model.DocumentList)
                                    {
                                        if (doc.StudentDocumentID > 0)
                                        {
                                            SqlCommand cmdDoc = new SqlCommand(@"
                                                INSERT INTO StudentDocuments
                                                (StudentID, StudentDocumentID, DocumentPath, DocumentName, CreatedDateTime, CreatedByUserID)
                                                VALUES
                                                (@StudentID, @StudentDocumentID, @DocumentPath, @DocumentName, GETDATE(), @CreatedByUserID)", con, transaction);

                                            cmdDoc.Parameters.AddWithValue("@StudentID", studentId);
                                            cmdDoc.Parameters.AddWithValue("@StudentDocumentID", doc.StudentDocumentID);
                                            cmdDoc.Parameters.AddWithValue("@DocumentPath", !string.IsNullOrEmpty(doc.DocumentPath) ? (object)doc.DocumentPath : DBNull.Value);
                                            cmdDoc.Parameters.AddWithValue("@DocumentName", !string.IsNullOrEmpty(doc.DocumentName) ? (object)doc.DocumentName : DBNull.Value);
                                            cmdDoc.Parameters.AddWithValue("@CreatedByUserID", currentUserId);

                                            cmdDoc.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // 4. CONVERT LINKED ENQUIRY STATUS IF APPLICABLE
                            if (model.StudentEnquiryID.HasValue && model.StudentEnquiryID.Value > 0)
                            {
                                SqlCommand cmdEnquiry = new SqlCommand(@"
                                    UPDATE StudentEnquiryMaster 
                                    SET IsConvertedToAdmission = 1,IsFollowUpRequired = 0 WHERE StudentEnquiryID = @StudentEnquiryID", con, transaction);

                                cmdEnquiry.Parameters.AddWithValue("@StudentEnquiryID", model.StudentEnquiryID.Value);
                                cmdEnquiry.ExecuteNonQuery();
                            }

                            int hfreceiptId = 0;

                            // 5. AUTO SAVE ₹200 ADMISSION FEE RECEIPT FOR NEW ADMISSION
                            if (!isUpdate && studentId > 0)
                            {
                                try
                                {
                                    // 5.1 Get PaymentTypeID and TransactionNo
                                    int paymentTypeId = (model.PaymentTypeID.HasValue && model.PaymentTypeID.Value == 4) ? 4 : 1;
                                    string paymentTypeTitle = paymentTypeId == 4 ? "UPI" : "Cash";
                                    string transactionNo = paymentTypeId == 4 ? (model.TransactionNo ?? "").Trim() : "";

                                    string queryPayType = paymentTypeId == 4 ?
                                        "SELECT TOP 1 PaymentTypeID, PaymentTypeTitle FROM PaymentTypeMaster WHERE PaymentTypeID = 4 OR PaymentTypeTitle LIKE '%UPI%'" :
                                        "SELECT TOP 1 PaymentTypeID, PaymentTypeTitle FROM PaymentTypeMaster WHERE PaymentTypeID = 1 OR PaymentTypeTitle LIKE '%Cash%'";

                                    SqlCommand cmdPayType = new SqlCommand(queryPayType, con, transaction);
                                    using (SqlDataReader drPay = cmdPayType.ExecuteReader())
                                    {
                                        if (drPay.Read())
                                        {
                                            paymentTypeId = Convert.ToInt32(drPay["PaymentTypeID"]);
                                            paymentTypeTitle = drPay["PaymentTypeTitle"].ToString();
                                        }
                                    }

                                    // 5.2 Get or Create FeesTypeID for Admission Fee
                                    int feesTypeId = 0;
                                    SqlCommand cmdFeeType = new SqlCommand(@"SELECT TOP 1 FeesTypeID FROM FeesTypeMaster WHERE FeesTypeTitle LIKE '%Admission%'", con, transaction);
                                    object ftObj = cmdFeeType.ExecuteScalar();
                                    if (ftObj != null && ftObj != DBNull.Value)
                                    {
                                        feesTypeId = Convert.ToInt32(ftObj);
                                    }
                                    else
                                    {
                                        SqlCommand cmdInsFT = new SqlCommand(@"INSERT INTO FeesTypeMaster (FeesTypeTitle, OrganizationID, IsActive) VALUES ('Admission Fee', @OrgID, 1); SELECT SCOPE_IDENTITY();", con, transaction);
                                        cmdInsFT.Parameters.AddWithValue("@OrgID", model.OrganizationID > 0 ? model.OrganizationID : 1);
                                        object newFtObj = cmdInsFT.ExecuteScalar();
                                        if (newFtObj != null && newFtObj != DBNull.Value)
                                        {
                                            feesTypeId = Convert.ToInt32(newFtObj);
                                        }
                                    }

                                    // 5.3 Generate Fee Receipt No
                                    string branchCode = "REC";
                                    SqlCommand cmdBranchCode = new SqlCommand("SELECT ISNULL(BranchCode, 'REC') FROM BranchMaster WHERE BranchID = @BranchID", con, transaction);
                                    cmdBranchCode.Parameters.AddWithValue("@BranchID", model.BranchID);
                                    object bCodeObj = cmdBranchCode.ExecuteScalar();
                                    if (bCodeObj != null && bCodeObj != DBNull.Value && !string.IsNullOrWhiteSpace(bCodeObj.ToString()))
                                    {
                                        branchCode = bCodeObj.ToString().Trim();
                                    }

                                    string currentYear = DateTime.Now.Year.ToString();
                                    string prefix = branchCode + "-" + currentYear + "-";
                                    int nextSeq = 1;

                                    SqlCommand cmdMaxSeq = new SqlCommand(@"
                                        SELECT MAX(CAST(RIGHT(FeeReceiptNo, 4) AS INT)) 
                                        FROM StudentFeeReceipt 
                                        WHERE BranchID = @BranchID AND FeeReceiptNo LIKE @Prefix + '%'", con, transaction);
                                    cmdMaxSeq.Parameters.AddWithValue("@BranchID", model.BranchID);
                                    cmdMaxSeq.Parameters.AddWithValue("@Prefix", prefix);
                                    object maxObj = cmdMaxSeq.ExecuteScalar();
                                    if (maxObj != null && maxObj != DBNull.Value)
                                    {
                                        nextSeq = Convert.ToInt32(maxObj) + 1;
                                    }

                                    string autoFeeReceiptNo = string.Format("{0}{1:D4}", prefix, nextSeq);
                                    int orgId = model.OrganizationID > 0 ? model.OrganizationID : 1;
                                    int finYearId = model.FinancialYearID > 0 ? model.FinancialYearID : 1;
                                    int createdByUserId = currentUserId > 0 ? currentUserId : 1;

                                    // 5.4 Insert into StudentFeeReceipt
                                    string insertFeeReceiptSql = @"
                                        INSERT INTO StudentFeeReceipt
                                        (
                                            StudentID, BranchID, FeeReceiptNo, ReceiptDate, AmountReceived,
                                            PaymentTypeID, FeesTypeID, TransactionNo, BankName, Remarks,
                                            ReceivedByUserID, FinancialYearID, CreatedDateTime, CreatedByUserID
                                        )
                                        VALUES
                                        (
                                            @StudentID, @BranchID, @FeeReceiptNo, GETDATE(), 200.00,
                                            @PaymentTypeID, @FeesTypeID, @TransactionNo, '', 'Auto Generated Admission Fee Receipt',
                                            @ReceivedByUserID, @FinancialYearID, GETDATE(), @CreatedByUserID
                                        );
                                        SELECT SCOPE_IDENTITY();";

                                    SqlCommand cmdInsReceipt = new SqlCommand(insertFeeReceiptSql, con, transaction);
                                    cmdInsReceipt.Parameters.AddWithValue("@StudentID", studentId);
                                    cmdInsReceipt.Parameters.AddWithValue("@BranchID", model.BranchID);
                                    cmdInsReceipt.Parameters.AddWithValue("@FeeReceiptNo", autoFeeReceiptNo);
                                    cmdInsReceipt.Parameters.AddWithValue("@PaymentTypeID", paymentTypeId);
                                    cmdInsReceipt.Parameters.AddWithValue("@FeesTypeID", feesTypeId > 0 ? (object)feesTypeId : DBNull.Value);
                                    cmdInsReceipt.Parameters.AddWithValue("@TransactionNo", transactionNo ?? "");
                                    cmdInsReceipt.Parameters.AddWithValue("@ReceivedByUserID", createdByUserId);
                                    cmdInsReceipt.Parameters.AddWithValue("@FinancialYearID", finYearId);
                                    cmdInsReceipt.Parameters.AddWithValue("@CreatedByUserID", createdByUserId);

                                    int autoReceiptId = Convert.ToInt32(cmdInsReceipt.ExecuteScalar());
                                    hfreceiptId = autoReceiptId;
                                    // 5.5 Insert into BranchLedger
                                    string ledgerDesc = $"Admission Fee Receipt from {model.StudentFullName ?? ""} ({model.StudentStringEnrollmentNo ?? ""})";
                                    string insertLedgerSql = @"
                                        INSERT INTO BranchLedger
                                        (
                                            OrganizationID, BranchID, FinancialYearID, TransactionDate,
                                            RelatedObjName, RelatedObjID, ReferenceNo, Description,
                                            Credit, Debit, PaymentType, Remarks, CreatedDateTime, CreatedByUserID
                                        )
                                        VALUES
                                        (
                                            @OrganizationID, @BranchID, @FinancialYearID, GETDATE(),
                                            'StudentFeeReceipt', @RelatedObjID, @ReferenceNo, @Description,
                                            200.00, 0.00, @PaymentType, 'Auto Generated Admission Fee Receipt',
                                            GETDATE(), @CreatedByUserID
                                        );";

                                    SqlCommand cmdInsLedger = new SqlCommand(insertLedgerSql, con, transaction);
                                    cmdInsLedger.Parameters.AddWithValue("@OrganizationID", orgId);
                                    cmdInsLedger.Parameters.AddWithValue("@BranchID", model.BranchID);
                                    cmdInsLedger.Parameters.AddWithValue("@FinancialYearID", finYearId);
                                    cmdInsLedger.Parameters.AddWithValue("@RelatedObjID", autoReceiptId);
                                    cmdInsLedger.Parameters.AddWithValue("@ReferenceNo", autoFeeReceiptNo);
                                    cmdInsLedger.Parameters.AddWithValue("@Description", ledgerDesc);
                                    cmdInsLedger.Parameters.AddWithValue("@PaymentType", paymentTypeTitle);
                                    cmdInsLedger.Parameters.AddWithValue("@CreatedByUserID", createdByUserId);

                                    cmdInsLedger.ExecuteNonQuery();
                                }
                                catch
                                {
                                    // Suppress exception to ensure main student admission save succeeds
                                }
                            }

                            transaction.Commit();

                            // Trigger Admission Confirmation Email if email is present
                            if (studentId > 0 && !string.IsNullOrEmpty(model.StudentEmail))
                            {
                                int sId = studentId;
                                System.Threading.Tasks.Task.Run(() =>
                                {
                                    SendAdmissionConfirmationEmail(sId);
                                });
                            }

                            if (isUpdate == true)
                            {
                                return Json(new { IsSuccess = true, Message = isUpdate ? "Student details updated successfully!" : "Student admission registered successfully!", StudentID = studentId, ReceiptId = 0 });
                            }
                            else
                            {
                                return Json(new { IsSuccess = true, Message = isUpdate ? "Student details updated successfully!" : "Student admission registered successfully!", StudentID = studentId, ReceiptId = Encrypt(Convert.ToInt32(hfreceiptId).ToString()) });
                            }                        
                         
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            return Json(new { IsSuccess = false, Message = "Database Error: " + ex.Message });
                        }
                    }
                    
                }
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = "Server Error: " + ex.Message });
            }
        }

        #region Send Admission Confirmation Email
        [HttpPost]
        public JsonResult SendAdmissionEmail(int studentId)
        {
            try
            {
                bool result = SendAdmissionConfirmationEmail(studentId);
                return Json(new { IsSuccess = result, Message = result ? "Admission confirmation email sent successfully!" : "Failed to send admission email. Please verify student email address." });
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = ex.Message });
            }
        }

        public bool SendAdmissionConfirmationEmail(int studentId)
        {
            try
            {
                if (studentId <= 0) return false;

                string email = "";
                string studentPhoto = "";
                string studentStringEnNo = "";
                DateTime custDateTime = DateTime.Now;
                string studentFullName = "";
                string studentAddress = "";
                string studentMobileNo = "";
                string studentAlternateNo = "";
                string courseTitle = "";
                string batchTime = "";
                string studentPassword = "";

                using (SqlConnection conn = new SqlConnection(CS))
                {
                    conn.Open();
                    string sql = @"
                        SELECT 
                            ISNULL(s.StudentEmail, '') AS StudentEmail,
                            ISNULL(s.StudentPhoto, 'Content/logo/VCSS_Logo_F1.jpg') AS StudentPhoto,
                            ISNULL(s.StudentStringEnrollmentNo, '') AS StudentStringEnrollmentNo,
                            ISNULL(s.AdminssionDateTime, GETDATE()) AS AdminssionDateTime,
                            ISNULL(s.StudentFullName, '') AS StudentFullName,
                            ISNULL(s.Address, '') AS Address,
                            ISNULL(s.StudentMobileNo, '') AS StudentMobileNo,
                            ISNULL(s.WhatsAppNo, ISNULL(s.FatherMobileNo, '')) AS StudentAlternateNo,
                            STUFF((
                                SELECT ', ' + c.CourseTitle
                                FROM StudentRegistrationCourse src
                                INNER JOIN CourseMaster c ON src.CourseID = c.CourseID
                                WHERE src.StudentID = s.StudentID
                                FOR XML PATH('')
                            ), 1, 2, '') AS CourseTitle,
                            ISNULL(b.BatchTitle, '') AS BatchTime
                        FROM StudentMaster s
                      LEFT JOIN BranchBatchMaster b ON s.BatchID = b.BatchID
                        WHERE s.StudentID = @StudentID;";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@StudentID", studentId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                email = Convert.ToString(reader["StudentEmail"]).Trim();
                                studentPhoto = Convert.ToString(reader["StudentPhoto"]).Trim();
                                studentStringEnNo = Convert.ToString(reader["StudentStringEnrollmentNo"]);
                                if (reader["AdminssionDateTime"] != DBNull.Value)
                                {
                                    custDateTime = Convert.ToDateTime(reader["AdminssionDateTime"]);
                                }
                                studentFullName = Convert.ToString(reader["StudentFullName"]);
                                studentAddress = Convert.ToString(reader["Address"]);
                                studentMobileNo = Convert.ToString(reader["StudentMobileNo"]);
                                studentAlternateNo = Convert.ToString(reader["StudentAlternateNo"]);
                                courseTitle = Convert.ToString(reader["CourseTitle"]);
                                batchTime = Convert.ToString(reader["BatchTime"]);
                                studentPassword = studentMobileNo;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(email)) return false;

                string str_body = "";
                str_body += "<table style='width:100%;' cellpadding='3px'><tbody>";
                str_body += "<tr><td style='width:50%;'><br />";
                str_body += "<table border='0' style='width: 99%; border-collapse: collapse; font-size: x-small;' frame='box'>";
                str_body += "<tbody>";
                str_body += "<tr><td style='text-align: center;' colspan='2'><img src='http://bctech.org.in/Content/logo/VCSS_Logo_F1.jpg' style='width:130px; margin: 5px 5px;'></td>";
                str_body += "<td style='background-color:black; text-align: center;'> <img src='http://bctech.org.in/Content/logo/LOGO_MAIN.png'></td>";
                str_body += "</tr>";
                str_body += "<tr style='font-size: medium;'><td colspan='3'>";
                str_body += "<table border='0' style='width: 100%; border-collapse: collapse;' frame='box'><tbody>";
                str_body += "<tr><td rowspan='6' style='text-align: center;'><img src='http://bctech.org.in/" + studentPhoto + "' style='width: 150px; height: 150px; border: 1px solid; padding: 2px;'></td>";
                str_body += "<td style='border-bottom: 1px solid #b7b7b7;'><b>Ad. No.:</b> " + studentStringEnNo + "</td>";
                str_body += "<td style='border-bottom: 1px solid #b7b7b7;'><b>Ad. Date: </b> " + custDateTime.ToString("dd-MMM-yyyy") + " </td></tr>";
                str_body += "<tr style='border-bottom: 1px solid #000;'><td colspan='2'><b>Name: </b>" + studentFullName + "</td></tr>";
                str_body += "<tr style='border-bottom: 1px solid #000;'><td colspan='2'><b>Address: </b>" + studentAddress + "</td></tr>";
                str_body += "<tr><td style='border-bottom: 1px solid #000;'><b>Contact:</b>" + studentMobileNo + "  " + studentAlternateNo + "</td>";
                str_body += "<td style='border-bottom: 1px solid #000;'><b>Email</b>: " + email + "</td></tr>";
                str_body += "<tr><td style='border-bottom: 1px solid #000;'><b>Course:</b> " + courseTitle + " </td>";
                str_body += "<td style='border-bottom: 1px solid #000'><b>Batch Time:</b> " + batchTime + " </td></tr>";
                str_body += "<tr><td style='border-bottom: 0px'><b>Password:</b> " + studentPassword + "</td>";
                str_body += "<td style='border-bottom: 0px; text-align:center;'></td></tr>";
                str_body += "</tbody></table>";
                str_body += "</td></tr>";
                str_body += "<tr><td colspan='3' style='text-align:center; color: red; padding: 5px;'>Navagam: 0261-276655, Nilgiri: 0261-2273803, Navagam: 7778926287, Limbayat: 9558576769</td></tr>";
                str_body += "</tbody></table><br />";
                str_body += "</td>";
                str_body += "</tr></tbody>";
                str_body += "</table>";

                string Subject = "BC Tech - Admission Confirm";

                MailMessage mailMessage = new MailMessage();
                mailMessage.From = new MailAddress("info@bctech.org.in", "Admission Confirm");
                mailMessage.To.Add(email);
                mailMessage.Subject = Subject;
                mailMessage.Body = str_body;
                mailMessage.IsBodyHtml = true;

                SmtpClient smtpClient = new SmtpClient();
                smtpClient.Host = "plesk7200.is.cc";
                smtpClient.Port = 587;
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new NetworkCredential("info@bctech.org.in", "1w_840Dka");
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.EnableSsl = true;

                smtpClient.Send(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        #endregion

        [HttpPost]
        public JsonResult GetFeesTypeDropdown()
        {
            try
            {
                List<object> list = new List<object>();
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = @"SELECT FeesTypeID, ISNULL(FeesTypeTitle, '') AS FeesTypeTitle 
                                     FROM FeesTypeMaster 
                                     ORDER BY FeesTypeID ASC";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new
                                {
                                    FeesTypeID = Convert.ToInt32(dr["FeesTypeID"]),
                                    FeesTypeTitle = dr["FeesTypeTitle"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                return Json(list, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult GetStudentDocumentDropdown()
        {
            try
            {
                List<object> list = new List<object>();
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = @"SELECT StudentDocumentID, ISNULL(StudentDocumentTitle, '') AS StudentDocumentTitle 
                                     FROM StudentDocumentMaster 
                                     ORDER BY StudentDocumentID ASC";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new
                                {
                                    StudentDocumentID = Convert.ToInt32(dr["StudentDocumentID"]),
                                    StudentDocumentTitle = dr["StudentDocumentTitle"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                return Json(list, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);
            }
        }
        // POST: Generate Next Branch-Wise Enrollment Number
        [HttpPost]
        public ActionResult GetNextEnrollmentNo(int branchID)
        {
            try
            {
                string nextEnrollmentNo = "";

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    // 1. Get Branch Code from BranchMaster
                    string branchCode = "BR";
                    string branchQuery = "SELECT ISNULL(BranchCode, 'BR') FROM BranchMaster WHERE BranchID = @BranchID";

                    using (SqlCommand cmdBranch = new SqlCommand(branchQuery, con))
                    {
                        cmdBranch.Parameters.AddWithValue("@BranchID", branchID);
                        object result = cmdBranch.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            branchCode = result.ToString().Trim();
                        }
                    }

                    // 2. Fetch Max Enrollment ID or Count for this specific Branch
                    string currentYear = DateTime.Now.Year.ToString(); // e.g. 2026
                    string maxQuery = @"
                SELECT MAX(CAST(RIGHT(StudentStringEnrollmentNo, 4) AS INT)) 
                FROM StudentMaster 
                WHERE BranchID = @BranchID 
                  AND StudentStringEnrollmentNo LIKE @BranchPrefix + '%'";

                    string branchPrefix = branchCode + "-" + currentYear + "-"; // e.g. "BR01-2026-"
                    int nextNumber = 1;

                    using (SqlCommand cmdMax = new SqlCommand(maxQuery, con))
                    {
                        cmdMax.Parameters.AddWithValue("@BranchID", branchID);
                        cmdMax.Parameters.AddWithValue("@BranchPrefix", branchPrefix);

                        object maxVal = cmdMax.ExecuteScalar();
                        if (maxVal != null && maxVal != DBNull.Value)
                        {
                            nextNumber = Convert.ToInt32(maxVal) + 1;
                        }
                    }

                    // 3. Format as PREFIX + 4-digit zero-padded number (e.g., BR01-2026-0001)
                    nextEnrollmentNo = string.Format("{0}{1:D4}", branchPrefix, nextNumber);
                }

                return Json(new { IsSuccess = true, EnrollmentNo = nextEnrollmentNo }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        #endregion
        #region Library Books Manage



        #region BookCategory
        public ActionResult BookCategory()
        {
            ViewData["ParentMenu"] = "Library";
            ViewData["PageTitle"] = "Book Category Master";
            return View();
        }

        [HttpGet]
        public JsonResult GetBookCategoryList()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();
                string query = @"SELECT
                            BookCategoryID,
                            BookCategoryTitle, IsActive, 
                            CONVERT(varchar(10),CreatedDateTime,103) AS CreatedDateTime, 
                            CreatedByUserID
                         FROM BookCategoryMaster
                         ORDER BY BookCategoryID DESC";

                SqlCommand cmd = new SqlCommand(query, con);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new
                    {
                        BookCategoryID = Convert.ToInt32(dr["BookCategoryID"]),
                        BookCategoryTitle = dr["BookCategoryTitle"].ToString(),
                        IsActive = Convert.ToInt32(dr["IsActive"].ToString()),
                        CreatedDateTime = dr["CreatedDateTime"].ToString(),
                        CreatedByUserID = Convert.ToInt32(dr["CreatedByUserID"])
                    });
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult SaveBookCategory(BookCategoryModal model)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                // Duplicate Check
                SqlCommand checkCmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM BookCategoryMaster
            WHERE BookCategoryTitle = @BookCategoryTitle
              AND BookCategoryID <> @BookCategoryID", con);

                checkCmd.Parameters.AddWithValue("@BookCategoryTitle", model.BookCategoryTitle);
                checkCmd.Parameters.AddWithValue("@BookCategoryID", model.BookCategoryID);

                int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Book Category is already exists."
                    });
                }

                if (model.BookCategoryID == 0)
                {
                    string query = @"INSERT INTO BookCategoryMaster
                            (
                                BookCategoryTitle,
                                IsActive,
                                CreatedDateTime,
                                CreatedByUserID
                            )
                            VALUES
                            (
                                @BookCategoryTitle,
                                @IsActive,
                                GETDATE(),
                                @UserID
                            )";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@BookCategoryTitle", model.BookCategoryTitle);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    cmd.Parameters.AddWithValue("@UserID", 1);

                    cmd.ExecuteNonQuery();
                }
                else
                {
                    string query = @"UPDATE BookCategoryMaster
                             SET BookCategoryTitle = @BookCategoryTitle, IsActive = @IsActive, CreatedDateTime= GETDATE(), CreatedByUserID = @CreatedByUserID
                             WHERE BookCategoryID = @BookCategoryID";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@BookCategoryID", model.BookCategoryID);
                    cmd.Parameters.AddWithValue("@BookCategoryTitle", model.BookCategoryTitle);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    cmd.Parameters.AddWithValue("@CreatedByUserID", model.CreatedByUserID);
                    cmd.ExecuteNonQuery();
                }
            }

            return Json(new
            {
                Status = true,
                Message = "Book Category saved successfully."
            });
        }



        [HttpGet]
        public JsonResult GetBookCategoryByID(int id)
        {
            BookCategoryModal model = new BookCategoryModal();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"SELECT *
                         FROM BookCategoryMaster
                         WHERE BookCategoryID=@BookCategoryID";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@BookCategoryID", id);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    model.BookCategoryID = Convert.ToInt32(dr["BookCategoryID"]);
                    model.BookCategoryTitle = dr["BookCategoryTitle"].ToString();
                    model.IsActive = Convert.ToBoolean(dr["IsActive"]);
                    model.CreatedDateTime = Convert.ToDateTime(dr["CreatedDateTime"].ToString());
                    model.CreatedDateTimes = Convert.ToDateTime(dr["CreatedDateTime"].ToString()).ToString("dd-MM-yyyy hh:mm tt");
                    model.CreatedByUserID = Convert.ToInt32(dr["CreatedByUserID"]);
                }
            }

            return Json(model, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult DeleteBookCategory(int id)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"DELETE FROM BookCategoryMaster
                         WHERE BookCategoryID=@BookCategoryID";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@BookCategoryID", id);

                cmd.ExecuteNonQuery();
            }

            return Json("Success");
        }
        #endregion



        #region BookSource
        public ActionResult BookSource()
        {
            ViewData["ParentMenu"] = "Library";
            ViewData["PageTitle"] = "Book Category Master";
            return View();
        }

        [HttpGet]
        public JsonResult GetBookSourceList()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();
                string query = @"SELECT
                            BookSourceID,
                            BookSourceTitle, IsActive, 
                            CONVERT(varchar(10),CreatedDateTime,103) AS CreatedDateTime, 
                            CreatedByUserID
                         FROM BookSourceMaster
                         ORDER BY BookSourceID DESC";

                SqlCommand cmd = new SqlCommand(query, con);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new
                    {
                        BookSourceID = Convert.ToInt32(dr["BookSourceID"]),
                        BookSourceTitle = dr["BookSourceTitle"].ToString(),
                        IsActive = Convert.ToInt32(dr["IsActive"].ToString()),
                        CreatedDateTime = dr["CreatedDateTime"].ToString(),
                        CreatedByUserID = Convert.ToInt32(dr["CreatedByUserID"])
                    });
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult SaveBookSource(BookSourceModal model)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                // Duplicate Check
                SqlCommand checkCmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM BookSourceMaster
            WHERE BookSourceTitle = @BookSourceTitle
              AND BookSourceID <> @BookSourceID", con);

                checkCmd.Parameters.AddWithValue("@BookSourceTitle", model.BookSourceTitle);
                checkCmd.Parameters.AddWithValue("@BookSourceID", model.BookSourceID);

                int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Book Category is already exists."
                    });
                }

                if (model.BookSourceID == 0)
                {
                    string query = @"INSERT INTO BookSourceMaster
                            (
                                BookSourceTitle,
                                IsActive,
                                CreatedDateTime,
                                CreatedByUserID
                            )
                            VALUES
                            (
                                @BookSourceTitle,
                                @IsActive,
                                GETDATE(),
                                @UserID
                            )";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@BookSourceTitle", model.BookSourceTitle);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    cmd.Parameters.AddWithValue("@UserID", 1);

                    cmd.ExecuteNonQuery();
                }
                else
                {
                    string query = @"UPDATE BookSourceMaster
                             SET BookSourceTitle = @BookSourceTitle, IsActive = @IsActive, CreatedDateTime= GETDATE(), CreatedByUserID = @CreatedByUserID
                             WHERE BookSourceID = @BookSourceID";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@BookSourceID", model.BookSourceID);
                    cmd.Parameters.AddWithValue("@BookSourceTitle", model.BookSourceTitle);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    cmd.Parameters.AddWithValue("@CreatedByUserID", model.CreatedByUserID);
                    cmd.ExecuteNonQuery();
                }
            }

            return Json(new
            {
                Status = true,
                Message = "Book Category saved successfully."
            });
        }



        [HttpGet]
        public JsonResult GetBookSourceByID(int id)
        {
            BookSourceModal model = new BookSourceModal();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"SELECT *
                         FROM BookSourceMaster
                         WHERE BookSourceID=@BookSourceID";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@BookSourceID", id);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    model.BookSourceID = Convert.ToInt32(dr["BookSourceID"]);
                    model.BookSourceTitle = dr["BookSourceTitle"].ToString();
                    model.IsActive = Convert.ToBoolean(dr["IsActive"]);
                    model.CreatedDateTime = Convert.ToDateTime(dr["CreatedDateTime"].ToString());
                    model.CreatedDateTimes = Convert.ToDateTime(dr["CreatedDateTime"].ToString()).ToString("dd-MM-yyyy hh:mm tt");
                    model.CreatedByUserID = Convert.ToInt32(dr["CreatedByUserID"]);
                }
            }

            return Json(model, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult DeleteBookSource(int id)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"DELETE FROM BookSourceMaster
                         WHERE BookSourceID=@BookSourceID";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@BookSourceID", id);

                cmd.ExecuteNonQuery();
            }

            return Json("Success");
        }
        #endregion

        #region BookMaster
        public ActionResult BookMaster()
        {
            ViewData["ParentMenu"] = "Library";
            ViewData["PageTitle"] = "Book Master";
            return View();
        }

        public ActionResult CreateBookMaster(int id = 0)
        {
            ViewData["ParentMenu"] = "Library";
            ViewData["PageTitle"] = id > 0 ? "Edit Book Master" : "Create Book Master";
            ViewBag.BookID = id;
            return View();
        }

        [HttpGet]
        public JsonResult GetBookMasterList()
        {
            List<BookMasterModel> list = new List<BookMasterModel>();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();
                string query = @"SELECT 
                                    BM.BookID,
                                    BM.OrganizationID,
                                    ISNULL(OM.OrganizationName, '') AS OrganizationName,
                                    BM.BookCategoryID,
                                    ISNULL(BCM.BookCategoryTitle, '') AS BookCategoryTitle,
                                    BM.BookSourceID,
                                    ISNULL(BSM.BookSourceTitle, '') AS BookSourceTitle,
                                    BM.CourseID,
                                    ISNULL(CM.CourseTitle, '') AS CourseTitle,
                                    ISNULL(BM.BookCode, '') AS BookCode,
                                    ISNULL(BM.BookTitle, '') AS BookTitle,
                                    ISNULL(BM.BookSubTitle, '') AS BookSubTitle,
                                    ISNULL(BM.AuthorName, '') AS AuthorName,
                                    ISNULL(BM.PublisherName, '') AS PublisherName,
                                    ISNULL(BM.Price, 0) AS Price,
                                    ISNULL(BM.TotalPages, 0) AS TotalPages,
                                    ISNULL(BM.RackNo, '') AS RackNo,
                                    ISNULL(BM.BookCoverImage, '') AS BookCoverImage,
                                    ISNULL(BM.IsEBook, 0) AS IsEBook,
                                    ISNULL(BM.EBookFile, '') AS EBookFile,
                                    ISNULL(BM.BookDescription, '') AS BookDescription,
                                    ISNULL(BM.MinimumStock, 0) AS MinimumStock,
                                    ISNULL(BM.IsActive, 1) AS IsActive,
                                    CONVERT(varchar(10), BM.CreatedDateTime, 103) AS CreatedDateTime,
                                    ISNULL(BM.CreatedByUserID, 1) AS CreatedByUserID
                                 FROM BookMaster BM
                                 LEFT JOIN BookCategoryMaster BCM ON BM.BookCategoryID = BCM.BookCategoryID
                                 LEFT JOIN BookSourceMaster BSM ON BM.BookSourceID = BSM.BookSourceID
                                 LEFT JOIN OrganizationMaster OM ON BM.OrganizationID = OM.OrganizationID
                                 LEFT JOIN CourseMaster CM ON BM.CourseID = CM.CourseID
                                 ORDER BY BM.BookID DESC";

                SqlCommand cmd = new SqlCommand(query, con);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new BookMasterModel
                    {
                        BookID = Convert.ToInt32(dr["BookID"]),
                        OrganizationID = dr["OrganizationID"] == DBNull.Value ? 0 : Convert.ToInt32(dr["OrganizationID"]),
                        OrganizationName = dr["OrganizationName"].ToString(),
                        BookCategoryID = dr["BookCategoryID"] == DBNull.Value ? 0 : Convert.ToInt32(dr["BookCategoryID"]),
                        BookCategoryTitle = dr["BookCategoryTitle"].ToString(),
                        BookSourceID = dr["BookSourceID"] == DBNull.Value ? 0 : Convert.ToInt32(dr["BookSourceID"]),
                        BookSourceTitle = dr["BookSourceTitle"].ToString(),
                        CourseID = dr["CourseID"] == DBNull.Value ? 0 : Convert.ToInt32(dr["CourseID"]),
                        CourseTitle = dr["CourseTitle"].ToString(),
                        BookCode = dr["BookCode"].ToString(),
                        BookTitle = dr["BookTitle"].ToString(),
                        BookSubTitle = dr["BookSubTitle"].ToString(),
                        AuthorName = dr["AuthorName"].ToString(),
                        PublisherName = dr["PublisherName"].ToString(),
                        Price = Convert.ToDecimal(dr["Price"]),
                        TotalPages = Convert.ToInt32(dr["TotalPages"]),
                        RackNo = dr["RackNo"].ToString(),
                        BookCoverImage = dr["BookCoverImage"].ToString(),
                        IsEBook = Convert.ToByte(dr["IsEBook"]),
                        EBookFile = dr["EBookFile"].ToString(),
                        BookDescription = dr["BookDescription"].ToString(),
                        MinimumStock = Convert.ToInt32(dr["MinimumStock"]),
                        IsActive = Convert.ToByte(dr["IsActive"]),
                        CreatedDateTimes = dr["CreatedDateTime"].ToString(),
                        CreatedByUserID = Convert.ToInt32(dr["CreatedByUserID"])
                    });
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetBookMasterByID(int id)
        {
            BookMasterModel model = new BookMasterModel();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"SELECT * FROM BookMaster WHERE BookID = @BookID";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@BookID", id);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    model.BookID = Convert.ToInt32(dr["BookID"]);
                    model.OrganizationID = dr["OrganizationID"] == DBNull.Value ? 0 : Convert.ToInt32(dr["OrganizationID"]);
                    model.BookCategoryID = dr["BookCategoryID"] == DBNull.Value ? 0 : Convert.ToInt32(dr["BookCategoryID"]);
                    model.BookSourceID = dr["BookSourceID"] == DBNull.Value ? 0 : Convert.ToInt32(dr["BookSourceID"]);
                    model.CourseID = dr["CourseID"] == DBNull.Value ? 0 : Convert.ToInt32(dr["CourseID"]);
                    model.BookCode = dr["BookCode"] == DBNull.Value ? "" : dr["BookCode"].ToString();
                    model.BookTitle = dr["BookTitle"] == DBNull.Value ? "" : dr["BookTitle"].ToString();
                    model.BookSubTitle = dr["BookSubTitle"] == DBNull.Value ? "" : dr["BookSubTitle"].ToString();
                    model.AuthorName = dr["AuthorName"] == DBNull.Value ? "" : dr["AuthorName"].ToString();
                    model.PublisherName = dr["PublisherName"] == DBNull.Value ? "" : dr["PublisherName"].ToString();
                    model.Price = dr["Price"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["Price"]);
                    model.TotalPages = dr["TotalPages"] == DBNull.Value ? 0 : Convert.ToInt32(dr["TotalPages"]);
                    model.RackNo = dr["RackNo"] == DBNull.Value ? "" : dr["RackNo"].ToString();
                    model.BookCoverImage = dr["BookCoverImage"] == DBNull.Value ? "" : dr["BookCoverImage"].ToString();
                    model.IsEBook = dr["IsEBook"] == DBNull.Value ? (byte)0 : Convert.ToByte(dr["IsEBook"]);
                    model.EBookFile = dr["EBookFile"] == DBNull.Value ? "" : dr["EBookFile"].ToString();
                    model.BookDescription = dr["BookDescription"] == DBNull.Value ? "" : dr["BookDescription"].ToString();
                    model.MinimumStock = dr["MinimumStock"] == DBNull.Value ? 0 : Convert.ToInt32(dr["MinimumStock"]);
                    model.IsActive = dr["IsActive"] == DBNull.Value ? (byte)1 : Convert.ToByte(dr["IsActive"]);
                }
            }

            return Json(model, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult SaveBookMaster(BookMasterModel model)
        {
            try
            {
                int userId = GetLoggedInUserID();
                if (model.OrganizationID <= 0)
                {
                    model.OrganizationID = GetOrganizationID();
                }

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    // Duplicate Check by BookTitle or BookCode
                    if (!string.IsNullOrEmpty(model.BookTitle))
                    {
                        SqlCommand checkCmd = new SqlCommand(@"
                            SELECT COUNT(*)
                            FROM BookMaster
                            WHERE (BookTitle = @BookTitle OR (BookCode <> '' AND BookCode = @BookCode))
                              AND BookID <> @BookID", con);

                        checkCmd.Parameters.AddWithValue("@BookTitle", model.BookTitle);
                        checkCmd.Parameters.AddWithValue("@BookCode", model.BookCode ?? "");
                        checkCmd.Parameters.AddWithValue("@BookID", model.BookID);

                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0)
                        {
                            return Json(new
                            {
                                Status = false,
                                Message = "Book with the same title or code already exists."
                            });
                        }
                    }

                    // Handle File Uploads
                    string coverPath = model.BookCoverImage ?? "";
                    if (Request.Files["BookCoverImageFile"] != null && Request.Files["BookCoverImageFile"].ContentLength > 0)
                    {
                        var file = Request.Files["BookCoverImageFile"];
                        string ext = System.IO.Path.GetExtension(file.FileName);
                        string fileName = Guid.NewGuid().ToString() + ext;
                        string uploadDir = Server.MapPath("~/Uploads/Books/Covers/");
                        if (!System.IO.Directory.Exists(uploadDir))
                        {
                            System.IO.Directory.CreateDirectory(uploadDir);
                        }
                        file.SaveAs(System.IO.Path.Combine(uploadDir, fileName));
                        coverPath = "/Uploads/Books/Covers/" + fileName;
                    }

                    string ebookPath = model.EBookFile ?? "";
                    if (Request.Files["EBookFileObj"] != null && Request.Files["EBookFileObj"].ContentLength > 0)
                    {
                        var file = Request.Files["EBookFileObj"];
                        string ext = System.IO.Path.GetExtension(file.FileName);
                        string fileName = Guid.NewGuid().ToString() + ext;
                        string uploadDir = Server.MapPath("~/Uploads/Books/EBooks/");
                        if (!System.IO.Directory.Exists(uploadDir))
                        {
                            System.IO.Directory.CreateDirectory(uploadDir);
                        }
                        file.SaveAs(System.IO.Path.Combine(uploadDir, fileName));
                        ebookPath = "/Uploads/Books/EBooks/" + fileName;
                    }

                    if (model.BookID == 0)
                    {
                        string query = @"INSERT INTO BookMaster
                                (
                                    OrganizationID, BookCategoryID, BookSourceID, CourseID,
                                    BookCode, BookTitle, BookSubTitle, AuthorName, PublisherName,
                                    Price, TotalPages, RackNo, BookCoverImage, IsEBook, EBookFile,
                                    BookDescription, MinimumStock, IsActive, CreatedDateTime, CreatedByUserID
                                )
                                VALUES
                                (
                                    @OrganizationID, @BookCategoryID, @BookSourceID, @CourseID,
                                    @BookCode, @BookTitle, @BookSubTitle, @AuthorName, @PublisherName,
                                    @Price, @TotalPages, @RackNo, @BookCoverImage, @IsEBook, @EBookFile,
                                    @BookDescription, @MinimumStock, @IsActive, GETDATE(), @CreatedByUserID
                                )";

                        SqlCommand cmd = new SqlCommand(query, con);
                        cmd.Parameters.AddWithValue("@OrganizationID", model.OrganizationID);
                        cmd.Parameters.AddWithValue("@BookCategoryID", model.BookCategoryID);
                        cmd.Parameters.AddWithValue("@BookSourceID", model.BookSourceID);
                        cmd.Parameters.AddWithValue("@CourseID", model.CourseID);
                        cmd.Parameters.AddWithValue("@BookCode", model.BookCode ?? "");
                        cmd.Parameters.AddWithValue("@BookTitle", model.BookTitle ?? "");
                        cmd.Parameters.AddWithValue("@BookSubTitle", model.BookSubTitle ?? "");
                        cmd.Parameters.AddWithValue("@AuthorName", model.AuthorName ?? "");
                        cmd.Parameters.AddWithValue("@PublisherName", model.PublisherName ?? "");
                        cmd.Parameters.AddWithValue("@Price", model.Price);
                        cmd.Parameters.AddWithValue("@TotalPages", model.TotalPages);
                        cmd.Parameters.AddWithValue("@RackNo", model.RackNo ?? "");
                        cmd.Parameters.AddWithValue("@BookCoverImage", coverPath);
                        cmd.Parameters.AddWithValue("@IsEBook", model.IsEBook);
                        cmd.Parameters.AddWithValue("@EBookFile", ebookPath);
                        cmd.Parameters.AddWithValue("@BookDescription", model.BookDescription ?? "");
                        cmd.Parameters.AddWithValue("@MinimumStock", model.MinimumStock);
                        cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                        cmd.Parameters.AddWithValue("@CreatedByUserID", userId);

                        cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        string query = @"UPDATE BookMaster
                                 SET OrganizationID = @OrganizationID,
                                     BookCategoryID = @BookCategoryID,
                                     BookSourceID = @BookSourceID,
                                     CourseID = @CourseID,
                                     BookCode = @BookCode,
                                     BookTitle = @BookTitle,
                                     BookSubTitle = @BookSubTitle,
                                     AuthorName = @AuthorName,
                                     PublisherName = @PublisherName,
                                     Price = @Price,
                                     TotalPages = @TotalPages,
                                     RackNo = @RackNo,
                                     BookCoverImage = @BookCoverImage,
                                     IsEBook = @IsEBook,
                                     EBookFile = @EBookFile,
                                     BookDescription = @BookDescription,
                                     MinimumStock = @MinimumStock,
                                     IsActive = @IsActive
                                 WHERE BookID = @BookID";

                        SqlCommand cmd = new SqlCommand(query, con);
                        cmd.Parameters.AddWithValue("@BookID", model.BookID);
                        cmd.Parameters.AddWithValue("@OrganizationID", model.OrganizationID);
                        cmd.Parameters.AddWithValue("@BookCategoryID", model.BookCategoryID);
                        cmd.Parameters.AddWithValue("@BookSourceID", model.BookSourceID);
                        cmd.Parameters.AddWithValue("@CourseID", model.CourseID);
                        cmd.Parameters.AddWithValue("@BookCode", model.BookCode ?? "");
                        cmd.Parameters.AddWithValue("@BookTitle", model.BookTitle ?? "");
                        cmd.Parameters.AddWithValue("@BookSubTitle", model.BookSubTitle ?? "");
                        cmd.Parameters.AddWithValue("@AuthorName", model.AuthorName ?? "");
                        cmd.Parameters.AddWithValue("@PublisherName", model.PublisherName ?? "");
                        cmd.Parameters.AddWithValue("@Price", model.Price);
                        cmd.Parameters.AddWithValue("@TotalPages", model.TotalPages);
                        cmd.Parameters.AddWithValue("@RackNo", model.RackNo ?? "");
                        cmd.Parameters.AddWithValue("@BookCoverImage", coverPath);
                        cmd.Parameters.AddWithValue("@IsEBook", model.IsEBook);
                        cmd.Parameters.AddWithValue("@EBookFile", ebookPath);
                        cmd.Parameters.AddWithValue("@BookDescription", model.BookDescription ?? "");
                        cmd.Parameters.AddWithValue("@MinimumStock", model.MinimumStock);
                        cmd.Parameters.AddWithValue("@IsActive", model.IsActive);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new
                {
                    Status = true,
                    Message = "Book Master saved successfully."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost]
        public JsonResult DeleteBookMaster(int id)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    string query = @"DELETE FROM BookMaster WHERE BookID = @BookID";

                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@BookID", id);

                    cmd.ExecuteNonQuery();
                }

                return Json(new { Status = true, Message = "Book deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetCourseDropdownList()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();
                string query = @"SELECT CourseID, CourseTitle FROM CourseMaster WHERE IsActive = 1 ORDER BY CourseTitle";

                SqlCommand cmd = new SqlCommand(query, con);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new
                    {
                        CourseID = Convert.ToInt32(dr["CourseID"]),
                        CourseTitle = dr["CourseTitle"].ToString()
                    });
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }
        #endregion

        #endregion
        #region FunctionEventTypeMaster
        public ActionResult FunctionEventType()
        {
            ViewData["ParentMenu"] = "Event";
            ViewData["PageTitle"] = "FunctionEventTypeMaster";
            return View();
        }

        [HttpGet]
        public JsonResult GetFunctionEventType()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();
                string query = @"SELECT
                            EventTypeID,
                            EventTypeName, IsActive, Description, 
                            CONVERT(varchar(10),CreatedDateTime,103) AS CreatedDateTime, 
                            CreatedByUserID
                         FROM FunctionEventTypeMaster
                         ORDER BY EventTypeID DESC";

                SqlCommand cmd = new SqlCommand(query, con);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new
                    {
                        EventTypeID = Convert.ToInt32(dr["EventTypeID"]),
                        EventTypeName = dr["EventTypeName"].ToString(),
                        Description = dr["Description"].ToString(),
                        IsActive = Convert.ToInt32(dr["IsActive"].ToString()),
                        CreatedDateTime = dr["CreatedDateTime"].ToString(),
                        CreatedByUserID = Convert.ToInt32(dr["CreatedByUserID"])
                    });
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult SaveFunctionEventType(FunctionEventTypeMaster model)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                // Duplicate Check
                SqlCommand checkCmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM FunctionEventTypeMaster
            WHERE EventTypeName = @EventTypeName
              AND EventTypeID <> @EventTypeID", con);

                checkCmd.Parameters.AddWithValue("@EventTypeName", model.EventTypeName);
                checkCmd.Parameters.AddWithValue("@EventTypeID", model.EventTypeID);

                int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Event Type is already exists."
                    });
                }

                if (model.EventTypeID == 0)
                {
                    string query = @"INSERT INTO FunctionEventTypeMaster
                            (
                                EventTypeName,
                                Description,
                                IsActive,
                                CreatedDateTime,
                                CreatedByUserID
                            )
                            VALUES
                            (
                                @EventTypeName,
                                @Description,
                                @IsActive,
                                GETDATE(),
                                @UserID
                            )";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@EventTypeName", model.EventTypeName);
                    cmd.Parameters.AddWithValue("@Description", (object)model.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    cmd.Parameters.AddWithValue("@UserID", 1);

                    cmd.ExecuteNonQuery();
                }
                else
                {
                    string query = @"UPDATE FunctionEventTypeMaster
                             SET EventTypeName = @EventTypeName, Description = @Description, IsActive = @IsActive, CreatedDateTime= GETDATE(), CreatedByUserID = @CreatedByUserID
                             WHERE EventTypeID = @EventTypeID";

                    SqlCommand cmd = new SqlCommand(query, con);

                    cmd.Parameters.AddWithValue("@EventTypeID", model.EventTypeID);
                    cmd.Parameters.AddWithValue("@EventTypeName", model.EventTypeName);
                    cmd.Parameters.AddWithValue("@Description", model.Description);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    cmd.Parameters.AddWithValue("@CreatedByUserID", model.CreatedByUserID);
                    cmd.ExecuteNonQuery();
                }
            }

            return Json(new
            {
                Status = true,
                Message = "Event Category saved successfully."
            });
        }
        [HttpGet]
        public JsonResult GetFunctionEventTypeByID(int id)
        {
            FunctionEventTypeMaster model = new FunctionEventTypeMaster();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"SELECT *
                         FROM FunctionEventTypeMaster
                         WHERE EventTypeID=@EventTypeID";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@EventTypeID", id);

                SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    model.EventTypeID = Convert.ToInt32(dr["EventTypeID"]);
                    model.EventTypeName = dr["EventTypeName"].ToString();
                    model.Description = dr["Description"].ToString();
                    model.IsActive = Convert.ToBoolean(dr["IsActive"]);
                    model.CreatedDateTime = Convert.ToDateTime(dr["CreatedDateTime"].ToString());
                    model.CreatedDateTimes = Convert.ToDateTime(dr["CreatedDateTime"].ToString()).ToString("dd-MM-yyyy hh:mm tt");
                    model.CreatedByUserID = Convert.ToInt32(dr["CreatedByUserID"]);
                }
            }

            return Json(model, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult DeleteEventType(int id)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                string query = @"DELETE FROM FunctionEventTypeMaster
                         WHERE EventTypeID=@EventTypeID";

                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@EventTypeID", id);

                cmd.ExecuteNonQuery();
            }

            return Json("Success");
        }
        #endregion
        #region Function Event Detail
        //public ActionResult FunctionEventDetail()
        //{
        //    ViewData["ParentMenu"] = "Event";
        //    ViewData["PageTitle"] = "Function Event Detail";
        //    return View();
        //}
        //     [HttpGet]
        //     public JsonResult GetFunctionEventList(int page = 1, int pageSize = 10, string search = "")
        //     {
        //         List<FunctionEventMaster> list = new List<FunctionEventMaster>();
        //         int totalRecords = 0;

        //         using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
        //         {
        //             con.Open();

        //             // Total Records
        //             string countQuery = @"
        //         SELECT COUNT(*)  FROM FunctionEventMaster WHERE IsActive = 1
        //         AND (EventTitle LIKE @Search  OR EventCode LIKE @Search  OR EventVenue LIKE @Search )";
        //             SqlCommand countCmd = new SqlCommand(countQuery, con);
        //             countCmd.Parameters.AddWithValue("@Search", "%" + search + "%");
        //             totalRecords = Convert.ToInt32(countCmd.ExecuteScalar());
        //             // Data Query
        //             string query = @"
        //         SELECT FEM.*, BM.BranchName, FETM.EventTypeName
        //         FROM FunctionEventMaster FEM
        //LEFT JOIN BranchMaster BM ON FEM.BranchID = BM.BranchID
        //LEFT JOIN FunctionEventTypeMaster FETM ON FEM.EventTypeID = FETM.EventTypeID
        //         WHERE FEM.IsActive = 1
        //         AND (
        //            EventTitle LIKE @Search  
        //             OR EventCode LIKE @Search 
        //             OR EventVenue LIKE @Search 
        //         )
        //         ORDER BY FEM.FunctionEventID DESC
        //         OFFSET @Offset ROWS
        //         FETCH NEXT @PageSize ROWS ONLY";

        //             SqlCommand cmd = new SqlCommand(query, con);
        //             cmd.Parameters.AddWithValue("@Search", "%" + search + "%");
        //             cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
        //             cmd.Parameters.AddWithValue("@PageSize", pageSize);

        //             SqlDataReader dr = cmd.ExecuteReader();

        //             while (dr.Read())
        //             {
        //                 list.Add(new FunctionEventMaster
        //                 {
        //                     FunctionEventID = Convert.ToInt32(dr["FunctionEventID"]),
        //                     BranchID = Convert.ToInt32(dr["BranchID"]),
        //                     EventCode = dr["EventCode"].ToString(),
        //                     BranchName = dr["BranchName"].ToString(),
        //                     EventTitle = dr["EventTitle"].ToString(),
        //                     EventDates = Convert.ToDateTime(dr["EventDate"].ToString()).ToString("dd-MM-yyyy"),
        //                     EventStartTimes = Convert.ToDateTime(dr["EventStartTime"].ToString()).ToString("h:mm tt"),
        //                     EventEndTimes = Convert.ToDateTime(dr["EventEndTime"].ToString()).ToString("h:mm tt"),
        //                     EventDate = dr["EventDate"] == DBNull.Value
        //                             ? (DateTime?)null
        //                             : Convert.ToDateTime(dr["EventDate"]),
        //                     EventStartTime = dr["EventStartTime"] == DBNull.Value
        //                     ? (TimeSpan?)null
        //                     : (TimeSpan)dr["EventStartTime"],

        //                     EventEndTime = dr["EventEndTime"] == DBNull.Value
        //                     ? (TimeSpan?)null
        //                     : (TimeSpan)dr["EventEndTime"],
        //                     EventDescription = dr["EventDescription"].ToString(),
        //                     EventTypeID = Convert.ToInt32(dr["EventTypeID"]),
        //                     EventTypeName = dr["EventTypeName"].ToString(),
        //                     EventthumbnailImage = dr["EventthumbnailImage"].ToString(),
        //                     EventVenue = dr["EventVenue"].ToString(),
        //                     IsPublished = Convert.ToBoolean(dr["IsPublished"]),
        //                     IsActive = Convert.ToBoolean(dr["IsActive"]),
        //                     EditEncryptedID = Encrypt(dr["FunctionEventID"].ToString() + "|E"),
        //                     ViewEncryptedID = Encrypt(dr["FunctionEventID"].ToString() + "|V")
        //                 });

        //             }

        //             dr.Close();
        //         }

        //         return Json(new
        //         {
        //             data = list,
        //             totalRecords = totalRecords
        //         }, JsonRequestBehavior.AllowGet);
        //     }
        [HttpGet]
        public JsonResult GetMasterFunctionEventList(int FunctionEventID = 0)
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();

                string query = @"
                           select FEM.FunctionEventID, FEM.EventTitle 
                            from FunctionEventMaster FEM
                            LEFT JOIN FunctionEventTypeMaster FETM ON FEM.EventTypeID = FETM.EventTypeID
                            Where FEM.IsActive = 1
                            AND FEM.FunctionEventID <> @FunctionEventID
                            order by FEM.EventTitle;";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@FunctionEventID", FunctionEventID);

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    list.Add(new
                    {
                        FunctionEventID = Convert.ToInt32(dr["FunctionEventID"]),
                        EventTitle = dr["EventTitle"].ToString()
                    });
                }
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public JsonResult GetFunctionEventMaster()
        {
            try
            {
                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    string query = @"select FEM.*, FETM.EventTypeName, BM.BranchName
                                    from FunctionEventMaster FEM
                                    LEFT JOIN FunctionEventTypeMaster FETM ON FEM.EventTypeID = FETM.EventTypeID
                                    LEFT JOIN BranchMaster BM ON FEM.BranchID = BM.BranchID
                                    Where FEM.IsActive = 1
                                    AND FEM.FunctionEventID <> FunctionEventID
                                    order by FEM.FunctionEventID Desc";

                    SqlCommand cmd = new SqlCommand(query, con);

                    SqlDataReader dr = cmd.ExecuteReader();

                    while (dr.Read())
                    {
                        list.Add(new
                        {
                            FunctionEventID = Convert.ToInt32(dr["FunctionEventID"]),
                            BranchID = Convert.ToInt32(dr["BranchID"]),
                            BranchName = dr["BranchName"].ToString(),
                            EventCode = dr["EventCode"].ToString(),
                            EventTitle = dr["EventTitle"].ToString(),
                            EventDate = dr["EventDate"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(dr["EventDate"]),
                            EventStartTime = dr["EventStartTime"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(dr["EventStartTime"]),
                            EventEndTime = dr["EventEndTime"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(dr["EventEndTime"]),

                            EventVenue = dr["EventVenue"].ToString(),
                            EventDescription = dr["EventDescription"].ToString(),
                            EventTypeID = Convert.ToInt32(dr["EventTypeID"].ToString()),
                            EventthumbnailImage = dr["EventthumbnailImage"].ToString(),
                            IsPublished = Convert.ToBoolean(dr["IsPublished"]),
                            IsActive = Convert.ToBoolean(dr["IsActive"]),
                            CreatedDateTime = dr["CreatedDateTime"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(dr["CreatedDateTime"])
                        });
                    }
                }

                return Json(list, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = 0,
                    Message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        #endregion
        #region CreateFunctionEvent
        //public ActionResult CreateFunctionEvent(string id = "", string mode = "A")
        //{
        //    int FunctionEventID = 0;
        //    string currentMode = mode;

        //    if (!string.IsNullOrEmpty(id))
        //    {
        //        try
        //        {
        //            string decryptedValue = Decrypt(id);
        //            if (!string.IsNullOrEmpty(decryptedValue))
        //            {
        //                string[] parts = decryptedValue.Split('|');
        //                FunctionEventID = Convert.ToInt32(parts[0]);
        //                if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
        //                {
        //                    currentMode = parts[1];
        //                }
        //            }
        //        }
        //        catch
        //        {
        //            FunctionEventID = 0;
        //            currentMode = "A";
        //        }
        //    }

        //    ViewBag.EncryptedID = id ?? "";
        //    ViewBag.FunctionEventID = FunctionEventID;
        //    ViewBag.Mode = currentMode;

        //    // Fetch User ID safely from Cookie or Session
        //    int loggedInUserID = 0;
        //    if (Request.Cookies["UserID"] != null && !string.IsNullOrEmpty(Request.Cookies["UserID"].Value))
        //    {
        //        int.TryParse(Request.Cookies["UserID"].Value, out loggedInUserID);
        //    }
        //    ViewBag.LoggedInUserID = loggedInUserID;

        //    ViewData["ParentMenu"] = "Event";
        //    ViewData["SubMenu"] = "Function Event";
        //    ViewData["SubMenuUrl"] = Url.Action("FunctionEventDetail", "bctechadmin");

        //    switch (currentMode)
        //    {
        //        case "E":
        //            ViewData["PageTitle"] = "Edit Function Enquiry";
        //            break;
        //        case "V":
        //            ViewData["PageTitle"] = "View Function Enquiry";
        //            break;
        //        default:
        //            ViewData["PageTitle"] = "Create Function Enquiry";
        //            break;
        //    }

        //    return View("~/Views/bctechadmin/CreateFunctionEvent.cshtml");
        //}
        [HttpPost]
        public JsonResult GetNextEventCode(int branchID, int orgID = 0)
        {
            // Fallback to session/cookie OrganizationID if not explicitly passed
            if (orgID == 0)
            {
                orgID = GetOrganizationID();
            }

            string enquiryNo = "";
            using (SqlConnection conn = new SqlConnection(CS))
            {
                conn.Open();
                string branchCode = "ENQ";

                // 1. Fetch Branch Code
                string codeSql = "SELECT ISNULL(BranchCode, 'ENQ') FROM BranchMaster WHERE BranchID = @BranchID;";
                using (SqlCommand cmdCode = new SqlCommand(codeSql, conn))
                {
                    cmdCode.Parameters.AddWithValue("@BranchID", branchID);
                    object res = cmdCode.ExecuteScalar();
                    if (res != null)
                    {
                        branchCode = res.ToString().Trim();
                    }
                }

                // 2. Format Next Serial No (BranchCode + SerialNo)
                string sql = @"
            SELECT @BranchCode + '-' + RIGHT('0' + CAST(ISNULL(MAX(FunctionEventID), 0) + 1 AS VARCHAR(10)), 5) 
            FROM FunctionEventMaster 
            ;";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@BranchCode", branchCode);
                    //cmd.Parameters.AddWithValue("@OrganizationID", orgID);
                    object scalarResult = cmd.ExecuteScalar();
                    enquiryNo = scalarResult != null ? scalarResult.ToString() : branchCode + "-00001";
                }
            }

            // Return as an object so res.EnquiryNo works properly in JavaScript
            return Json(new { EnquiryNo = enquiryNo }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult GetEventTypeDropdown(int orgID = 0)
        {
            List<DropdownItem> list = new List<DropdownItem>();

            using (SqlConnection conn = new SqlConnection(CS))
            {
                string sql = @"
                    SELECT
                        EventTypeID,
                        LTRIM(RTRIM(
                            ISNULL(EventTypeName,'')        
                        )) AS EventTypeName
                    FROM FunctionEventTypeMaster
                    WHERE IsActive = 1 order by EventTypeName";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();

                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string name = dr["EventTypeName"].ToString();

                            list.Add(new DropdownItem
                            {
                                ID = Convert.ToInt32(dr["EventTypeID"]),
                                Name = string.IsNullOrWhiteSpace(name)
                                        ? "User #" + dr["EventTypeID"].ToString()
                                        : name
                            });
                        }
                    }
                }
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }
        #endregion
        #region Function Event Actions

        public ActionResult FunctionEventDetail()
        {
            ViewData["ParentMenu"] = "Event";
            ViewData["PageTitle"] = "Function Event Detail";
            return View();
        }

        [HttpGet]
        public JsonResult GetFunctionEventList(int page = 1, int pageSize = 10, string search = "")
        {
            List<FunctionEventMaster> list = new List<FunctionEventMaster>();
            int totalRecords = 0;

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                // Total Records Count
                string countQuery = @"
            SELECT COUNT(*) FROM FunctionEventMaster WHERE IsActive = 1 
            AND (EventTitle LIKE @Search OR EventCode LIKE @Search OR EventVenue LIKE @Search)";

                using (SqlCommand countCmd = new SqlCommand(countQuery, con))
                {
                    countCmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                    totalRecords = Convert.ToInt32(countCmd.ExecuteScalar());
                }

                // Data Query with Pagination
                string query = @"
            SELECT FEM.*, BM.BranchName, FETM.EventTypeName
            FROM FunctionEventMaster FEM
            LEFT JOIN BranchMaster BM ON FEM.BranchID = BM.BranchID
            LEFT JOIN FunctionEventTypeMaster FETM ON FEM.EventTypeID = FETM.EventTypeID
            WHERE FEM.IsActive = 1 
            AND (EventTitle LIKE @Search OR EventCode LIKE @Search OR EventVenue LIKE @Search)
            ORDER BY FEM.FunctionEventID DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            list.Add(new FunctionEventMaster
                            {
                                FunctionEventID = Convert.ToInt32(dr["FunctionEventID"]),
                                BranchID = Convert.ToInt32(dr["BranchID"]),
                                EventCode = dr["EventCode"].ToString(),
                                BranchName = dr["BranchName"].ToString(),
                                EventTitle = dr["EventTitle"].ToString(),
                                EventDates = dr["EventDate"] == DBNull.Value ? "" : Convert.ToDateTime(dr["EventDate"]).ToString("dd-MM-yyyy"),
                                EventStartTimes = dr["EventStartTime"] == DBNull.Value ? "" : DateTime.Today.Add((TimeSpan)dr["EventStartTime"]).ToString("h:mm tt"),
                                EventEndTimes = dr["EventEndTime"] == DBNull.Value ? "" : DateTime.Today.Add((TimeSpan)dr["EventEndTime"]).ToString("h:mm tt"),
                                EventDescription = dr["EventDescription"].ToString(),
                                EventTypeID = Convert.ToInt32(dr["EventTypeID"]),
                                EventTypeName = dr["EventTypeName"].ToString(),
                                EventthumbnailImage = dr["EventthumbnailImage"].ToString(),
                                EventVenue = dr["EventVenue"].ToString(),
                                IsPublished = Convert.ToBoolean(dr["IsPublished"]),
                                IsActive = Convert.ToBoolean(dr["IsActive"]),
                                EditEncryptedID = Encrypt(dr["FunctionEventID"].ToString() + "|E"),
                                ViewEncryptedID = Encrypt(dr["FunctionEventID"].ToString() + "|V")
                            });
                        }
                    }
                }
            }

            return Json(new { data = list, totalRecords = totalRecords }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult CreateFunctionEvent(string id = "", string mode = "A")
        {
            int functionEventID = 0;
            string currentMode = mode;

            if (!string.IsNullOrEmpty(id))
            {
                try
                {
                    string decryptedValue = Decrypt(id);
                    if (!string.IsNullOrEmpty(decryptedValue))
                    {
                        string[] parts = decryptedValue.Split('|');
                        functionEventID = Convert.ToInt32(parts[0]);
                        if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                        {
                            currentMode = parts[1];
                        }
                    }
                }
                catch
                {
                    functionEventID = 0;
                    currentMode = "A";
                }
            }

            ViewBag.FunctionEventID = functionEventID;
            ViewBag.Mode = currentMode;

            int loggedInUserID = 0;
            if (Request.Cookies["UserID"] != null && !string.IsNullOrEmpty(Request.Cookies["UserID"].Value))
            {
                int.TryParse(Request.Cookies["UserID"].Value, out loggedInUserID);
            }
            ViewBag.LoggedInUserID = loggedInUserID;

            ViewData["ParentMenu"] = "Event";
            ViewData["SubMenu"] = "Function Event";
            ViewData["SubMenuUrl"] = Url.Action("FunctionEventDetail", "bctechadmin");

            switch (currentMode)
            {
                case "E":
                    ViewData["PageTitle"] = "Edit Function Event";
                    break;
                case "V":
                    ViewData["PageTitle"] = "View Function Event";
                    break;
                default:
                    ViewData["PageTitle"] = "Create Function Event";
                    break;
            }

            return View();
        }

        [HttpPost]
        public JsonResult DeleteFunctionEvent(int CourseID) // Matches the parameter sent by AJAX
        {
            try
            {
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();
                    string query = "UPDATE FunctionEventMaster SET IsActive = 0 WHERE FunctionEventID = @FunctionEventID";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@FunctionEventID", CourseID);
                        int rowsAffected = cmd.ExecuteNonQuery(); // Fixed: capture return value directly
                    }
                }
                return Json(new { Status = 1, Message = "Function event deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { Status = 0, Message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateInput(false)]
        public JsonResult SaveFunctionEvent()
        {
            try
            {
                int loggedInUserID = 0;
                if (Request.Cookies["UserID"] != null && !string.IsNullOrEmpty(Request.Cookies["UserID"].Value))
                {
                    int.TryParse(Request.Cookies["UserID"].Value, out loggedInUserID);
                }

                int branchID = Convert.ToInt32(Request.Form["BranchID"]);
                string eventCode = Request.Form["EventCode"];
                int eventTypeID = Convert.ToInt32(Request.Form["EventTypeID"]);
                string eventTitle = Request.Form["EventTitle"];
                DateTime? eventDate = string.IsNullOrEmpty(Request.Form["EventDate"]) ? (DateTime?)null : Convert.ToDateTime(Request.Form["EventDate"]);
                TimeSpan? eventStartTime = string.IsNullOrEmpty(Request.Form["EventStartTime"]) ? (TimeSpan?)null : TimeSpan.Parse(Request.Form["EventStartTime"]);
                TimeSpan? eventEndTime = string.IsNullOrEmpty(Request.Form["EventEndTime"]) ? (TimeSpan?)null : TimeSpan.Parse(Request.Form["EventEndTime"]);
                string eventVenue = Request.Form["EventVenue"];
                string eventDescription = Request.Form["EventDescription"];
                bool isPublished = Convert.ToBoolean(Request.Form["IsPublished"]);
                bool isActive = Convert.ToBoolean(Request.Form["IsActive"]);

                string thumbnailPath = "";
                List<string> uploadedGalleryPaths = new List<string>();

                if (Request.Files.Count > 0)
                {
                    string uploadFolder = Server.MapPath("~/Content/Upload/EventImage/");
                    if (!Directory.Exists(uploadFolder)) { Directory.CreateDirectory(uploadFolder); }

                    for (int i = 0; i < Request.Files.Count; i++)
                    {
                        HttpPostedFileBase file = Request.Files[i];
                        if (file != null && file.ContentLength > 0)
                        {
                            string fileName = "Event_" + Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                            string fullPath = Path.Combine(uploadFolder, fileName);
                            file.SaveAs(fullPath);

                            string relativePath = "/Content/Upload/EventImage/" + fileName;
                            if (i == 0) { thumbnailPath = relativePath; }
                            uploadedGalleryPaths.Add(relativePath);
                        }
                    }
                }

                int newEventID = 0;
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    string masterQuery = @"
                INSERT INTO FunctionEventMaster 
                (BranchID, EventCode, EventTitle, EventDate, EventStartTime, EventEndTime, EventVenue, EventDescription, EventTypeID, EventthumbnailImage, IsPublished, IsActive, CreatedDateTime, CreatedByUserID) 
                VALUES 
                (@BranchID, @EventCode, @EventTitle, @EventDate, @EventStartTime, @EventEndTime, @EventVenue, @EventDescription, @EventTypeID, @EventthumbnailImage, @IsPublished, @IsActive, GETDATE(), @CreatedByUserID);
                SELECT SCOPE_IDENTITY();";

                    using (SqlCommand cmd = new SqlCommand(masterQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@BranchID", branchID);
                        cmd.Parameters.AddWithValue("@EventCode", string.IsNullOrEmpty(eventCode) ? (object)DBNull.Value : eventCode);
                        cmd.Parameters.AddWithValue("@EventTitle", eventTitle ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EventDate", eventDate.HasValue ? (object)eventDate.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EventStartTime", eventStartTime.HasValue ? (object)eventStartTime.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EventEndTime", eventEndTime.HasValue ? (object)eventEndTime.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EventVenue", eventVenue ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EventDescription", eventDescription ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EventTypeID", eventTypeID);
                        cmd.Parameters.AddWithValue("@EventthumbnailImage", string.IsNullOrEmpty(thumbnailPath) ? (object)DBNull.Value : thumbnailPath);
                        cmd.Parameters.AddWithValue("@IsPublished", isPublished ? 1 : 0);
                        cmd.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
                        cmd.Parameters.AddWithValue("@CreatedByUserID", loggedInUserID);

                        newEventID = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    string[] titles = Request.Form.GetValues("ImageTitles");
                    string[] descs = Request.Form.GetValues("ImageDescriptions");

                    for (int i = 0; i < uploadedGalleryPaths.Count; i++)
                    {
                        string imgPath = uploadedGalleryPaths[i];
                        string imgTitle = (titles != null && titles.Length > i) ? titles[i] : "";
                        string imgDesc = (descs != null && descs.Length > i) ? descs[i] : "";
                        int displayOrder = i + 1; // Tracks visual order sequence

                        string galleryQuery = @"
                    INSERT INTO FunctionEventGallery (FunctionEventID, GalleryImage, ImageTitle, ImageDescription, DisplayOrderNo, IsActive, CreatedDateTime, CreatedByUserID)
                    VALUES (@FunctionEventID, @GalleryImage, @ImageTitle, @ImageDescription, @DisplayOrderNo, 1, GETDATE(), @CreatedByUserID)";

                        using (SqlCommand gCmd = new SqlCommand(galleryQuery, con))
                        {
                            gCmd.Parameters.AddWithValue("@FunctionEventID", newEventID);
                            gCmd.Parameters.AddWithValue("@GalleryImage", imgPath);
                            gCmd.Parameters.AddWithValue("@ImageTitle", string.IsNullOrEmpty(imgTitle) ? (object)DBNull.Value : imgTitle);
                            gCmd.Parameters.AddWithValue("@ImageDescription", string.IsNullOrEmpty(imgDesc) ? (object)DBNull.Value : imgDesc);
                            gCmd.Parameters.AddWithValue("@DisplayOrderNo", displayOrder);
                            gCmd.Parameters.AddWithValue("@CreatedByUserID", loggedInUserID);
                            gCmd.ExecuteNonQuery();
                        }
                    }
                }

                return Json(new { IsSuccess = true, Message = "Function Event saved successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateInput(false)]
        public JsonResult UpdateFunctionEvent()
        {
            try
            {
                int loggedInUserID = 0;
                if (Request.Cookies["UserID"] != null && !string.IsNullOrEmpty(Request.Cookies["UserID"].Value))
                {
                    int.TryParse(Request.Cookies["UserID"].Value, out loggedInUserID);
                }

                int functionEventID = Convert.ToInt32(Request.Form["FunctionEventID"]);
                int branchID = Convert.ToInt32(Request.Form["BranchID"]);
                string eventCode = Request.Form["EventCode"];
                int eventTypeID = Convert.ToInt32(Request.Form["EventTypeID"]);
                string eventTitle = Request.Form["EventTitle"];
                DateTime? eventDate = string.IsNullOrEmpty(Request.Form["EventDate"]) ? (DateTime?)null : Convert.ToDateTime(Request.Form["EventDate"]);
                TimeSpan? eventStartTime = string.IsNullOrEmpty(Request.Form["EventStartTime"]) ? (TimeSpan?)null : TimeSpan.Parse(Request.Form["EventStartTime"]);
                TimeSpan? eventEndTime = string.IsNullOrEmpty(Request.Form["EventEndTime"]) ? (TimeSpan?)null : TimeSpan.Parse(Request.Form["EventEndTime"]);
                string eventVenue = Request.Form["EventVenue"];
                string eventDescription = Request.Form["EventDescription"];
                bool isPublished = Convert.ToBoolean(Request.Form["IsPublished"]);
                bool isActive = Convert.ToBoolean(Request.Form["IsActive"]);

                List<string> uploadedGalleryPaths = new List<string>();
                bool hasNewFiles = Request.Files.Count > 0 && Request.Files[0].ContentLength > 0;

                string uploadFolder = Server.MapPath("~/Content/Upload/EventImage/");
                if (!Directory.Exists(uploadFolder)) { Directory.CreateDirectory(uploadFolder); }

                if (hasNewFiles)
                {
                    for (int i = 0; i < Request.Files.Count; i++)
                    {
                        HttpPostedFileBase file = Request.Files[i];
                        if (file != null && file.ContentLength > 0)
                        {
                            string fileName = "Event_" + Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                            string fullPath = Path.Combine(uploadFolder, fileName);
                            file.SaveAs(fullPath);

                            string relativePath = "/Content/Upload/EventImage/" + fileName;
                            uploadedGalleryPaths.Add(relativePath);
                        }
                    }
                }

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    // Fetch old images for physical file cleanup later
                    List<string> oldGalleryImages = new List<string>();
                    string selectOldQuery = "SELECT GalleryImage FROM FunctionEventGallery WHERE FunctionEventID = @FunctionEventID";
                    using (SqlCommand sCmd = new SqlCommand(selectOldQuery, con))
                    {
                        sCmd.Parameters.AddWithValue("@FunctionEventID", functionEventID);
                        using (SqlDataReader reader = sCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader["GalleryImage"] != DBNull.Value)
                                {
                                    oldGalleryImages.Add(reader["GalleryImage"].ToString());
                                }
                            }
                        }
                    }

                    string[] existingPaths = Request.Form.GetValues("ExistingGalleryPaths");
                    string[] titles = Request.Form.GetValues("ImageTitles");
                    string[] descs = Request.Form.GetValues("ImageDescriptions");

                    // Determine Thumbnail based on the first item in the reordered table list
                    string firstRowPath = "";
                    int newFileCursor = 0;
                    if (existingPaths != null && existingPaths.Length > 0 && existingPaths[0] != "__NEW_FILE__")
                    {
                        firstRowPath = existingPaths[0];
                    }
                    else if (uploadedGalleryPaths.Count > 0)
                    {
                        firstRowPath = uploadedGalleryPaths[0];
                    }

                    // Update Master Table
                    string updateQuery = @"
                        UPDATE FunctionEventMaster 
                        SET 
                            BranchID = @BranchID,
                            EventCode = @EventCode,
                            EventTitle = @EventTitle, 
                            EventDate = @EventDate, 
                            EventStartTime = @EventStartTime, 
                            EventEndTime = @EventEndTime, 
                            EventVenue = @EventVenue, 
                            EventDescription = @EventDescription, 
                            EventTypeID = @EventTypeID, 
                            EventthumbnailImage = CASE WHEN @NewThumbnail = '' THEN EventthumbnailImage ELSE @NewThumbnail END, 
                            IsPublished = @IsPublished, 
                            IsActive = @IsActive
                        WHERE FunctionEventID = @FunctionEventID";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@FunctionEventID", functionEventID);
                        cmd.Parameters.AddWithValue("@BranchID", branchID);
                        cmd.Parameters.AddWithValue("@EventCode", string.IsNullOrEmpty(eventCode) ? (object)DBNull.Value : eventCode);
                        cmd.Parameters.AddWithValue("@EventTitle", eventTitle ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EventDate", eventDate.HasValue ? (object)eventDate.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EventStartTime", eventStartTime.HasValue ? (object)eventStartTime.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EventEndTime", eventEndTime.HasValue ? (object)eventEndTime.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EventVenue", eventVenue ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EventDescription", eventDescription ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EventTypeID", eventTypeID);
                        cmd.Parameters.AddWithValue("@NewThumbnail", string.IsNullOrEmpty(firstRowPath) ? "" : firstRowPath);
                        cmd.Parameters.AddWithValue("@IsPublished", isPublished ? 1 : 0);
                        cmd.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }

                    // Clear old gallery records
                    string deleteGallery = "DELETE FROM FunctionEventGallery WHERE FunctionEventID = @FunctionEventID";
                    using (SqlCommand dCmd = new SqlCommand(deleteGallery, con))
                    {
                        dCmd.Parameters.AddWithValue("@FunctionEventID", functionEventID);
                        dCmd.ExecuteNonQuery();
                    }

                    int totalItemsCount = (titles != null) ? titles.Length : 0;
                    List<string> activeImagePaths = new List<string>();

                    for (int i = 0; i < totalItemsCount; i++)
                    {
                        string imgPath = "";
                        if (existingPaths != null && i < existingPaths.Length && existingPaths[i] != "__NEW_FILE__")
                        {
                            imgPath = existingPaths[i];
                        }
                        else if (newFileCursor < uploadedGalleryPaths.Count)
                        {
                            imgPath = uploadedGalleryPaths[newFileCursor];
                            newFileCursor++;
                        }

                        if (!string.IsNullOrEmpty(imgPath))
                        {
                            activeImagePaths.Add(imgPath);
                            string imgTitle = (titles != null && titles.Length > i) ? titles[i] : "";
                            string imgDesc = (descs != null && descs.Length > i) ? descs[i] : "";
                            int displayOrder = i + 1;

                            string galleryQuery = @"
                                INSERT INTO FunctionEventGallery (FunctionEventID, GalleryImage, ImageTitle, ImageDescription, DisplayOrderNo, IsActive, CreatedDateTime, CreatedByUserID)
                                VALUES (@FunctionEventID, @GalleryImage, @ImageTitle, @ImageDescription, @DisplayOrderNo, 1, GETDATE(), @CreatedByUserID)";

                            using (SqlCommand gCmd = new SqlCommand(galleryQuery, con))
                            {
                                gCmd.Parameters.AddWithValue("@FunctionEventID", functionEventID);
                                gCmd.Parameters.AddWithValue("@GalleryImage", imgPath);
                                gCmd.Parameters.AddWithValue("@ImageTitle", string.IsNullOrEmpty(imgTitle) ? (object)DBNull.Value : imgTitle);
                                gCmd.Parameters.AddWithValue("@ImageDescription", string.IsNullOrEmpty(imgDesc) ? (object)DBNull.Value : imgDesc);
                                gCmd.Parameters.AddWithValue("@DisplayOrderNo", displayOrder);
                                gCmd.Parameters.AddWithValue("@CreatedByUserID", loggedInUserID);
                                gCmd.ExecuteNonQuery();
                            }
                        }
                    }

                    // Physical file cleanup for removed images
                    foreach (var oldImg in oldGalleryImages)
                    {
                        if (!activeImagePaths.Contains(oldImg))
                        {
                            string physicalPath = Server.MapPath("~" + oldImg);
                            if (System.IO.File.Exists(physicalPath))
                            {
                                System.IO.File.Delete(physicalPath);
                            }
                        }
                    }
                }

                return Json(new { IsSuccess = true, Message = "Function Event updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { IsSuccess = false, Message = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetFunctionEventByID(string id)
        {
            try
            {
                int functionEventID = 0;
                if (!string.IsNullOrEmpty(id))
                {
                    string decryptedValue = Decrypt(id);
                    if (!string.IsNullOrEmpty(decryptedValue))
                    {
                        string[] parts = decryptedValue.Split('|');
                        functionEventID = Convert.ToInt32(parts[0]);
                    }
                }

                if (functionEventID == 0) { return Json(null, JsonRequestBehavior.AllowGet); }

                Dictionary<string, object> eventData = new Dictionary<string, object>();
                List<object> galleryList = new List<object>();

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
                {
                    con.Open();

                    string masterQuery = "SELECT * FROM FunctionEventMaster WHERE FunctionEventID = @FunctionEventID";
                    using (SqlCommand cmd = new SqlCommand(masterQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@FunctionEventID", functionEventID);
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                eventData["FunctionEventID"] = Convert.ToInt32(dr["FunctionEventID"]);
                                eventData["BranchID"] = Convert.ToInt32(dr["BranchID"]);
                                eventData["EventCode"] = dr["EventCode"]?.ToString();
                                eventData["EventTitle"] = dr["EventTitle"]?.ToString();
                                eventData["EventDate"] = dr["EventDate"] == DBNull.Value ? null : Convert.ToDateTime(dr["EventDate"]).ToString("yyyy-MM-dd");
                                eventData["EventStartTime"] = dr["EventStartTime"] == DBNull.Value ? null : dr["EventStartTime"].ToString();
                                eventData["EventEndTime"] = dr["EventEndTime"] == DBNull.Value ? null : dr["EventEndTime"].ToString();
                                eventData["EventVenue"] = dr["EventVenue"]?.ToString();
                                eventData["EventDescription"] = dr["EventDescription"]?.ToString();
                                eventData["EventTypeID"] = Convert.ToInt32(dr["EventTypeID"]);
                                eventData["EventthumbnailImage"] = dr["EventthumbnailImage"]?.ToString();
                                eventData["IsPublished"] = Convert.ToBoolean(dr["IsPublished"]);
                                eventData["IsActive"] = Convert.ToBoolean(dr["IsActive"]);
                            }
                        }
                    }

                    // Fetch gallery sorted by DisplayOrderNo to retain the custom sequence set by the user
                    string galleryQuery = "SELECT GalleryImage, ImageTitle, ImageDescription FROM FunctionEventGallery WHERE FunctionEventID = @FunctionEventID AND IsActive = 1 ORDER BY DisplayOrderNo ASC";
                    using (SqlCommand gCmd = new SqlCommand(galleryQuery, con))
                    {
                        gCmd.Parameters.AddWithValue("@FunctionEventID", functionEventID);
                        using (SqlDataReader gDr = gCmd.ExecuteReader())
                        {
                            while (gDr.Read())
                            {
                                galleryList.Add(new
                                {
                                    GalleryImage = gDr["GalleryImage"]?.ToString(),
                                    ImageTitle = gDr["ImageTitle"]?.ToString(),
                                    ImageDescription = gDr["ImageDescription"]?.ToString()
                                });
                            }
                        }
                    }
                }

                eventData["GalleryList"] = galleryList;
                return Json(eventData, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = 0, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion
        #region Public Branch-Wise Student Enquiry
        [AllowAnonymous]
        public ActionResult BranchWiseEnquiry(string id = "", int branchId = 0, string b = "")
        {
            // Redirect direct /bctechadmin/BranchWiseEnquiry requests to clean URI /StudentEnquiry/BranchWise
            if (Request != null && Request.RawUrl != null && Request.RawUrl.StartsWith("/bctechadmin/BranchWiseEnquiry", System.StringComparison.OrdinalIgnoreCase))
            {
                string queryString = Request.Url != null ? Request.Url.Query : "";
                return RedirectPermanent("/StudentEnquiry/BranchWise" + queryString);
            }

            int targetBranchID = branchId;
            string valueToProcess = !string.IsNullOrEmpty(b) ? b : id;

            if (targetBranchID <= 0 && !string.IsNullOrEmpty(valueToProcess))
            {
                valueToProcess = HttpUtility.UrlDecode(valueToProcess);

                if (!int.TryParse(valueToProcess, out targetBranchID))
                {
                    string decrypted = Decrypt(valueToProcess);
                    if (!string.IsNullOrEmpty(decrypted) && decrypted.Contains("|"))
                    {
                        decrypted = decrypted.Split('|')[0];
                    }
                    int.TryParse(decrypted, out targetBranchID);
                }
            }

            ViewBag.BranchID = targetBranchID;
            return View("~/Views/bctechadmin/BranchWiseEnquiry.cshtml");
        }

        [HttpGet]
        [AllowAnonymous]
        public JsonResult GetPublicOrganization()
        {
            try
            {
                using (SqlConnection con = new SqlConnection(CS))
                {
                    SqlCommand cmd = new SqlCommand("SELECT TOP 1 OrganizationID, OrganizationName, OrganizationLogo FROM OrganizationMaster", con);
                    con.Open();
                    SqlDataReader dr = cmd.ExecuteReader();
                    if (dr.Read())
                    {
                        return Json(new
                        {
                            Status = true,
                            Data = new
                            {
                                OrganizationID = Convert.ToInt32(dr["OrganizationID"]),
                                OrganizationName = dr["OrganizationName"] != DBNull.Value ? dr["OrganizationName"].ToString() : "B.C.Tech Computer Education",
                                OrganizationLogo = dr["OrganizationLogo"] != DBNull.Value ? dr["OrganizationLogo"].ToString() : ""
                            }
                        }, JsonRequestBehavior.AllowGet);
                    }
                }
                return Json(new { Status = false }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public JsonResult GetPublicBranchList()
        {
            List<object> list = new List<object>();
            try
            {
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = @"SELECT BM.BranchID, BM.OrganizationID, ISNULL(BM.BranchCode, '') AS BranchCode, 
                                            ISNULL(BM.BranchName, '') AS BranchName, ISNULL(BM.Address, '') AS Address, 
                                            ISNULL(BM.City, '') AS City, ISNULL(BM.State, '') AS State, 
                                            ISNULL(BM.MobileNo, '') AS MobileNo, ISNULL(BM.EmailAddress, '') AS EmailAddress,
                                            ISNULL(OM.OrganizationName, 'B.C.Tech Computer Education') AS OrganizationName,
                                            ISNULL(OM.OrganizationLogo, '') AS OrganizationLogo
                                     FROM BranchMaster BM
                                     LEFT JOIN OrganizationMaster OM ON BM.OrganizationID = OM.OrganizationID
                                     WHERE BM.IsActive = 1 
                                     ORDER BY BM.BranchName";
                    SqlCommand cmd = new SqlCommand(query, con);
                    SqlDataReader dr = cmd.ExecuteReader();
                    while (dr.Read())
                    {
                        list.Add(new
                        {
                            BranchID = Convert.ToInt32(dr["BranchID"]),
                            OrganizationID = Convert.ToInt32(dr["OrganizationID"]),
                            BranchCode = dr["BranchCode"].ToString(),
                            BranchName = dr["BranchName"].ToString(),
                            Address = dr["Address"].ToString(),
                            City = dr["City"].ToString(),
                            State = dr["State"].ToString(),
                            MobileNo = dr["MobileNo"].ToString(),
                            EmailAddress = dr["EmailAddress"].ToString(),
                            OrganizationName = dr["OrganizationName"].ToString(),
                            OrganizationLogo = dr["OrganizationLogo"].ToString()
                        });
                    }
                }
                return Json(new { Status = true, Data = list }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public JsonResult GetPublicBranchDetails(int branchId)
        {
            try
            {
                object result = null;
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = @"SELECT BM.BranchID, BM.OrganizationID, ISNULL(BM.BranchCode, '') AS BranchCode, 
                                            ISNULL(BM.BranchName, '') AS BranchName, ISNULL(BM.Address, '') AS Address, 
                                            ISNULL(BM.City, '') AS City, ISNULL(BM.State, '') AS State, 
                                            ISNULL(BM.MobileNo, '') AS MobileNo, ISNULL(BM.EmailAddress, '') AS EmailAddress,
                                            ISNULL(OM.OrganizationName, 'B.C.Tech Computer Education') AS OrganizationName,
                                            ISNULL(OM.OrganizationLogo, '') AS OrganizationLogo
                                     FROM BranchMaster BM
                                     LEFT JOIN OrganizationMaster OM ON BM.OrganizationID = OM.OrganizationID
                                     WHERE BM.BranchID = @BranchID";
                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@BranchID", branchId);
                    SqlDataReader dr = cmd.ExecuteReader();
                    if (dr.Read())
                    {
                        result = new
                        {
                            BranchID = Convert.ToInt32(dr["BranchID"]),
                            OrganizationID = Convert.ToInt32(dr["OrganizationID"]),
                            BranchCode = dr["BranchCode"].ToString(),
                            BranchName = dr["BranchName"].ToString(),
                            Address = dr["Address"].ToString(),
                            City = dr["City"].ToString(),
                            State = dr["State"].ToString(),
                            MobileNo = dr["MobileNo"].ToString(),
                            EmailAddress = dr["EmailAddress"].ToString(),
                            OrganizationName = dr["OrganizationName"].ToString(),
                            OrganizationLogo = dr["OrganizationLogo"].ToString()
                        };
                    }
                }
                return Json(new { Status = true, Data = result }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public JsonResult GetPublicCourseList()
        {
            List<object> list = new List<object>();
            try
            {
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = @"SELECT CourseID, CourseCode, CourseTitle, Duration, DurationType, CourseFee 
                                     FROM CourseMaster 
                                     WHERE IsActive = 1 
                                     ORDER BY CourseTitle";
                    SqlCommand cmd = new SqlCommand(query, con);
                    SqlDataReader dr = cmd.ExecuteReader();
                    while (dr.Read())
                    {
                        list.Add(new
                        {
                            CourseID = Convert.ToInt32(dr["CourseID"]),
                            CourseCode = dr["CourseCode"].ToString(),
                            CourseTitle = dr["CourseTitle"].ToString(),
                            Duration = dr["Duration"] == DBNull.Value ? 0 : Convert.ToInt32(dr["Duration"]),
                            DurationType = dr["DurationType"].ToString(),
                            CourseFee = dr["CourseFee"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["CourseFee"])
                        });
                    }
                }
                return Json(new { Status = true, Data = list }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public JsonResult GetPublicSourceList()
        {
            List<object> list = new List<object>();
            try
            {
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = @"SELECT EnquirySourceID, EnquirySourceType FROM EnquirySourceMaster ORDER BY EnquirySourceType";
                    SqlCommand cmd = new SqlCommand(query, con);
                    SqlDataReader dr = cmd.ExecuteReader();
                    while (dr.Read())
                    {
                        list.Add(new
                        {
                            EnquirySourceID = Convert.ToInt32(dr["EnquirySourceID"]),
                            SourceTitle = dr["SourceTitle"].ToString()
                        });
                    }
                }
            }
            catch (Exception ex)
            {

            }
            return Json(new { Status = true, Data = list }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [AllowAnonymous]
        public JsonResult SavePublicStudentEnquiry(StudentEnquiryModel model)
        {
            ProcessResult result = new ProcessResult();

            if (model.BranchID <= 0)
            {
                result.IsSuccess = false;
                result.Message = "Please select a Branch.";
                return Json(result);
            }

            if (string.IsNullOrEmpty(model.StudentName))
            {
                result.IsSuccess = false;
                result.Message = "Please enter Student Name.";
                return Json(result);
            }

            if (string.IsNullOrEmpty(model.StudentMobileNo))
            {
                result.IsSuccess = false;
                result.Message = "Please enter Mobile Number.";
                return Json(result);
            }

            model.CreatedByUserID = 1; // Default Public Submission User ID

            using (SqlConnection conn = new SqlConnection(CS))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    object ToDbDateTime(string dateStr)
                    {
                        if (DateTime.TryParse(dateStr, out DateTime dt))
                            return dt;
                        return DBNull.Value;
                    }

                    // 1. Fetch Branch Code & Organization ID
                    string branchCode = "ENQ";
                    int orgID = 1;
                    string getBranchSql = @"SELECT ISNULL(BranchCode, 'ENQ') AS BranchCode, ISNULL(OrganizationID, 1) AS OrganizationID 
                                            FROM BranchMaster WHERE BranchID = @BranchID;";
                    using (SqlCommand cmdBranch = new SqlCommand(getBranchSql, conn, transaction))
                    {
                        cmdBranch.Parameters.AddWithValue("@BranchID", model.BranchID);
                        using (SqlDataReader reader = cmdBranch.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                branchCode = reader["BranchCode"] != DBNull.Value ? reader["BranchCode"].ToString().Trim() : "ENQ";
                                orgID = reader["OrganizationID"] != DBNull.Value ? Convert.ToInt32(reader["OrganizationID"]) : 1;
                            }
                        }
                    }
                    model.OrganizationID = orgID;

                    // 2. Generate Next Enquiry Number
                    string nextEnquiryNo = branchCode + "-00001";
                    string genNoSql = @"SELECT @BranchCode + '-' + RIGHT('0' + CAST(ISNULL(MAX(StudentEnquiryID), 0) + 1 AS VARCHAR(10)), 5) 
                                        FROM StudentEnquiryMaster WHERE OrganizationID = @OrganizationID;";
                    using (SqlCommand cmdNo = new SqlCommand(genNoSql, conn, transaction))
                    {
                        cmdNo.Parameters.AddWithValue("@BranchCode", branchCode);
                        cmdNo.Parameters.AddWithValue("@OrganizationID", orgID);
                        object scalarRes = cmdNo.ExecuteScalar();
                        if (scalarRes != null && !string.IsNullOrEmpty(scalarRes.ToString()))
                        {
                            nextEnquiryNo = scalarRes.ToString();
                        }
                    }
                    model.EnquiryNo = nextEnquiryNo;

                    // 3. Insert Master Record
                    string insertMasterSql = @"
                        INSERT INTO StudentEnquiryMaster
                        (
                            OrganizationID, BranchID, EnquiryNo, EnquiryDate, EnquiryTakenByUserID,
                            StudentName, StudentMobileNo, WhatsAppNo, StudentEmail, DOB, Gender, BloodGroup,
                            EducationQualification, SchoolCollegeName, FatherName, FatherMobileNo, FatherAlternateMobileNo,
                            FatherOccupation, FatherEmail, MotherName, MotherMobileNo, MotherOccupation, MotherEmail,
                            Address, AreaLocation, City, State, Pincode, EnquirySourceID, ReferencePersonName,
                            ExpectedJoiningDate, Remarks, IsFollowUpRequired, IsConvertedToAdmission, IsActive,
                            CreatedDateTime, CreatedByUserID
                        )
                        VALUES
                        (
                            @OrganizationID, @BranchID, @EnquiryNo, GETDATE(), 1,
                            @StudentName, @StudentMobileNo, @WhatsAppNo, @StudentEmail, @DOB, @Gender, @BloodGroup,
                            @EducationQualification, @SchoolCollegeName, @FatherName, @FatherMobileNo, @FatherAlternateMobileNo,
                            @FatherOccupation, @FatherEmail, @MotherName, @MotherMobileNo, @MotherOccupation, @MotherEmail,
                            @Address, @AreaLocation, @City, @State, @Pincode, @EnquirySourceID, @ReferencePersonName,
                            @ExpectedJoiningDate, @Remarks, 1, 0, 1,
                            GETDATE(), 1
                        );
                        SELECT SCOPE_IDENTITY();";

                    int newEnquiryID = 0;
                    using (SqlCommand cmdInsert = new SqlCommand(insertMasterSql, conn, transaction))
                    {
                        cmdInsert.Parameters.AddWithValue("@OrganizationID", model.OrganizationID);
                        cmdInsert.Parameters.AddWithValue("@BranchID", model.BranchID);
                        cmdInsert.Parameters.AddWithValue("@EnquiryNo", model.EnquiryNo);
                        cmdInsert.Parameters.AddWithValue("@StudentName", model.StudentName ?? "");
                        cmdInsert.Parameters.AddWithValue("@StudentMobileNo", model.StudentMobileNo ?? "");
                        cmdInsert.Parameters.AddWithValue("@WhatsAppNo", (object)model.WhatsAppNo ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@StudentEmail", (object)model.StudentEmail ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@DOB", ToDbDateTime(model.DOB));
                        cmdInsert.Parameters.AddWithValue("@Gender", (object)model.Gender ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@BloodGroup", (object)model.BloodGroup ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@EducationQualification", (object)model.EducationQualification ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@SchoolCollegeName", (object)model.SchoolCollegeName ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@FatherName", (object)model.FatherName ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@FatherMobileNo", (object)model.FatherMobileNo ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@FatherAlternateMobileNo", (object)model.FatherAlternateMobileNo ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@FatherOccupation", (object)model.FatherOccupation ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@FatherEmail", (object)model.FatherEmail ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@MotherName", (object)model.MotherName ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@MotherMobileNo", (object)model.MotherMobileNo ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@MotherOccupation", (object)model.MotherOccupation ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@MotherEmail", (object)model.MotherEmail ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@Address", (object)model.Address ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@AreaLocation", (object)model.AreaLocation ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@City", (object)model.City ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@State", (object)model.State ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@Pincode", (object)model.Pincode ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@EnquirySourceID", model.EnquirySourceID > 0 ? (object)model.EnquirySourceID : DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@ReferencePersonName", (object)model.ReferencePersonName ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@ExpectedJoiningDate", ToDbDateTime(model.ExpectedJoiningDate));
                        cmdInsert.Parameters.AddWithValue("@Remarks", (object)model.Remarks ?? DBNull.Value);

                        object insertedIdObj = cmdInsert.ExecuteScalar();
                        newEnquiryID = Convert.ToInt32(insertedIdObj);
                    }

                    // 4. Insert Selected Courses
                    if (model.CourseList != null && model.CourseList.Count > 0)
                    {
                        string insertCourseSql = @"
                            INSERT INTO StudentEnquiryCourse
                            (
                                StudentEnquiryID, CourseID, CourseFee, Discount, FinalFee, Remarks, CreatedDateTime, CreatedByUserID
                            )
                            VALUES
                            (
                                @StudentEnquiryID, @CourseID, @CourseFee, @Discount, @FinalFee, @Remarks, GETDATE(), 1
                            );";

                        foreach (var course in model.CourseList)
                        {
                            if (course.CourseID > 0)
                            {
                                using (SqlCommand cmdCourse = new SqlCommand(insertCourseSql, conn, transaction))
                                {
                                    cmdCourse.Parameters.AddWithValue("@StudentEnquiryID", newEnquiryID);
                                    cmdCourse.Parameters.AddWithValue("@CourseID", course.CourseID);
                                    cmdCourse.Parameters.AddWithValue("@CourseFee", course.CourseFee);
                                    cmdCourse.Parameters.AddWithValue("@Discount", course.Discount);
                                    cmdCourse.Parameters.AddWithValue("@FinalFee", course.FinalFee);
                                    cmdCourse.Parameters.AddWithValue("@Remarks", (object)course.Remarks ?? DBNull.Value);
                                    cmdCourse.ExecuteNonQuery();
                                }
                            }
                        }
                    }

                    transaction.Commit();
                    result.IsSuccess = true;
                    result.PrimaryID = newEnquiryID;
                    result.Message = nextEnquiryNo; // Pass Enquiry No back in Message
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
            }

            return Json(result);
        }
        #endregion
        #region Student Enquiry Follow Up Page & APIs
        public ActionResult StudentEnquiryFollowUp()
        {
            return View("~/Views/bctechadmin/StudentEnquiryFollowUp.cshtml");
        }

        [HttpGet]
        public JsonResult GetStudentEnquiryFollowUpData(string tab = "today", string fromDate = "", string toDate = "", string search = "", int pageSize = 25)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    // 1. Get Tab Counts
                    string countsSql = @"
                        SELECT
                            SUM(CASE WHEN ISNULL(se.IsConvertedToAdmission, 0) = 0 AND ISNULL(se.IsActive, 1) = 1 AND ISNULL(se.IsFollowUpRequired, 1) = 1 AND CONVERT(DATE, ISNULL(fu.NextFollowUpDate, se.EnquiryDate)) = CONVERT(DATE, GETDATE()) THEN 1 ELSE 0 END) AS TodayCount,
                            SUM(CASE WHEN ISNULL(se.IsConvertedToAdmission, 0) = 0 AND ISNULL(se.IsActive, 1) = 1 AND ISNULL(se.IsFollowUpRequired, 1) = 1 AND CONVERT(DATE, ISNULL(fu.NextFollowUpDate, se.EnquiryDate)) < CONVERT(DATE, GETDATE()) THEN 1 ELSE 0 END) AS PendingCount,
                            SUM(CASE WHEN ISNULL(se.IsConvertedToAdmission, 0) = 0 AND ISNULL(se.IsActive, 1) = 1 AND ISNULL(se.IsFollowUpRequired, 1) = 1 AND CONVERT(DATE, ISNULL(fu.NextFollowUpDate, se.EnquiryDate)) > CONVERT(DATE, GETDATE()) THEN 1 ELSE 0 END) AS UpcomingCount,
                            SUM(CASE WHEN ISNULL(se.IsConvertedToAdmission, 0) = 0 AND ISNULL(se.IsActive, 1) = 1 AND ISNULL(se.IsFollowUpRequired, 1) = 0 THEN 1 ELSE 0 END) AS ClosedCount
                        FROM StudentEnquiryMaster se
                        OUTER APPLY (
                            SELECT TOP 1 NextFollowUpDate
                            FROM StudentEnquiryFollowUp
                            WHERE StudentEnquiryID = se.StudentEnquiryID
                            ORDER BY StudentEnquiryFollowUpID DESC
                        ) fu;";

                    int countToday = 0, countPending = 0, countUpcoming = 0, countClosed = 0;
                    using (SqlCommand cmdCount = new SqlCommand(countsSql, con))
                    {
                        using (SqlDataReader drC = cmdCount.ExecuteReader())
                        {
                            if (drC.Read())
                            {
                                countToday = drC["TodayCount"] != DBNull.Value ? Convert.ToInt32(drC["TodayCount"]) : 0;
                                countPending = drC["PendingCount"] != DBNull.Value ? Convert.ToInt32(drC["PendingCount"]) : 0;
                                countUpcoming = drC["UpcomingCount"] != DBNull.Value ? Convert.ToInt32(drC["UpcomingCount"]) : 0;
                                countClosed = drC["ClosedCount"] != DBNull.Value ? Convert.ToInt32(drC["ClosedCount"]) : 0;
                            }
                        }
                    }

                    // 2. Build Query Filters based on Tab
                    string tabFilter = "";
                    tab = (tab ?? "today").ToLower();

                    if (tab == "today")
                    {
                        tabFilter = " AND ISNULL(se.IsFollowUpRequired, 1) = 1 AND CONVERT(DATE, ISNULL(fu.NextFollowUpDate, se.EnquiryDate)) = CONVERT(DATE, GETDATE()) ";
                    }
                    else if (tab == "pending")
                    {
                        tabFilter = " AND ISNULL(se.IsFollowUpRequired, 1) = 1 AND CONVERT(DATE, ISNULL(fu.NextFollowUpDate, se.EnquiryDate)) < CONVERT(DATE, GETDATE()) ";
                    }
                    else if (tab == "upcoming")
                    {
                        tabFilter = " AND ISNULL(se.IsFollowUpRequired, 1) = 1 AND CONVERT(DATE, ISNULL(fu.NextFollowUpDate, se.EnquiryDate)) > CONVERT(DATE, GETDATE()) ";
                    }
                    else if (tab == "donotfollowup" || tab == "closed")
                    {
                        tabFilter = " AND ISNULL(se.IsFollowUpRequired, 1) = 0 ";
                    }

                    string dateFilter = "";
                    if (!string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate))
                    {
                        dateFilter = " AND CONVERT(DATE, se.EnquiryDate) BETWEEN @FromDate AND @ToDate ";
                    }

                    string searchFilter = "";
                    if (!string.IsNullOrEmpty(search))
                    {
                        searchFilter = " AND (se.StudentName LIKE @Search OR se.StudentMobileNo LIKE @Search OR se.EnquiryNo LIKE @Search OR se.FatherMobileNo LIKE @Search OR se.MotherMobileNo LIKE @Search) ";
                    }

                    string dataQuery = $@"
                        SELECT TOP ({pageSize})
                            se.StudentEnquiryID,
                            se.EnquiryNo,
                            CONVERT(VARCHAR(11), se.EnquiryDate, 106) AS EnquiryDate,
                            se.StudentName,
                            se.StudentMobileNo,
                            se.FatherMobileNo,
                            se.MotherMobileNo,
                            ISNULL(se.AreaLocation, ISNULL(se.City, '')) AS AreaLocation,
                            ISNULL(es.EnquirySourceType, '') AS SourceName,
                            CONVERT(VARCHAR(11), ISNULL(fu.NextFollowUpDate, se.EnquiryDate), 106) AS DueDate,
                            ISNULL(fu.FollowUpType, 'Call') AS FollowUpType,
                            ISNULL(fu.Discussion, '') AS Discussion,
                            ISNULL(fu.NextActionRemark, '') AS NextActionRemark,
                            ISNULL(u.Username, 'Admin') AS FollowUpByUserName,
                            CASE WHEN ISNULL(se.IsFollowUpRequired, 1) = 0 THEN 1 ELSE 0 END AS IsClosed
                        FROM StudentEnquiryMaster se
                        LEFT JOIN EnquirySourceMaster es ON se.EnquirySourceID = es.EnquirySourceID
                        OUTER APPLY (
                            SELECT TOP 1 NextFollowUpDate, FollowUpType, Discussion, NextActionRemark, FollowUpByUserID
                            FROM StudentEnquiryFollowUp
                            WHERE StudentEnquiryID = se.StudentEnquiryID
                            ORDER BY StudentEnquiryFollowUpID DESC
                        ) fu
                        LEFT JOIN UserMaster u ON fu.FollowUpByUserID = u.UserID
                        WHERE ISNULL(se.IsConvertedToAdmission, 0) = 0 AND ISNULL(se.IsActive, 1) = 1
                        {tabFilter} {dateFilter} {searchFilter}
                        ORDER BY ISNULL(fu.NextFollowUpDate, se.EnquiryDate) ASC, se.StudentEnquiryID DESC";

                    List<object> list = new List<object>();
                    using (SqlCommand cmdData = new SqlCommand(dataQuery, con))
                    {
                        if (!string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate))
                        {
                            cmdData.Parameters.AddWithValue("@FromDate", fromDate);
                            cmdData.Parameters.AddWithValue("@ToDate", toDate);
                        }
                        if (!string.IsNullOrEmpty(search))
                        {
                            cmdData.Parameters.AddWithValue("@Search", "%" + search + "%");
                        }

                        using (SqlDataReader drD = cmdData.ExecuteReader())
                        {
                            while (drD.Read())
                            {
                                int enquiryId = Convert.ToInt32(drD["StudentEnquiryID"]);
                                list.Add(new
                                {
                                    StudentEnquiryID = enquiryId,
                                    EncryptedID = Encrypt(enquiryId.ToString()),
                                    ViewEncryptedID = Encrypt(enquiryId + "|V"),
                                    EditEncryptedID = Encrypt(enquiryId + "|E"),
                                    EnquiryNo = drD["EnquiryNo"]?.ToString() ?? "",
                                    EnquiryDate = drD["EnquiryDate"]?.ToString() ?? "",
                                    DueDate = drD["DueDate"]?.ToString() ?? "",
                                    StudentName = drD["StudentName"]?.ToString() ?? "",
                                    StudentMobileNo = drD["StudentMobileNo"]?.ToString() ?? "",
                                    FatherMobileNo = drD["FatherMobileNo"]?.ToString() ?? "",
                                    MotherMobileNo = drD["MotherMobileNo"]?.ToString() ?? "",
                                    AreaLocation = drD["AreaLocation"]?.ToString() ?? "",
                                    SourceName = drD["SourceName"]?.ToString() ?? "",
                                    FollowUpType = drD["FollowUpType"]?.ToString() ?? "",
                                    LeadStage = drD["FollowUpType"]?.ToString() ?? "Pending",
                                    Discussion = drD["Discussion"]?.ToString() ?? "",
                                    LastRemark = !string.IsNullOrEmpty(drD["NextActionRemark"]?.ToString()) ? drD["NextActionRemark"].ToString() : drD["Discussion"].ToString(),
                                    FollowUpByUserName = drD["FollowUpByUserName"]?.ToString() ?? "",
                                    IsClosed = Convert.ToBoolean(drD["IsClosed"])
                                });
                            }
                        }
                    }

                    return Json(new
                    {
                        Status = true,
                        Counts = new
                        {
                            Today = countToday,
                            Pending = countPending,
                            Upcoming = countUpcoming,
                            Closed = countClosed
                        },
                        Data = list
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult SaveStudentEnquiryFollowUpEntry(StudentEnquiryFollowUpModel model)
        {
            ProcessResult result = new ProcessResult();
            try
            {
                if (model.StudentEnquiryID <= 0)
                {
                    result.IsSuccess = false;
                    result.Message = "Invalid Student Enquiry ID.";
                    return Json(result);
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    // Ensure table
                    string createTableSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StudentEnquiryFollowUp')
                        BEGIN
                            CREATE TABLE StudentEnquiryFollowUp (
                                StudentEnquiryFollowUpID INT IDENTITY(1,1) PRIMARY KEY,
                                StudentEnquiryID INT NOT NULL,
                                FollowUpDate DATETIME NULL,
                                NextFollowUpDate DATETIME NULL,
                                FollowUpType VARCHAR(50) NULL,
                                Discussion NVARCHAR(MAX) NULL,
                                NextActionRemark NVARCHAR(MAX) NULL,
                                FollowUpByUserID INT NULL,
                                IsClosed BIT DEFAULT 0,
                                CreatedDateTime DATETIME DEFAULT GETDATE(),
                                CreatedByUserID INT NULL
                            );
                        END;";
                    using (SqlCommand cmdInit = new SqlCommand(createTableSql, con))
                    {
                        cmdInit.ExecuteNonQuery();
                    }

                    // Check if IsClosed column exists, if not add it
                    string alterColSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StudentEnquiryFollowUp') AND name = 'IsClosed')
                        BEGIN
                            ALTER TABLE StudentEnquiryFollowUp ADD IsClosed BIT DEFAULT 0;
                        END;";
                    using (SqlCommand cmdAlter = new SqlCommand(alterColSql, con))
                    {
                        cmdAlter.ExecuteNonQuery();
                    }

                    DateTime? dtFollowUp = null;
                    if (DateTime.TryParse(model.FollowUpDate, out DateTime parsedFU))
                        dtFollowUp = parsedFU;

                    DateTime? dtNextFollowUp = null;
                    if (DateTime.TryParse(model.NextFollowUpDate, out DateTime parsedNext))
                        dtNextFollowUp = parsedNext;

                    bool isClosed = false;
                    var isClosedProp = model.GetType().GetProperty("IsClosed");
                    if (isClosedProp != null)
                    {
                        isClosed = Convert.ToBoolean(isClosedProp.GetValue(model));
                    }

                    string insertSql = @"
                        INSERT INTO StudentEnquiryFollowUp
                        (
                            StudentEnquiryID, FollowUpDate, NextFollowUpDate, FollowUpType,
                            Discussion, NextActionRemark, FollowUpByUserID, IsClosed, CreatedDateTime, CreatedByUserID
                        )
                        VALUES
                        (
                            @StudentEnquiryID, @FollowUpDate, @NextFollowUpDate, @FollowUpType,
                            @Discussion, @NextActionRemark, @FollowUpByUserID, @IsClosed, GETDATE(), 1
                        );";

                    using (SqlCommand cmdInsert = new SqlCommand(insertSql, con))
                    {
                        cmdInsert.Parameters.AddWithValue("@StudentEnquiryID", model.StudentEnquiryID);
                        cmdInsert.Parameters.AddWithValue("@FollowUpDate", (object)dtFollowUp ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@NextFollowUpDate", (object)dtNextFollowUp ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@FollowUpType", (object)model.FollowUpType ?? "Call");
                        cmdInsert.Parameters.AddWithValue("@Discussion", (object)model.Discussion ?? "");
                        cmdInsert.Parameters.AddWithValue("@NextActionRemark", (object)model.NextActionRemark ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@FollowUpByUserID", model.FollowUpByUserID > 0 ? (object)model.FollowUpByUserID : DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@IsClosed", isClosed);
                        cmdInsert.ExecuteNonQuery();
                    }

                    // Also update IsFollowUpRequired flag in StudentEnquiryMaster
                    string updateMasterSql = @"UPDATE StudentEnquiryMaster SET IsFollowUpRequired = @IsFollowUpRequired WHERE StudentEnquiryID = @StudentEnquiryID;";
                    using (SqlCommand cmdUpdate = new SqlCommand(updateMasterSql, con))
                    {
                        cmdUpdate.Parameters.AddWithValue("@IsFollowUpRequired", isClosed ? 0 : 1);
                        cmdUpdate.Parameters.AddWithValue("@StudentEnquiryID", model.StudentEnquiryID);
                        cmdUpdate.ExecuteNonQuery();
                    }

                    result.IsSuccess = true;
                    result.Message = "Follow Up recorded successfully.";
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            return Json(result);
        }
        #endregion
        #region QuestionMCQ

        public ActionResult CoursequestionView()
        {
            ViewBag.Title = "Question Bank";
            ViewData["PageTitle"] = "Question Bank";
            ViewData["ParentMenu"] = "Student Exam";
            return View();
        }

        public ActionResult QuestionBank()
        {
            return RedirectToAction("CoursequestionView");
        }

        public ActionResult ScheduleExam()
        {
            if (Request.Cookies["UserID"] == null)
            {
                return RedirectToAction("Index", "bctechadmin");
            }
            ViewBag.Title = "Schedule Exam";
            ViewData["PageTitle"] = "Schedule Exam";
            ViewData["ParentMenu"] = "Student Exam";
            return View();
        }

        public ActionResult ScheduleExamAdd(int? id, string mode)
        {
            if (Request.Cookies["UserID"] == null)
            {
                return RedirectToAction("Index", "bctechadmin");
            }
            ViewBag.Title = "Schedule Exam Add";
            ViewData["PageTitle"] = "Schedule Exam Add";
            ViewData["ParentMenu"] = "Student Exam";
            ViewBag.ScheduleExamId = id ?? 0;
            ViewBag.Mode = (mode ?? "").ToLower();
            return View();
        }

        [HttpGet]
        public JsonResult GetScheduleExamList(string search = "", string SearchText = "", int PageNumber = 1, int PageSize = 10)
        {
            List<object> list = new List<object>();
            try
            {
                string term = !string.IsNullOrWhiteSpace(SearchText) ? SearchText.Trim() : (search ?? "").Trim();
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = @"
                        SELECT 
                            e.ScheduleExamId,
                            e.dateandtime,
                            e.course_id,
                            ISNULL(c.CourseTitle, '') AS CourseTitle,
                            e.ExamScheduleDate,
                            e.NoOfQuestion,
                            e.ExamTime,
                            e.ExamTotalMarks,
                            e.PracticalExamTime,
                            e.TheoryExamTime,
                            e.SecureCode,
                            e.BranchID
                        FROM ScheduleExamMaster e
                        LEFT JOIN CourseMaster c ON e.course_id = c.CourseID
                        WHERE (@Search = '' OR c.CourseTitle LIKE '%' + @Search + '%' OR CAST(e.SecureCode AS NVARCHAR(50)) LIKE '%' + @Search + '%')
                        ORDER BY e.ScheduleExamId DESC";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@Search", term);
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                string examDate = "";
                                if (dr["ExamScheduleDate"] != DBNull.Value)
                                {
                                    if (DateTime.TryParse(dr["ExamScheduleDate"].ToString(), out DateTime dt))
                                    {
                                        examDate = dt.ToString("yyyy-MM-dd");
                                    }
                                    else
                                    {
                                        examDate = dr["ExamScheduleDate"].ToString();
                                    }
                                }

                                string examTime = dr["ExamTime"] != DBNull.Value ? dr["ExamTime"].ToString().Trim() : "";
                                if (!string.IsNullOrEmpty(examTime) && !examTime.ToLower().Contains("min"))
                                {
                                    examTime += " Minutes";
                                }

                                string totalMarks = dr["ExamTotalMarks"] != DBNull.Value ? dr["ExamTotalMarks"].ToString().Trim() : "";
                                if (!string.IsNullOrEmpty(totalMarks) && !totalMarks.ToLower().Contains("mark"))
                                {
                                    totalMarks += " Marks";
                                }

                                list.Add(new
                                {
                                    ScheduleExamId = Convert.ToInt32(dr["ScheduleExamId"]),
                                    course_id = dr["course_id"] != DBNull.Value ? Convert.ToInt32(dr["course_id"]) : 0,
                                    CourseTitle = dr["CourseTitle"].ToString(),
                                    ExamScheduleDate = examDate,
                                    NoOfQuestion = dr["NoOfQuestion"] != DBNull.Value ? dr["NoOfQuestion"].ToString() : "",
                                    ExamTime = examTime,
                                    ExamTotalMarks = totalMarks,
                                    PracticalExamTime = dr["PracticalExamTime"] != DBNull.Value ? dr["PracticalExamTime"].ToString() : "",
                                    TheoryExamTime = dr["TheoryExamTime"] != DBNull.Value ? dr["TheoryExamTime"].ToString() : "",
                                    SecureCode = dr["SecureCode"] != DBNull.Value ? dr["SecureCode"].ToString() : "",
                                    BranchID = dr["BranchID"] != DBNull.Value ? Convert.ToInt32(dr["BranchID"]) : 1
                                });
                            }
                        }
                    }
                }

                int totalRecords = list.Count;
                if (PageSize > 0)
                {
                    int skip = (Math.Max(1, PageNumber) - 1) * PageSize;
                    var pagedList = list.Skip(skip).Take(PageSize).ToList();
                    return Json(new { Status = true, Data = pagedList, TotalRecords = totalRecords }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { Status = true, Data = list, TotalRecords = totalRecords }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message, Data = list, TotalRecords = 0 }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetScheduleExamById(int id)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = @"
                        SELECT 
                            e.ScheduleExamId,
                            e.dateandtime,
                            e.course_id,
                            ISNULL(c.CourseTitle, '') AS CourseTitle,
                            e.ExamScheduleDate,
                            e.NoOfQuestion,
                            e.ExamTime,
                            e.ExamTotalMarks,
                            e.PracticalExamTime,
                            e.TheoryExamTime,
                            e.SecureCode,
                            e.BranchID,
                            ISNULL(e.ExamType, '') AS ExamType
                        FROM ScheduleExamMaster e
                        LEFT JOIN CourseMaster c ON e.course_id = c.CourseID
                        WHERE e.ScheduleExamId = @Id order by ScheduleExamId desc";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                string examDate = "";
                                if (dr["ExamScheduleDate"] != DBNull.Value)
                                {
                                    if (DateTime.TryParse(dr["ExamScheduleDate"].ToString(), out DateTime dt))
                                    {
                                        examDate = dt.ToString("yyyy-MM-dd");
                                    }
                                    else
                                    {
                                        examDate = dr["ExamScheduleDate"].ToString();
                                    }
                                }

                                string rawTime = dr["ExamTime"] != DBNull.Value ? dr["ExamTime"].ToString().Trim() : "";
                                string cleanMinutes = Regex.Replace(rawTime, @"[^\d]", "");
                                if (string.IsNullOrEmpty(cleanMinutes)) cleanMinutes = rawTime;

                                string rawMarks = dr["ExamTotalMarks"] != DBNull.Value ? dr["ExamTotalMarks"].ToString().Trim() : "";
                                string cleanMarks = Regex.Replace(rawMarks, @"[^\d]", "");
                                if (string.IsNullOrEmpty(cleanMarks)) cleanMarks = rawMarks;

                                var obj = new
                                {
                                    ScheduleExamId = Convert.ToInt32(dr["ScheduleExamId"]),
                                    course_id = dr["course_id"] != DBNull.Value ? Convert.ToInt32(dr["course_id"]) : 0,
                                    CourseTitle = dr["CourseTitle"].ToString(),
                                    ExamScheduleDate = examDate,
                                    NoOfQuestion = dr["NoOfQuestion"] != DBNull.Value ? dr["NoOfQuestion"].ToString() : "",
                                    ExamTime = rawTime,
                                    ExamMinutes = cleanMinutes,
                                    ExamTotalMarks = cleanMarks,
                                    PracticalExamTime = dr["PracticalExamTime"] != DBNull.Value ? dr["PracticalExamTime"].ToString() : "",
                                    TheoryExamTime = dr["TheoryExamTime"] != DBNull.Value ? dr["TheoryExamTime"].ToString() : "",
                                    SecureCode = dr["SecureCode"] != DBNull.Value ? dr["SecureCode"].ToString() : "",
                                    BranchID = dr["BranchID"] != DBNull.Value ? Convert.ToInt32(dr["BranchID"]) : 1,
                                    ExamType = dr["ExamType"] != DBNull.Value ? dr["ExamType"].ToString() : ""
                                };
                                return Json(new { Status = true, Data = obj }, JsonRequestBehavior.AllowGet);
                            }
                        }
                    }
                }
                return Json(new { Status = false, Message = "Schedule Exam record not found." }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult SaveScheduleExam(ScheduleExamMasterModel model)
        {
            try
            {
                if (model == null)
                {
                    return Json(new { Status = false, Message = "Invalid data submitted." });
                }

                if (model.course_id <= 0)
                {
                    return Json(new { Status = false, Message = "Please select a Course." });
                }

                if (string.IsNullOrWhiteSpace(model.ExamScheduleDate))
                {
                    return Json(new { Status = false, Message = "Please select Exam Schedule Date." });
                }

                // Parse NoOfQuestion as integer
                string rawQDigits = Regex.Replace(model.NoOfQuestion ?? "", @"[^\d]", "");
                if (string.IsNullOrWhiteSpace(rawQDigits))
                {
                    return Json(new { Status = false, Message = "Please enter valid No Of Questions." });
                }
                int noOfQuestions = int.Parse(rawQDigits);

                // Parse ExamTime as integer (e.g. 60)
                string rawTimeDigits = Regex.Replace(model.ExamTime ?? "", @"[^\d]", "");
                if (string.IsNullOrWhiteSpace(rawTimeDigits))
                {
                    return Json(new { Status = false, Message = "Please enter valid Exam Minutes." });
                }
                int examMinutes = int.Parse(rawTimeDigits);

                // Enforce 2 digits only for Exam Total Marks (1 to 99)
                string rawMarksDigits = Regex.Replace(model.ExamTotalMarks ?? "", @"[^\d]", "");
                if (string.IsNullOrWhiteSpace(rawMarksDigits))
                {
                    return Json(new { Status = false, Message = "Please enter Exam Total Marks." });
                }
                if (rawMarksDigits.Length > 2)
                {
                    return Json(new { Status = false, Message = "Exam Total Marks allows only 2 digits (maximum 99)." });
                }
                int totalMarks = int.Parse(rawMarksDigits);
                if (totalMarks <= 0 || totalMarks > 99)
                {
                    return Json(new { Status = false, Message = "Exam Total Marks must be between 1 and 99 (2 digits only)." });
                }

                object secureCodeVal = int.TryParse((model.SecureCode ?? "").Trim(), out int sc) ? (object)sc : (object)(model.SecureCode ?? "");

                int branchId = 1;
                if (Session["BranchID"] != null)
                {
                    int.TryParse(Session["BranchID"].ToString(), out branchId);
                }
                else if (Request.Cookies["BranchID"] != null)
                {
                    int.TryParse(Request.Cookies["BranchID"].Value, out branchId);
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    if (model.ScheduleExamId == 0)
                    {
                        string insertSql = @"
                            INSERT INTO ScheduleExamMaster (
                                dateandtime,
                                course_id,
                                ExamScheduleDate,
                                NoOfQuestion,
                                ExamTime,
                                ExamTotalMarks,
                                PracticalExamTime,
                                TheoryExamTime,
                                SecureCode,
                                BranchID,
                                ExamType
                            ) VALUES (
                                GETDATE(),
                                @course_id,
                                @ExamScheduleDate,
                                @NoOfQuestion,
                                @ExamTime,
                                @ExamTotalMarks,
                                @PracticalExamTime,
                                @TheoryExamTime,
                                @SecureCode,
                                @BranchID,
                                @ExamType
                            );
                            SELECT SCOPE_IDENTITY();";

                        using (SqlCommand cmd = new SqlCommand(insertSql, con))
                        {
                            cmd.Parameters.AddWithValue("@course_id", model.course_id);
                            cmd.Parameters.AddWithValue("@ExamScheduleDate", (object)model.ExamScheduleDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@NoOfQuestion", noOfQuestions);
                            cmd.Parameters.AddWithValue("@ExamTime", examMinutes);
                            cmd.Parameters.AddWithValue("@ExamTotalMarks", totalMarks);
                            cmd.Parameters.AddWithValue("@PracticalExamTime", (object)model.PracticalExamTime ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@TheoryExamTime", (object)model.TheoryExamTime ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@SecureCode", secureCodeVal ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BranchID", branchId);
                            cmd.Parameters.AddWithValue("@ExamType", (object)model.ExamType ?? DBNull.Value);

                            object newId = cmd.ExecuteScalar();
                            return Json(new { Status = true, Message = "Schedule Exam saved successfully!", Id = newId });
                        }
                    }
                    else
                    {
                        string updateSql = @"
                            UPDATE ScheduleExamMaster SET
                                course_id = @course_id,
                                ExamScheduleDate = @ExamScheduleDate,
                                NoOfQuestion = @NoOfQuestion,
                                ExamTime = @ExamTime,
                                ExamTotalMarks = @ExamTotalMarks,
                                PracticalExamTime = @PracticalExamTime,
                                TheoryExamTime = @TheoryExamTime,
                                SecureCode = @SecureCode,
                                ExamType = @ExamType,
                                BranchID = ISNULL(@BranchID, BranchID)
                            WHERE ScheduleExamId = @ScheduleExamId;";

                        using (SqlCommand cmd = new SqlCommand(updateSql, con))
                        {
                            cmd.Parameters.AddWithValue("@ScheduleExamId", model.ScheduleExamId);
                            cmd.Parameters.AddWithValue("@course_id", model.course_id);
                            cmd.Parameters.AddWithValue("@ExamScheduleDate", (object)model.ExamScheduleDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@NoOfQuestion", noOfQuestions);
                            cmd.Parameters.AddWithValue("@ExamTime", examMinutes);
                            cmd.Parameters.AddWithValue("@ExamTotalMarks", totalMarks);
                            cmd.Parameters.AddWithValue("@PracticalExamTime", (object)model.PracticalExamTime ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@TheoryExamTime", (object)model.TheoryExamTime ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@SecureCode", secureCodeVal ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BranchID", branchId);
                            cmd.Parameters.AddWithValue("@ExamType", (object)model.ExamType ?? DBNull.Value);

                            int rows = cmd.ExecuteNonQuery();
                            if (rows > 0)
                            {
                                return Json(new { Status = true, Message = "Schedule Exam updated successfully!", Id = model.ScheduleExamId });
                            }
                            else
                            {
                                return Json(new { Status = false, Message = "No record found to update." });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeleteScheduleExam(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return Json(new { Status = false, Message = "Invalid ID." });
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string sql = "DELETE FROM ScheduleExamMaster WHERE ScheduleExamId = @Id";
                    using (SqlCommand cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            return Json(new { Status = true, Message = "Schedule Exam deleted successfully!" });
                        }
                        else
                        {
                            return Json(new { Status = false, Message = "Schedule Exam not found." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message });
            }
        }




        [HttpGet]
        public JsonResult GetCourseGridList()
        {
            List<object> list = new List<object>();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();
                string query = @"SELECT * FROM CourseMaster WHERE IsActive = 1 ORDER BY CourseTitle";

                SqlCommand cmd = new SqlCommand(query, con);
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new
                    {
                        CourseID = Convert.ToInt32(dr["CourseID"]),
                        CourseCode = dr["CourseCode"].ToString(),
                        CourseURL = dr["CourseURL"].ToString(),
                        CourseImage = dr["CourseImage"].ToString(),
                        CourseTitle = dr["CourseTitle"].ToString()
                    });
                }
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        #region MCQ Questions CRUD

        private static bool HasMCQOptionText(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return false;
            if (html.IndexOf("<img", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            string text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
            text = text.Replace("&nbsp;", string.Empty).Replace("&#160;", string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(text);
        }

        [HttpPost]
        [ValidateInput(false)]
        public JsonResult SaveMCQQuestion(MCQSaveDTO model)
        {
            try
            {
                if (model == null)
                {
                    return Json(new { Status = false, Message = "Invalid data submitted." });
                }

                if (model.course_id <= 0)
                {
                    return Json(new { Status = false, Message = "Please select a valid Course." });
                }

                if (string.IsNullOrWhiteSpace(model.questions))
                {
                    return Json(new { Status = false, Message = "Please enter Question text." });
                }

                if (string.IsNullOrWhiteSpace(model.txtOBA) || string.IsNullOrWhiteSpace(model.txtOBB))
                {
                    return Json(new { Status = false, Message = "Please provide Option A and Option B." });
                }

                if (!HasMCQOptionText(model.txtOBC))
                {
                    model.txtOBC = "";
                }
                if (!HasMCQOptionText(model.txtOBD))
                {
                    model.txtOBD = "";
                }

                string selectedOpt = (model.DDLobjectives ?? "").Trim().ToUpper();
                if (selectedOpt != "A" && selectedOpt != "B" && selectedOpt != "C" && selectedOpt != "D")
                {
                    return Json(new { Status = false, Message = "Please select a correct answer option (A, B, C, or D)." });
                }

                if (selectedOpt == "C" && string.IsNullOrWhiteSpace(model.txtOBC))
                {
                    return Json(new { Status = false, Message = "Option C cannot be empty when selected as correct answer." });
                }

                if (selectedOpt == "D" && string.IsNullOrWhiteSpace(model.txtOBD))
                {
                    return Json(new { Status = false, Message = "Option D cannot be empty when selected as correct answer." });
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    using (SqlTransaction tran = con.BeginTransaction())
                    {
                        try
                        {
                            int questionId = model.question_id;

                            if (questionId <= 0)
                            {
                                // 1. INSERT INTO [MCQQuestions]
                                string insertQuestionQuery = @"
                                    INSERT INTO [dbo].[MCQQuestions] 
                                        ([dateandtime], [questions], [course_id], [ISActive])
                                    VALUES 
                                        (GETDATE(), @questions, @course_id, 1);
                                    SELECT SCOPE_IDENTITY();";

                                using (SqlCommand cmdQ = new SqlCommand(insertQuestionQuery, con, tran))
                                {
                                    cmdQ.Parameters.AddWithValue("@questions", model.questions ?? (object)DBNull.Value);
                                    cmdQ.Parameters.AddWithValue("@course_id", model.course_id);
                                    questionId = Convert.ToInt32(cmdQ.ExecuteScalar());
                                }

                                // 2. INSERT INTO [MCQOjective] (4 options)
                                int objA_Id = 0, objB_Id = 0, objC_Id = 0, objD_Id = 0;

                                string insertObjQuery = @"
                                    INSERT INTO [dbo].[MCQOjective] 
                                        ([dateandtime], [objectives], [question_id])
                                    VALUES 
                                        (GETDATE(), @objectives, @question_id);
                                    SELECT SCOPE_IDENTITY();";

                                // Option A
                                using (SqlCommand cmdObj = new SqlCommand(insertObjQuery, con, tran))
                                {
                                    cmdObj.Parameters.AddWithValue("@objectives", model.txtOBA ?? "");
                                    cmdObj.Parameters.AddWithValue("@question_id", questionId);
                                    objA_Id = Convert.ToInt32(cmdObj.ExecuteScalar());
                                }

                                // Option B
                                using (SqlCommand cmdObj = new SqlCommand(insertObjQuery, con, tran))
                                {
                                    cmdObj.Parameters.AddWithValue("@objectives", model.txtOBB ?? "");
                                    cmdObj.Parameters.AddWithValue("@question_id", questionId);
                                    objB_Id = Convert.ToInt32(cmdObj.ExecuteScalar());
                                }

                                // Option C
                                using (SqlCommand cmdObj = new SqlCommand(insertObjQuery, con, tran))
                                {
                                    cmdObj.Parameters.AddWithValue("@objectives", model.txtOBC ?? "");
                                    cmdObj.Parameters.AddWithValue("@question_id", questionId);
                                    objC_Id = Convert.ToInt32(cmdObj.ExecuteScalar());
                                }

                                // Option D
                                using (SqlCommand cmdObj = new SqlCommand(insertObjQuery, con, tran))
                                {
                                    cmdObj.Parameters.AddWithValue("@objectives", model.txtOBD ?? "");
                                    cmdObj.Parameters.AddWithValue("@question_id", questionId);
                                    objD_Id = Convert.ToInt32(cmdObj.ExecuteScalar());
                                }

                                // 3. INSERT INTO [MCQAnswer]
                                int targetObjectiveId = 0;
                                if (selectedOpt == "A") targetObjectiveId = objA_Id;
                                else if (selectedOpt == "B") targetObjectiveId = objB_Id;
                                else if (selectedOpt == "C") targetObjectiveId = objC_Id;
                                else if (selectedOpt == "D") targetObjectiveId = objD_Id;

                                string insertAnsQuery = @"
                                    INSERT INTO [dbo].[MCQAnswer] 
                                        ([dateandtime], [question_id], [objective_id], [answer_description])
                                    VALUES 
                                        (GETDATE(), @question_id, @objective_id, @answer_description);";

                                using (SqlCommand cmdAns = new SqlCommand(insertAnsQuery, con, tran))
                                {
                                    cmdAns.Parameters.AddWithValue("@question_id", questionId);
                                    cmdAns.Parameters.AddWithValue("@objective_id", targetObjectiveId);
                                    cmdAns.Parameters.AddWithValue("@answer_description", (object)model.txtansdesc ?? DBNull.Value);
                                    cmdAns.ExecuteNonQuery();
                                }

                                tran.Commit();
                                return Json(new { Status = true, Message = "MCQ Question saved successfully.", QuestionID = questionId });
                            }
                            else
                            {
                                // Check if question exists in [dbo].[MCQ]
                                string checkMCQQuery = "SELECT COUNT(*) FROM [dbo].[MCQ] WHERE [MCQId] = @question_id;";
                                using (SqlCommand cmdChk = new SqlCommand(checkMCQQuery, con, tran))
                                {
                                    cmdChk.Parameters.AddWithValue("@question_id", questionId);
                                    int inMCQ = Convert.ToInt32(cmdChk.ExecuteScalar());
                                    if (inMCQ > 0)
                                    {
                                        string updateMCQQuery = @"
                                            UPDATE [dbo].[MCQ]
                                            SET [Question] = @Question,
                                                [A] = @A,
                                                [B] = @B,
                                                [C] = @C,
                                                [D] = @D,
                                                [Answer] = @Answer,
                                                [Describe] = @Describe,
                                                [course_id] = @course_id
                                            WHERE [MCQId] = @MCQId;";

                                        using (SqlCommand cmdUp = new SqlCommand(updateMCQQuery, con, tran))
                                        {
                                            cmdUp.Parameters.AddWithValue("@Question", model.questions ?? "");
                                            cmdUp.Parameters.AddWithValue("@A", model.txtOBA ?? "");
                                            cmdUp.Parameters.AddWithValue("@B", model.txtOBB ?? "");
                                            cmdUp.Parameters.AddWithValue("@C", string.IsNullOrWhiteSpace(model.txtOBC) ? (object)DBNull.Value : model.txtOBC);
                                            cmdUp.Parameters.AddWithValue("@D", string.IsNullOrWhiteSpace(model.txtOBD) ? (object)DBNull.Value : model.txtOBD);
                                            cmdUp.Parameters.AddWithValue("@Answer", model.DDLobjectives ?? "");
                                            cmdUp.Parameters.AddWithValue("@Describe", string.IsNullOrWhiteSpace(model.txtansdesc) ? (object)DBNull.Value : model.txtansdesc);
                                            cmdUp.Parameters.AddWithValue("@course_id", model.course_id);
                                            cmdUp.Parameters.AddWithValue("@MCQId", questionId);
                                            cmdUp.ExecuteNonQuery();
                                        }

                                        tran.Commit();
                                        return Json(new { Status = true, Message = "MCQ Question updated successfully.", QuestionID = questionId });
                                    }
                                }

                                // UPDATE EXISTING MCQ QUESTION
                                string updateQuestionQuery = @"
                                    UPDATE [dbo].[MCQQuestions] 
                                    SET [questions] = @questions, 
                                        [course_id] = @course_id 
                                    WHERE [question_id] = @question_id;";

                                using (SqlCommand cmdQ = new SqlCommand(updateQuestionQuery, con, tran))
                                {
                                    cmdQ.Parameters.AddWithValue("@questions", model.questions ?? (object)DBNull.Value);
                                    cmdQ.Parameters.AddWithValue("@course_id", model.course_id);
                                    cmdQ.Parameters.AddWithValue("@question_id", questionId);
                                    cmdQ.ExecuteNonQuery();
                                }

                                // Fetch existing objectives ordered by objective_id ASC
                                List<int> existingObjIds = new List<int>();
                                string getExistingObjQuery = "SELECT [objective_id] FROM [dbo].[MCQOjective] WHERE [question_id] = @question_id ORDER BY [objective_id] ASC;";
                                using (SqlCommand cmdGet = new SqlCommand(getExistingObjQuery, con, tran))
                                {
                                    cmdGet.Parameters.AddWithValue("@question_id", questionId);
                                    using (SqlDataReader rdr = cmdGet.ExecuteReader())
                                    {
                                        while (rdr.Read())
                                        {
                                            existingObjIds.Add(Convert.ToInt32(rdr["objective_id"]));
                                        }
                                    }
                                }

                                int objA_Id = 0, objB_Id = 0, objC_Id = 0, objD_Id = 0;

                                if (existingObjIds.Count == 4)
                                {
                                    objA_Id = existingObjIds[0];
                                    objB_Id = existingObjIds[1];
                                    objC_Id = existingObjIds[2];
                                    objD_Id = existingObjIds[3];

                                    string updateObjQuery = "UPDATE [dbo].[MCQOjective] SET [objectives] = @objectives WHERE [objective_id] = @objective_id;";
                                    string[] texts = new string[] { model.txtOBA ?? "", model.txtOBB ?? "", model.txtOBC ?? "", model.txtOBD ?? "" };

                                    for (int i = 0; i < 4; i++)
                                    {
                                        using (SqlCommand cmdUp = new SqlCommand(updateObjQuery, con, tran))
                                        {
                                            cmdUp.Parameters.AddWithValue("@objectives", texts[i]);
                                            cmdUp.Parameters.AddWithValue("@objective_id", existingObjIds[i]);
                                            cmdUp.ExecuteNonQuery();
                                        }
                                    }
                                }
                                else
                                {
                                    using (SqlCommand delAns = new SqlCommand("DELETE FROM [dbo].[MCQAnswer] WHERE [question_id] = @question_id", con, tran))
                                    {
                                        delAns.Parameters.AddWithValue("@question_id", questionId);
                                        delAns.ExecuteNonQuery();
                                    }
                                    using (SqlCommand delObj = new SqlCommand("DELETE FROM [dbo].[MCQOjective] WHERE [question_id] = @question_id", con, tran))
                                    {
                                        delObj.Parameters.AddWithValue("@question_id", questionId);
                                        delObj.ExecuteNonQuery();
                                    }

                                    string insertObjQuery = @"
                                        INSERT INTO [dbo].[MCQOjective] ([dateandtime], [objectives], [question_id])
                                        VALUES (GETDATE(), @objectives, @question_id);
                                        SELECT SCOPE_IDENTITY();";

                                    string[] texts = new string[] { model.txtOBA ?? "", model.txtOBB ?? "", model.txtOBC ?? "", model.txtOBD ?? "" };
                                    int[] newIds = new int[4];
                                    for (int i = 0; i < 4; i++)
                                    {
                                        using (SqlCommand cmdObj = new SqlCommand(insertObjQuery, con, tran))
                                        {
                                            cmdObj.Parameters.AddWithValue("@objectives", texts[i]);
                                            cmdObj.Parameters.AddWithValue("@question_id", questionId);
                                            newIds[i] = Convert.ToInt32(cmdObj.ExecuteScalar());
                                        }
                                    }
                                    objA_Id = newIds[0];
                                    objB_Id = newIds[1];
                                    objC_Id = newIds[2];
                                    objD_Id = newIds[3];
                                }

                                int targetObjectiveId = 0;
                                if (selectedOpt == "A") targetObjectiveId = objA_Id;
                                else if (selectedOpt == "B") targetObjectiveId = objB_Id;
                                else if (selectedOpt == "C") targetObjectiveId = objC_Id;
                                else if (selectedOpt == "D") targetObjectiveId = objD_Id;

                                // Upsert Answer
                                string checkAnsQuery = "SELECT COUNT(*) FROM [dbo].[MCQAnswer] WHERE [question_id] = @question_id;";
                                int ansCount = 0;
                                using (SqlCommand checkCmd = new SqlCommand(checkAnsQuery, con, tran))
                                {
                                    checkCmd.Parameters.AddWithValue("@question_id", questionId);
                                    ansCount = Convert.ToInt32(checkCmd.ExecuteScalar());
                                }

                                if (ansCount > 0)
                                {
                                    string updateAnsQuery = @"
                                        UPDATE [dbo].[MCQAnswer] 
                                        SET [objective_id] = @objective_id, 
                                            [answer_description] = @answer_description 
                                        WHERE [question_id] = @question_id;";

                                    using (SqlCommand cmdAns = new SqlCommand(updateAnsQuery, con, tran))
                                    {
                                        cmdAns.Parameters.AddWithValue("@objective_id", targetObjectiveId);
                                        cmdAns.Parameters.AddWithValue("@answer_description", (object)model.txtansdesc ?? DBNull.Value);
                                        cmdAns.Parameters.AddWithValue("@question_id", questionId);
                                        cmdAns.ExecuteNonQuery();
                                    }
                                }
                                else
                                {
                                    string insertAnsQuery = @"
                                        INSERT INTO [dbo].[MCQAnswer] 
                                            ([dateandtime], [question_id], [objective_id], [answer_description])
                                        VALUES 
                                            (GETDATE(), @question_id, @objective_id, @answer_description);";

                                    using (SqlCommand cmdAns = new SqlCommand(insertAnsQuery, con, tran))
                                    {
                                        cmdAns.Parameters.AddWithValue("@question_id", questionId);
                                        cmdAns.Parameters.AddWithValue("@objective_id", targetObjectiveId);
                                        cmdAns.Parameters.AddWithValue("@answer_description", (object)model.txtansdesc ?? DBNull.Value);
                                        cmdAns.ExecuteNonQuery();
                                    }
                                }

                                tran.Commit();
                                return Json(new { Status = true, Message = "MCQ Question updated successfully.", QuestionID = questionId });
                            }
                        }
                        catch (Exception ex)
                        {
                            tran.Rollback();
                            return Json(new { Status = false, Message = "Transaction error: " + ex.Message });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetMCQQuestionByID(int question_id)
        {
            try
            {
                MCQQuestionDetailDTO detail = null;

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    string qQuery = @"
                        SELECT q.[question_id], q.[dateandtime], q.[questions], q.[course_id], q.[ISActive],
                               c.[CourseTitle],
                               a.[answer_id], a.[objective_id], a.[answer_description]
                        FROM [dbo].[MCQQuestions] q
                        LEFT JOIN [dbo].[CourseMaster] c ON q.[course_id] = c.[CourseID]
                        LEFT JOIN [dbo].[MCQAnswer] a ON q.[question_id] = a.[question_id]
                        WHERE q.[question_id] = @question_id;";

                    using (SqlCommand cmd = new SqlCommand(qQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@question_id", question_id);
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                detail = new MCQQuestionDetailDTO
                                {
                                    question_id = Convert.ToInt32(rdr["question_id"]),
                                    dateandtime = rdr["dateandtime"] != DBNull.Value ? Convert.ToDateTime(rdr["dateandtime"]) : (DateTime?)null,
                                    questions = rdr["questions"] != DBNull.Value ? rdr["questions"].ToString() : "",
                                    course_id = rdr["course_id"] != DBNull.Value ? Convert.ToInt32(rdr["course_id"]) : (int?)null,
                                    CourseTitle = rdr["CourseTitle"] != DBNull.Value ? rdr["CourseTitle"].ToString() : "",
                                    ISActive = rdr["ISActive"] != DBNull.Value ? Convert.ToByte(rdr["ISActive"]) : (byte?)null,
                                    answer_id = rdr["answer_id"] != DBNull.Value ? Convert.ToInt32(rdr["answer_id"]) : 0,
                                    objective_id = rdr["objective_id"] != DBNull.Value ? Convert.ToInt32(rdr["objective_id"]) : 0,
                                    answer_description = rdr["answer_description"] != DBNull.Value ? rdr["answer_description"].ToString() : ""
                                };
                            }
                        }
                    }

                    if (detail != null)
                    {
                        string objQuery = @"
                            SELECT [objective_id], [objectives]
                            FROM [dbo].[MCQOjective]
                            WHERE [question_id] = @question_id
                            ORDER BY [objective_id] ASC;";

                        using (SqlCommand cmdObj = new SqlCommand(objQuery, con))
                        {
                            cmdObj.Parameters.AddWithValue("@question_id", question_id);
                            using (SqlDataReader rdr = cmdObj.ExecuteReader())
                            {
                                int idx = 0;
                                while (rdr.Read())
                                {
                                    int oid = Convert.ToInt32(rdr["objective_id"]);
                                    string text = rdr["objectives"] != DBNull.Value ? rdr["objectives"].ToString() : "";

                                    if (idx == 0) { detail.OptionA_ID = oid; detail.OptionA_Text = text; }
                                    else if (idx == 1) { detail.OptionB_ID = oid; detail.OptionB_Text = text; }
                                    else if (idx == 2) { detail.OptionC_ID = oid; detail.OptionC_Text = text; }
                                    else if (idx == 3) { detail.OptionD_ID = oid; detail.OptionD_Text = text; }

                                    idx++;
                                }
                            }
                        }

                        if (detail.objective_id > 0)
                        {
                            if (detail.objective_id == detail.OptionA_ID) detail.CorrectOption = "A";
                            else if (detail.objective_id == detail.OptionB_ID) detail.CorrectOption = "B";
                            else if (detail.objective_id == detail.OptionC_ID) detail.CorrectOption = "C";
                            else if (detail.objective_id == detail.OptionD_ID) detail.CorrectOption = "D";
                        }
                    }

                    if (detail == null)
                    {
                        string mcqSql = @"SELECT m.[MCQId], m.[Question], m.[A], m.[B], m.[C], m.[D], m.[Answer], m.[Describe], m.[course_id],
                                                 c.[CourseTitle]
                                          FROM [dbo].[MCQ] m
                                          LEFT JOIN [dbo].[CourseMaster] c ON m.[course_id] = c.[CourseID]
                                          WHERE m.[MCQId] = @question_id;";
                        using (SqlCommand cmdMCQ = new SqlCommand(mcqSql, con))
                        {
                            cmdMCQ.Parameters.AddWithValue("@question_id", question_id);
                            using (SqlDataReader rdr = cmdMCQ.ExecuteReader())
                            {
                                if (rdr.Read())
                                {
                                    detail = new MCQQuestionDetailDTO
                                    {
                                        question_id = Convert.ToInt32(rdr["MCQId"]),
                                        dateandtime = (DateTime?)null,
                                        questions = rdr["Question"] != DBNull.Value ? rdr["Question"].ToString() : "",
                                        course_id = rdr["course_id"] != DBNull.Value ? Convert.ToInt32(rdr["course_id"]) : (int?)null,
                                        CourseTitle = rdr["CourseTitle"] != DBNull.Value ? rdr["CourseTitle"].ToString() : "",
                                        ISActive = 1,
                                        OptionA_Text = rdr["A"] != DBNull.Value ? rdr["A"].ToString() : "",
                                        OptionB_Text = rdr["B"] != DBNull.Value ? rdr["B"].ToString() : "",
                                        OptionC_Text = rdr["C"] != DBNull.Value ? rdr["C"].ToString() : "",
                                        OptionD_Text = rdr["D"] != DBNull.Value ? rdr["D"].ToString() : "",
                                        CorrectOption = rdr["Answer"] != DBNull.Value ? rdr["Answer"].ToString() : "",
                                        answer_description = rdr["Describe"] != DBNull.Value ? rdr["Describe"].ToString() : ""
                                    };
                                }
                            }
                        }
                    }
                }

                if (detail != null)
                {
                    return Json(new { Status = true, Data = detail }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { Status = false, Message = "Question not found." }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetMCQQuestionList(int course_id = 0)
        {
            try
            {
                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    // Ensure ISActive column exists in [dbo].[MCQ]
                    string ensureColSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MCQ]') AND name = 'ISActive')
                        BEGIN
                            ALTER TABLE [dbo].[MCQ] ADD [ISActive] INT NULL DEFAULT 1;
                        END";
                    using (SqlCommand cmdCol = new SqlCommand(ensureColSql, con))
                    {
                        cmdCol.ExecuteNonQuery();
                    }

                    // 1. Query records from [dbo].[MCQ]
                    string mcqQuery = @"
                        SELECT m.[MCQId], m.[Question], m.[A], m.[B], m.[C], m.[D], m.[Answer], m.[Describe], m.[course_id],
                               ISNULL(m.[ISActive], 1) AS [ISActive],
                               c.[CourseTitle]
                        FROM [dbo].[MCQ] m
                        LEFT JOIN [dbo].[CourseMaster] c ON m.[course_id] = c.[CourseID]
                        WHERE (@course_id = 0 OR m.[course_id] = @course_id)
                        ORDER BY m.[MCQId] ASC;";

                    DataTable dtMCQ = new DataTable();
                    using (SqlCommand cmdMCQ = new SqlCommand(mcqQuery, con))
                    {
                        cmdMCQ.Parameters.AddWithValue("@course_id", course_id);
                        using (SqlDataAdapter daMCQ = new SqlDataAdapter(cmdMCQ))
                        {
                            daMCQ.Fill(dtMCQ);
                        }
                    }

                    foreach (DataRow row in dtMCQ.Rows)
                    {
                        string optC = row["C"] != DBNull.Value ? row["C"].ToString() : "";
                        string optD = row["D"] != DBNull.Value ? row["D"].ToString() : "";

                        list.Add(new
                        {
                            question_id = Convert.ToInt32(row["MCQId"]),
                            dateandtime = "",
                            questions = row["Question"] != DBNull.Value ? row["Question"].ToString() : "",
                            course_id = row["course_id"] != DBNull.Value ? Convert.ToInt32(row["course_id"]) : 0,
                            CourseTitle = row["CourseTitle"] != DBNull.Value ? row["CourseTitle"].ToString() : "",
                            ISActive = (row["ISActive"] != DBNull.Value && Convert.ToInt32(row["ISActive"]) == 0) ? 0 : 1,
                            OptionA = row["A"] != DBNull.Value ? row["A"].ToString() : "",
                            OptionB = row["B"] != DBNull.Value ? row["B"].ToString() : "",
                            OptionC = HasMCQOptionText(optC) ? optC : "",
                            OptionD = HasMCQOptionText(optD) ? optD : "",
                            CorrectOption = row["Answer"] != DBNull.Value ? row["Answer"].ToString() : "",
                            answer_description = row["Describe"] != DBNull.Value ? row["Describe"].ToString() : "",
                            Source = "MCQ"
                        });
                    }

                    // 2. Query records from [dbo].[MCQQuestions] (if any exists)
                    string qQuery = @"
                        SELECT q.[question_id], q.[dateandtime], q.[questions], q.[course_id], q.[ISActive],
                               c.[CourseTitle],
                               a.[answer_id], a.[objective_id] AS AnswerObjectiveID, a.[answer_description]
                        FROM [dbo].[MCQQuestions] q
                        LEFT JOIN [dbo].[CourseMaster] c ON q.[course_id] = c.[CourseID]
                        LEFT JOIN [dbo].[MCQAnswer] a ON q.[question_id] = a.[question_id]
                        WHERE (@course_id = 0 OR q.[course_id] = @course_id)
                        ORDER BY q.[question_id] ASC;";

                    DataTable dtQuestions = new DataTable();
                    using (SqlCommand cmd = new SqlCommand(qQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@course_id", course_id);
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dtQuestions);
                        }
                    }

                    if (dtQuestions.Rows.Count > 0)
                    {
                        List<int> qIds = new List<int>();
                        foreach (DataRow row in dtQuestions.Rows)
                        {
                            qIds.Add(Convert.ToInt32(row["question_id"]));
                        }

                        string inClause = string.Join(",", qIds);
                        string objQuery = $@"
                            SELECT [objective_id], [objectives], [question_id]
                            FROM [dbo].[MCQOjective]
                            WHERE [question_id] IN ({inClause})
                            ORDER BY [question_id], [objective_id] ASC;";

                        DataTable dtObjectives = new DataTable();
                        using (SqlCommand cmdObj = new SqlCommand(objQuery, con))
                        {
                            using (SqlDataAdapter da = new SqlDataAdapter(cmdObj))
                            {
                                da.Fill(dtObjectives);
                            }
                        }

                        var objLookup = new Dictionary<int, List<Tuple<int, string>>>();
                        foreach (DataRow row in dtObjectives.Rows)
                        {
                            int qid = Convert.ToInt32(row["question_id"]);
                            int oid = Convert.ToInt32(row["objective_id"]);
                            string text = row["objectives"] != DBNull.Value ? row["objectives"].ToString() : "";

                            if (!objLookup.ContainsKey(qid))
                            {
                                objLookup[qid] = new List<Tuple<int, string>>();
                            }
                            objLookup[qid].Add(new Tuple<int, string>(oid, text));
                        }

                        foreach (DataRow row in dtQuestions.Rows)
                        {
                            int qid = Convert.ToInt32(row["question_id"]);
                            int ansObjId = row["AnswerObjectiveID"] != DBNull.Value ? Convert.ToInt32(row["AnswerObjectiveID"]) : 0;
                            string correctOpt = "";

                            string optA = "", optB = "", optC = "", optD = "";
                            if (objLookup.ContainsKey(qid))
                            {
                                var opts = objLookup[qid];
                                if (opts.Count > 0)
                                {
                                    optA = opts[0].Item2;
                                    if (opts[0].Item1 == ansObjId) correctOpt = "A";
                                }
                                if (opts.Count > 1)
                                {
                                    optB = opts[1].Item2;
                                    if (opts[1].Item1 == ansObjId) correctOpt = "B";
                                }
                                if (opts.Count > 2)
                                {
                                    optC = opts[2].Item2;
                                    if (opts[2].Item1 == ansObjId) correctOpt = "C";
                                }
                                if (opts.Count > 3)
                                {
                                    optD = opts[3].Item2;
                                    if (opts[3].Item1 == ansObjId) correctOpt = "D";
                                }
                            }

                            list.Add(new
                            {
                                question_id = qid,
                                dateandtime = row["dateandtime"] != DBNull.Value ? Convert.ToDateTime(row["dateandtime"]).ToString("dd/MM/yyyy hh:mm tt") : "",
                                questions = row["questions"] != DBNull.Value ? row["questions"].ToString() : "",
                                course_id = row["course_id"] != DBNull.Value ? Convert.ToInt32(row["course_id"]) : 0,
                                CourseTitle = row["CourseTitle"] != DBNull.Value ? row["CourseTitle"].ToString() : "",
                                ISActive = (row["ISActive"] != DBNull.Value && Convert.ToInt32(row["ISActive"]) == 0) ? 0 : 1,
                                OptionA = optA,
                                OptionB = optB,
                                OptionC = HasMCQOptionText(optC) ? optC : "",
                                OptionD = HasMCQOptionText(optD) ? optD : "",
                                CorrectOption = correctOpt,
                                answer_description = row["answer_description"] != DBNull.Value ? row["answer_description"].ToString() : "",
                                Source = "MCQQuestions"
                            });
                        }
                    }
                }

                return Json(new { Status = true, Data = list }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult DeleteMCQQuestion(int question_id, bool hardDelete = true)
        {
            try
            {
                if (question_id <= 0)
                {
                    return Json(new { Status = false, Message = "Invalid question ID." });
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    using (SqlTransaction tran = con.BeginTransaction())
                    {
                        try
                        {
                            // 1. Check if exists in [dbo].[MCQ]
                            string checkMCQSql = "SELECT COUNT(*) FROM [dbo].[MCQ] WHERE [MCQId] = @question_id;";
                            using (SqlCommand cmdChk = new SqlCommand(checkMCQSql, con, tran))
                            {
                                cmdChk.Parameters.AddWithValue("@question_id", question_id);
                                int inMCQ = Convert.ToInt32(cmdChk.ExecuteScalar());
                                if (inMCQ > 0)
                                {
                                    using (SqlCommand cmdDel = new SqlCommand("DELETE FROM [dbo].[MCQ] WHERE [MCQId] = @question_id;", con, tran))
                                    {
                                        cmdDel.Parameters.AddWithValue("@question_id", question_id);
                                        cmdDel.ExecuteNonQuery();
                                    }
                                    tran.Commit();
                                    return Json(new { Status = true, Message = "MCQ Question deleted successfully." });
                                }
                            }

                            // 2. Otherwise delete from MCQAnswer
                            using (SqlCommand cmdAns = new SqlCommand("DELETE FROM [dbo].[MCQAnswer] WHERE [question_id] = @question_id", con, tran))
                            {
                                cmdAns.Parameters.AddWithValue("@question_id", question_id);
                                cmdAns.ExecuteNonQuery();
                            }

                            // 2. Delete from MCQOjective
                            using (SqlCommand cmdObj = new SqlCommand("DELETE FROM [dbo].[MCQOjective] WHERE [question_id] = @question_id", con, tran))
                            {
                                cmdObj.Parameters.AddWithValue("@question_id", question_id);
                                cmdObj.ExecuteNonQuery();
                            }

                            // 3. Delete from MCQQuestions
                            using (SqlCommand cmdQ = new SqlCommand("DELETE FROM [dbo].[MCQQuestions] WHERE [question_id] = @question_id", con, tran))
                            {
                                cmdQ.Parameters.AddWithValue("@question_id", question_id);
                                cmdQ.ExecuteNonQuery();
                            }

                            tran.Commit();
                            return Json(new { Status = true, Message = "Question deleted successfully from all three tables." });
                        }
                        catch
                        {
                            tran.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult ToggleMCQQuestionStatus(int question_id, int status)
        {
            try
            {
                if (question_id <= 0)
                {
                    return Json(new { Status = false, Message = "Invalid question ID." });
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    // Ensure ISActive column exists in [dbo].[MCQ]
                    string ensureColSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MCQ]') AND name = 'ISActive')
                        BEGIN
                            ALTER TABLE [dbo].[MCQ] ADD [ISActive] INT NULL DEFAULT 1;
                        END";
                    using (SqlCommand cmdCol = new SqlCommand(ensureColSql, con))
                    {
                        cmdCol.ExecuteNonQuery();
                    }

                    // Update [dbo].[MCQ] if question exists there
                    using (SqlCommand cmdMCQ = new SqlCommand("UPDATE [dbo].[MCQ] SET [ISActive] = @ISActive WHERE [MCQId] = @question_id", con))
                    {
                        cmdMCQ.Parameters.AddWithValue("@ISActive", status == 1 ? 1 : 0);
                        cmdMCQ.Parameters.AddWithValue("@question_id", question_id);
                        cmdMCQ.ExecuteNonQuery();
                    }

                    // Also update [dbo].[MCQQuestions] if question exists there
                    string query = "UPDATE [dbo].[MCQQuestions] SET [ISActive] = @ISActive WHERE [question_id] = @question_id";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@ISActive", status == 1 ? 1 : 0);
                        cmd.Parameters.AddWithValue("@question_id", question_id);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { Status = true, Message = (status == 1 ? "Question activated successfully." : "Question deactivated successfully.") });
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message });
            }
        }
        #endregion

        #region MCQ Excel Upload

        public class MCQExcelRowItem
        {
            public int RowIndex { get; set; }
            public string Question { get; set; }
            public string A { get; set; }
            public string B { get; set; }
            public string C { get; set; }
            public string D { get; set; }
            public string OptionA { get { return A; } set { if (string.IsNullOrEmpty(A)) A = value; } }
            public string OptionB { get { return B; } set { if (string.IsNullOrEmpty(B)) B = value; } }
            public string OptionC { get { return C; } set { if (string.IsNullOrEmpty(C)) C = value; } }
            public string OptionD { get { return D; } set { if (string.IsNullOrEmpty(D)) D = value; } }
            public string Answer { get; set; }
            public string Describe { get; set; }
        }

        private static string NormalizeMCQQuestionText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string cleaned = Regex.Replace(text, "<.*?>", string.Empty);
            cleaned = cleaned.Replace("&nbsp;", " ").Replace("&#160;", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned.ToLowerInvariant();
        }

        private List<MCQExcelRowItem> ParseMCQExcelFile(Stream fileStream)
        {
            var rows = new List<MCQExcelRowItem>();

            using (MemoryStream ms = new MemoryStream())
            {
                fileStream.CopyTo(ms);
                ms.Position = 0;

                using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Read, true))
                {
                    // 1. Read shared strings
                    List<string> sharedStrings = new List<string>();
                    ZipArchiveEntry sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
                    if (sharedStringsEntry != null)
                    {
                        using (Stream ssStream = sharedStringsEntry.Open())
                        {
                            XDocument xdoc = XDocument.Load(ssStream);
                            XNamespace ns = xdoc.Root.Name.Namespace;
                            foreach (XElement si in xdoc.Root.Elements(ns + "si"))
                            {
                                XElement t = si.Element(ns + "t");
                                if (t != null)
                                {
                                    sharedStrings.Add(t.Value);
                                }
                                else
                                {
                                    string combined = string.Concat(si.Elements(ns + "r").Elements(ns + "t").Select(e => e.Value));
                                    sharedStrings.Add(combined);
                                }
                            }
                        }
                    }

                    // 2. Find first sheet
                    ZipArchiveEntry sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
                    if (sheetEntry == null)
                    {
                        sheetEntry = archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
                    }

                    if (sheetEntry == null)
                    {
                        throw new Exception("Worksheet could not be found in the Excel file.");
                    }

                    using (Stream sStream = sheetEntry.Open())
                    {
                        XDocument xdoc = XDocument.Load(sStream);
                        XNamespace ns = xdoc.Root.Name.Namespace;
                        XElement sheetData = xdoc.Root.Element(ns + "sheetData");
                        if (sheetData == null) return rows;

                        foreach (XElement rowElem in sheetData.Elements(ns + "row"))
                        {
                            string rAttr = (string)rowElem.Attribute("r");
                            int rNum = 0;
                            int.TryParse(rAttr, out rNum);

                            // Skip header row (row 1)
                            if (rNum <= 1) continue;

                            Dictionary<string, string> cellValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                            foreach (XElement cell in rowElem.Elements(ns + "c"))
                            {
                                string cellRef = (string)cell.Attribute("r");
                                if (string.IsNullOrEmpty(cellRef)) continue;

                                string colLetter = Regex.Replace(cellRef, @"[\d]", "").ToUpper();
                                string cellType = (string)cell.Attribute("t");
                                string val = "";

                                if (cellType == "s")
                                {
                                    string vStr = cell.Element(ns + "v") != null ? cell.Element(ns + "v").Value : "";
                                    int sIdx;
                                    if (int.TryParse(vStr, out sIdx) && sIdx >= 0 && sIdx < sharedStrings.Count)
                                    {
                                        val = sharedStrings[sIdx];
                                    }
                                }
                                else if (cellType == "inlineStr")
                                {
                                    XElement isEl = cell.Element(ns + "is");
                                    if (isEl != null)
                                    {
                                        XElement tEl = isEl.Element(ns + "t");
                                        val = tEl != null ? tEl.Value : isEl.Value;
                                    }
                                }
                                else
                                {
                                    XElement vEl = cell.Element(ns + "v");
                                    if (vEl != null) val = vEl.Value;
                                }

                                cellValues[colLetter] = val != null ? val.Trim() : "";
                            }

                            string question = cellValues.ContainsKey("A") ? cellValues["A"] : "";
                            string optA = cellValues.ContainsKey("B") ? cellValues["B"] : "";
                            string optB = cellValues.ContainsKey("C") ? cellValues["C"] : "";
                            string optC = cellValues.ContainsKey("D") ? cellValues["D"] : "";
                            string optD = cellValues.ContainsKey("E") ? cellValues["E"] : "";
                            string ans = cellValues.ContainsKey("F") ? cellValues["F"] : "";
                            string desc = cellValues.ContainsKey("G") ? cellValues["G"] : "";

                            if (string.IsNullOrWhiteSpace(question) && string.IsNullOrWhiteSpace(optA) && string.IsNullOrWhiteSpace(optB))
                            {
                                continue;
                            }

                            // Standardize Answer
                            string normAns = (ans ?? "").Trim().ToUpper();
                            if (normAns == "1") normAns = "A";
                            else if (normAns == "2") normAns = "B";
                            else if (normAns == "3") normAns = "C";
                            else if (normAns == "4") normAns = "D";

                            if (normAns != "A" && normAns != "B" && normAns != "C" && normAns != "D")
                            {
                                if (!string.IsNullOrWhiteSpace(optA) && string.Equals(ans.Trim(), optA.Trim(), StringComparison.OrdinalIgnoreCase)) normAns = "A";
                                else if (!string.IsNullOrWhiteSpace(optB) && string.Equals(ans.Trim(), optB.Trim(), StringComparison.OrdinalIgnoreCase)) normAns = "B";
                                else if (!string.IsNullOrWhiteSpace(optC) && string.Equals(ans.Trim(), optC.Trim(), StringComparison.OrdinalIgnoreCase)) normAns = "C";
                                else if (!string.IsNullOrWhiteSpace(optD) && string.Equals(ans.Trim(), optD.Trim(), StringComparison.OrdinalIgnoreCase)) normAns = "D";
                            }

                            if (string.IsNullOrEmpty(normAns))
                            {
                                normAns = "A";
                            }

                            rows.Add(new MCQExcelRowItem
                            {
                                RowIndex = rNum,
                                Question = question,
                                A = optA,
                                B = optB,
                                C = string.IsNullOrWhiteSpace(optC) ? null : optC,
                                D = string.IsNullOrWhiteSpace(optD) ? null : optD,
                                Answer = normAns,
                                Describe = string.IsNullOrWhiteSpace(desc) ? null : desc
                            });
                        }
                    }
                }
            }

            return rows;
        }

        [HttpPost]
        public JsonResult PreviewMCQExcel()
        {
            try
            {
                if (Request.Files == null || Request.Files.Count == 0 || Request.Files[0] == null || Request.Files[0].ContentLength == 0)
                {
                    return Json(new { Status = false, Message = "Please select an Excel file." });
                }

                HttpPostedFileBase file = Request.Files[0];
                string ext = Path.GetExtension(file.FileName).ToLower();
                if (ext != ".xlsx" && ext != ".xls")
                {
                    return Json(new { Status = false, Message = "Invalid file type. Please upload an Excel (.xlsx, .xls) file." });
                }

                List<MCQExcelRowItem> rows = ParseMCQExcelFile(file.InputStream);
                if (rows == null || rows.Count == 0)
                {
                    return Json(new { Status = false, Message = "No questions found in the Excel file. Please ensure data is present starting from Row 2." });
                }

                return Json(new { Status = true, Data = rows, TotalCount = rows.Count });
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = "Error parsing Excel file: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateInput(false)]
        public JsonResult UploadMCQExcel(int course_id, string excelQuestionsJson = null)
        {
            try
            {
                if (course_id <= 0)
                {
                    return Json(new { Status = false, Message = "Invalid Course ID. Please reselect the course." });
                }

                List<MCQExcelRowItem> rows = new List<MCQExcelRowItem>();

                // 1. Try parsing from client-side JSON (fast, reliable SheetJS parse)
                if (!string.IsNullOrWhiteSpace(excelQuestionsJson))
                {
                    try
                    {
                        var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                        serializer.MaxJsonLength = int.MaxValue;
                        var jsonRows = serializer.Deserialize<List<MCQExcelRowItem>>(excelQuestionsJson);
                        if (jsonRows != null && jsonRows.Count > 0)
                        {
                            rows = jsonRows;
                        }
                    }
                    catch { }
                }

                // 2. If no JSON rows, fallback to server-side Excel file parsing
                if (rows.Count == 0)
                {
                    if (Request.Files == null || Request.Files.Count == 0 || Request.Files[0] == null || Request.Files[0].ContentLength == 0)
                    {
                        return Json(new { Status = false, Message = "Please select an Excel file to upload." });
                    }

                    HttpPostedFileBase file = Request.Files[0];
                    string ext = Path.GetExtension(file.FileName).ToLower();
                    if (ext != ".xlsx" && ext != ".xls")
                    {
                        return Json(new { Status = false, Message = "Invalid file type. Please upload an Excel (.xlsx, .xls) file." });
                    }

                    rows = ParseMCQExcelFile(file.InputStream);
                }

                if (rows == null || rows.Count == 0)
                {
                    return Json(new { Status = false, Message = "No questions found in the Excel file. Please ensure questions are present starting from Row 2." });
                }

                int savedCount = 0;
                int duplicateCount = 0;

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    // 1. Ensure [dbo].[MCQ] table and its columns exist in SQL database
                    string ensureTableSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MCQ')
                        BEGIN
                            CREATE TABLE [dbo].[MCQ] (
                                [MCQId] INT IDENTITY(1,1) PRIMARY KEY,
                                [Question] NVARCHAR(MAX) NULL,
                                [A] NVARCHAR(MAX) NULL,
                                [B] NVARCHAR(MAX) NULL,
                                [C] NVARCHAR(MAX) NULL,
                                [D] NVARCHAR(MAX) NULL,
                                [Answer] NVARCHAR(50) NULL,
                                [Describe] NVARCHAR(MAX) NULL,
                                [course_id] INT NULL,
                                [CreatedDate] DATETIME DEFAULT GETDATE()
                            );
                        END
                        ELSE
                        BEGIN
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MCQ]') AND name = 'course_id')
                                ALTER TABLE [dbo].[MCQ] ADD [course_id] INT NULL;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MCQ]') AND name = 'Question')
                                ALTER TABLE [dbo].[MCQ] ADD [Question] NVARCHAR(MAX) NULL;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MCQ]') AND name = 'A')
                                ALTER TABLE [dbo].[MCQ] ADD [A] NVARCHAR(MAX) NULL;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MCQ]') AND name = 'B')
                                ALTER TABLE [dbo].[MCQ] ADD [B] NVARCHAR(MAX) NULL;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MCQ]') AND name = 'C')
                                ALTER TABLE [dbo].[MCQ] ADD [C] NVARCHAR(MAX) NULL;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MCQ]') AND name = 'D')
                                ALTER TABLE [dbo].[MCQ] ADD [D] NVARCHAR(MAX) NULL;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MCQ]') AND name = 'Answer')
                                ALTER TABLE [dbo].[MCQ] ADD [Answer] NVARCHAR(50) NULL;
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MCQ]') AND name = 'Describe')
                                ALTER TABLE [dbo].[MCQ] ADD [Describe] NVARCHAR(MAX) NULL;
                        END";

                    using (SqlCommand cmdInit = new SqlCommand(ensureTableSql, con))
                    {
                        cmdInit.ExecuteNonQuery();
                    }

                    // 2. Pre-load existing questions for this course to prevent duplicate questions
                    HashSet<string> existingQuestions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    using (SqlCommand cmdEx = new SqlCommand("SELECT [Question] FROM [dbo].[MCQ] WHERE [course_id] = @course_id AND [Question] IS NOT NULL;", con))
                    {
                        cmdEx.Parameters.AddWithValue("@course_id", course_id);
                        using (SqlDataReader rdr = cmdEx.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                string qText = rdr["Question"] != DBNull.Value ? rdr["Question"].ToString() : "";
                                string nq = NormalizeMCQQuestionText(qText);
                                if (!string.IsNullOrEmpty(nq))
                                {
                                    existingQuestions.Add(nq);
                                }
                            }
                        }
                    }

                    string checkQTable = "SELECT COUNT(*) FROM sys.tables WHERE name = 'MCQQuestions';";
                    bool hasQTable = false;
                    using (SqlCommand cmdChk = new SqlCommand(checkQTable, con))
                    {
                        hasQTable = Convert.ToInt32(cmdChk.ExecuteScalar()) > 0;
                    }
                    if (hasQTable)
                    {
                        using (SqlCommand cmdExQ = new SqlCommand("SELECT [questions] FROM [dbo].[MCQQuestions] WHERE [course_id] = @course_id AND [questions] IS NOT NULL;", con))
                        {
                            cmdExQ.Parameters.AddWithValue("@course_id", course_id);
                            using (SqlDataReader rdr = cmdExQ.ExecuteReader())
                            {
                                while (rdr.Read())
                                {
                                    string qText = rdr["questions"] != DBNull.Value ? rdr["questions"].ToString() : "";
                                    string nq = NormalizeMCQQuestionText(qText);
                                    if (!string.IsNullOrEmpty(nq))
                                    {
                                        existingQuestions.Add(nq);
                                    }
                                }
                            }
                        }
                    }

                    // 3. Insert unique questions inside transaction
                    using (SqlTransaction tran = con.BeginTransaction())
                    {
                        try
                        {
                            string insertSql = @"
                                INSERT INTO [dbo].[MCQ] 
                                    ([Question], [A], [B], [C], [D], [Answer], [Describe], [course_id])
                                VALUES 
                                    (@Question, @A, @B, @C, @D, @Answer, @Describe, @course_id);";

                            foreach (var item in rows)
                            {
                                if (string.IsNullOrWhiteSpace(item.Question))
                                {
                                    continue;
                                }

                                string nq = NormalizeMCQQuestionText(item.Question);
                                if (string.IsNullOrEmpty(nq))
                                {
                                    continue;
                                }

                                // Prevent Duplicate Question
                                if (existingQuestions.Contains(nq))
                                {
                                    duplicateCount++;
                                    continue; // Skip duplicate question!
                                }

                                // Track in set so intra-file duplicates are also prevented
                                existingQuestions.Add(nq);

                                // Standardize answer
                                string normAns = (item.Answer ?? "A").Trim().ToUpper();
                                if (normAns == "1") normAns = "A";
                                else if (normAns == "2") normAns = "B";
                                else if (normAns == "3") normAns = "C";
                                else if (normAns == "4") normAns = "D";

                                if (normAns != "A" && normAns != "B" && normAns != "C" && normAns != "D")
                                {
                                    string optA = item.A ?? item.OptionA ?? "";
                                    string optB = item.B ?? item.OptionB ?? "";
                                    string optC = item.C ?? item.OptionC ?? "";
                                    string optD = item.D ?? item.OptionD ?? "";

                                    if (!string.IsNullOrWhiteSpace(optA) && string.Equals(normAns, optA.Trim(), StringComparison.OrdinalIgnoreCase)) normAns = "A";
                                    else if (!string.IsNullOrWhiteSpace(optB) && string.Equals(normAns, optB.Trim(), StringComparison.OrdinalIgnoreCase)) normAns = "B";
                                    else if (!string.IsNullOrWhiteSpace(optC) && string.Equals(normAns, optC.Trim(), StringComparison.OrdinalIgnoreCase)) normAns = "C";
                                    else if (!string.IsNullOrWhiteSpace(optD) && string.Equals(normAns, optD.Trim(), StringComparison.OrdinalIgnoreCase)) normAns = "D";
                                    else normAns = "A";
                                }

                                using (SqlCommand cmd = new SqlCommand(insertSql, con, tran))
                                {
                                    cmd.Parameters.AddWithValue("@Question", item.Question.Trim());
                                    cmd.Parameters.AddWithValue("@A", (item.A ?? item.OptionA ?? "").Trim());
                                    cmd.Parameters.AddWithValue("@B", (item.B ?? item.OptionB ?? "").Trim());
                                    cmd.Parameters.AddWithValue("@C", (object)(item.C ?? item.OptionC) ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@D", (object)(item.D ?? item.OptionD) ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Answer", normAns);
                                    cmd.Parameters.AddWithValue("@Describe", (object)item.Describe ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@course_id", course_id);
                                    cmd.ExecuteNonQuery();
                                    savedCount++;
                                }
                            }

                            tran.Commit();
                        }
                        catch (Exception ex)
                        {
                            tran.Rollback();
                            return Json(new { Status = false, Message = "Database error while saving MCQ questions: " + ex.Message });
                        }
                    }
                }

                string responseMsg;
                if (savedCount > 0 && duplicateCount > 0)
                {
                    responseMsg = $"Successfully imported and saved {savedCount} questions into database. ({duplicateCount} duplicate questions skipped)";
                }
                else if (savedCount > 0)
                {
                    responseMsg = $"Successfully imported and saved {savedCount} MCQ questions into database.";
                }
                else if (duplicateCount > 0)
                {
                    responseMsg = $"All {duplicateCount} questions from the Excel file already exist for this course (duplicate questions skipped). No new questions inserted.";
                }
                else
                {
                    responseMsg = "No valid questions were found to import.";
                }

                return Json(new
                {
                    Status = savedCount > 0 || (duplicateCount > 0 && rows.Count > 0),
                    Message = responseMsg,
                    SavedCount = savedCount,
                    DuplicateCount = duplicateCount,
                    TotalCount = savedCount,
                    CourseID = course_id
                });
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = "Error saving Excel questions: " + ex.Message });
            }
        }
        #endregion
        #endregion
        #region Student Reminders (Birthday & Remaining Fees)

        public class StudentBirthdayReminderItem
        {
            public int StudentID { get; set; }
            public string StudentName { get; set; }
            public string EnrollmentNo { get; set; }
            public string CourseTitle { get; set; }
            public string Mobile { get; set; }
            public string Email { get; set; }
            public string Gender { get; set; }
            public string DOBFormatted { get; set; }
            public int Age { get; set; }
            public int DaysDiff { get; set; }
            public string StatusText { get; set; }
            public string StatusBadgeClass { get; set; }
            public string Photo { get; set; }
        }

        public class StudentRemainingFeeReminderItem
        {
            public int StudentID { get; set; }
            public int FeesInstallmentID { get; set; }
            public string StudentName { get; set; }
            public string EnrollmentNo { get; set; }
            public string CourseTitle { get; set; }
            public string Mobile { get; set; }
            public string Email { get; set; }
            public decimal TotalFee { get; set; }
            public decimal TotalPaid { get; set; }
            public decimal BalanceFee { get; set; }
            public decimal InstallmentAmount { get; set; }
            public string InstallmentTitle { get; set; }
            public string DueDateFormatted { get; set; }
            public int DaysDiff { get; set; }
            public string StatusText { get; set; }
            public string StatusBadgeClass { get; set; }
            public string Photo { get; set; }
        }

        [HttpGet]
        public ActionResult StudentBirthdayReminders()
        {
            ViewData["PageTitle"] = "Student Birthday Reminders";
            return View();
        }

        [HttpGet]
        public ActionResult StudentFeesReminders()
        {
            ViewData["PageTitle"] = "Remaining Fees Reminders";
            return View();
        }

        [HttpGet]
        public ActionResult StudentReminders(string tab = "birthday")
        {
            if (string.Equals(tab, "fees", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("StudentFeesReminders");
            }
            return RedirectToAction("StudentBirthdayReminders");
        }

        private List<StudentBirthdayReminderItem> FetchBirthdayReminders(string filter = "7days")
        {
            List<StudentBirthdayReminderItem> list = new List<StudentBirthdayReminderItem>();

            int branchId = 0;
            if (Request.Cookies["BranchID"] != null) int.TryParse(Request.Cookies["BranchID"].Value, out branchId);
            if (branchId == 0 && Session["BranchID"] != null) int.TryParse(Session["BranchID"].ToString(), out branchId);

            string branchCond = (branchId > 0) ? " AND s.BranchID = @BranchID " : "";

            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();
                string sql = $@"
                    SELECT s.StudentID, s.StudentFullName, s.StudentEnrollmentNo, s.StudentStringEnrollmentNo,
                           s.StudentMobileNo, s.WhatsAppNo, s.StudentEmail, s.StudentPhoto, s.DOB, s.Gender,
                           (SELECT TOP 1 c.CourseTitle 
                            FROM StudentRegistrationCourse src 
                            JOIN CourseMaster c ON src.CourseID = c.CourseID 
                            WHERE src.StudentID = s.StudentID) AS CourseTitle
                    FROM StudentMaster s
                    WHERE s.DOB IS NOT NULL AND s.IsActive = 1 {branchCond}
                    ORDER BY s.StudentFullName ASC;";

                DataTable dt = new DataTable();
                using (SqlCommand cmd = new SqlCommand(sql, con))
                {
                    if (branchId > 0) cmd.Parameters.AddWithValue("@BranchID", branchId);
                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }

                DateTime today = DateTime.Today;

                foreach (DataRow row in dt.Rows)
                {
                    DateTime dob = Convert.ToDateTime(row["DOB"]);
                    DateTime bdayThisYear = new DateTime(today.Year, dob.Month, Math.Min(dob.Day, DateTime.DaysInMonth(today.Year, dob.Month)));
                    int daysDiff = (bdayThisYear - today).Days;

                    if (daysDiff < -180)
                    {
                        DateTime nextYearBday = new DateTime(today.Year + 1, dob.Month, Math.Min(dob.Day, DateTime.DaysInMonth(today.Year + 1, dob.Month)));
                        daysDiff = (nextYearBday - today).Days;
                    }
                    else if (daysDiff > 180)
                    {
                        DateTime prevYearBday = new DateTime(today.Year - 1, dob.Month, Math.Min(dob.Day, DateTime.DaysInMonth(today.Year - 1, dob.Month)));
                        daysDiff = (prevYearBday - today).Days;
                    }

                    bool match = false;
                    string statusText = "";
                    string statusBadgeClass = "";

                    if (daysDiff == 0)
                    {
                        statusText = "Today 🎉";
                        statusBadgeClass = "bg-danger";
                        if (filter == "today" || filter == "7days" || filter == "upcoming7" || filter == "all") match = true;
                    }
                    else if (daysDiff > 0 && daysDiff <= 7)
                    {
                        statusText = $"In {daysDiff} day{(daysDiff > 1 ? "s" : "")}";
                        statusBadgeClass = "bg-warning text-dark";
                        if (filter == "upcoming7" || filter == "7days" || filter == "all") match = true;
                    }
                    else if (daysDiff < 0 && daysDiff >= -7)
                    {
                        int ago = Math.Abs(daysDiff);
                        statusText = $"{ago} day{(ago > 1 ? "s" : "")} ago";
                        statusBadgeClass = "bg-secondary";
                        if (filter == "ago7" || filter == "7days" || filter == "all") match = true;
                    }
                    else if (filter == "all")
                    {
                        statusText = dob.ToString("dd MMM");
                        statusBadgeClass = "bg-light text-dark border";
                        match = true;
                    }

                    if (!match) continue;

                    int age = today.Year - dob.Year;
                    if (today < dob.AddYears(age)) age--;
                    if (age < 0) age = 0;

                    string photo = row["StudentPhoto"] != DBNull.Value ? row["StudentPhoto"].ToString() : "";
                    if (string.IsNullOrWhiteSpace(photo)) photo = "/Content/Admin/assets/images/no-image.png";
                    else if (!photo.StartsWith("/") && !photo.StartsWith("http")) photo = "/Content/Upload/StudentPhoto/" + photo;

                    string mobile = row["WhatsAppNo"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["WhatsAppNo"].ToString())
                        ? row["WhatsAppNo"].ToString()
                        : (row["StudentMobileNo"] != DBNull.Value ? row["StudentMobileNo"].ToString() : "");

                    string gender = row["Gender"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["Gender"].ToString()) ? row["Gender"].ToString() : "N/A";

                    list.Add(new StudentBirthdayReminderItem
                    {
                        StudentID = Convert.ToInt32(row["StudentID"]),
                        StudentName = row["StudentFullName"].ToString(),
                        EnrollmentNo = row["StudentStringEnrollmentNo"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["StudentStringEnrollmentNo"].ToString())
                            ? row["StudentStringEnrollmentNo"].ToString()
                            : (row["StudentEnrollmentNo"] != DBNull.Value ? row["StudentEnrollmentNo"].ToString() : ""),
                        CourseTitle = row["CourseTitle"] != DBNull.Value ? row["CourseTitle"].ToString() : "N/A",
                        Mobile = mobile,
                        Email = row["StudentEmail"] != DBNull.Value ? row["StudentEmail"].ToString() : "",
                        Gender = gender,
                        DOBFormatted = dob.ToString("dd MMM yyyy"),
                        Age = age,
                        DaysDiff = daysDiff,
                        StatusText = statusText,
                        StatusBadgeClass = statusBadgeClass,
                        Photo = photo
                    });
                }
            }

            return list.OrderBy(x => x.DaysDiff >= 0 ? x.DaysDiff : (100 + Math.Abs(x.DaysDiff))).ToList();
        }

        private List<StudentRemainingFeeReminderItem> FetchRemainingFeesReminders(string filter = "7days")
        {
            List<StudentRemainingFeeReminderItem> list = new List<StudentRemainingFeeReminderItem>();

            int branchId = 0;
            if (Request.Cookies["BranchID"] != null) int.TryParse(Request.Cookies["BranchID"].Value, out branchId);
            if (branchId == 0 && Session["BranchID"] != null) int.TryParse(Session["BranchID"].ToString(), out branchId);

            string branchCond = (branchId > 0) ? " AND s.BranchID = @BranchID " : "";

            using (SqlConnection con = new SqlConnection(CS))
            {
                con.Open();
                string sql = $@"
                    SELECT 
                        s.StudentID, s.StudentFullName, s.StudentEnrollmentNo, s.StudentStringEnrollmentNo,
                        s.StudentMobileNo, s.WhatsAppNo, s.StudentEmail, s.StudentPhoto,
                        (SELECT TOP 1 c.CourseTitle 
                         FROM StudentRegistrationCourse src 
                         JOIN CourseMaster c ON src.CourseID = c.CourseID 
                         WHERE src.StudentID = s.StudentID) AS CourseTitle,
                        ISNULL((SELECT SUM(src.FinalFee) FROM StudentRegistrationCourse src WHERE src.StudentID = s.StudentID), 0) AS TotalFee,
                        ISNULL((SELECT SUM(r.AmountReceived) FROM StudentFeeReceipt r WHERE r.StudentID = s.StudentID), 0) AS TotalPaid,
                        fi.FeesInstallmentID,
                        fi.FeesDate AS DueDate,
                        fi.FeesAmount AS InstallmentAmount,
                        ft.FeesTypeTitle
                    FROM FeesInstallmentMaster fi
                    JOIN StudentMaster s ON fi.StudentID = s.StudentID
                    LEFT JOIN FeesTypeMaster ft ON fi.FeesTypeID = ft.FeesTypeID
                    WHERE s.IsActive = 1 {branchCond}
                    ORDER BY fi.FeesDate ASC, s.StudentFullName ASC;";

                DataTable dt = new DataTable();
                using (SqlCommand cmd = new SqlCommand(sql, con))
                {
                    if (branchId > 0) cmd.Parameters.AddWithValue("@BranchID", branchId);
                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }

                DateTime today = DateTime.Today;

                foreach (DataRow row in dt.Rows)
                {
                    decimal totalFee = Convert.ToDecimal(row["TotalFee"]);
                    decimal totalPaid = Convert.ToDecimal(row["TotalPaid"]);
                    decimal balance = totalFee - totalPaid;

                    if (balance <= 0) continue;

                    DateTime? dueDate = row["DueDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["DueDate"]) : null;
                    if (!dueDate.HasValue) continue;

                    int daysDiff = (dueDate.Value.Date - today).Days;

                    bool match = false;
                    string statusText = "";
                    string statusBadgeClass = "";

                    if (daysDiff == 0)
                    {
                        statusText = "Due Today";
                        statusBadgeClass = "bg-danger text-white";
                        if (filter == "today" || filter == "7days" || filter == "all") match = true;
                    }
                    else if (daysDiff < 0 && daysDiff >= -7)
                    {
                        int ago = Math.Abs(daysDiff);
                        statusText = $"Due {ago} day{(ago > 1 ? "s" : "")} ago";
                        statusBadgeClass = "bg-danger text-white";
                        if (filter == "ago7" || filter == "7days" || filter == "all") match = true;
                    }
                    else if (daysDiff < -7)
                    {
                        int ago = Math.Abs(daysDiff);
                        statusText = $"Overdue ({ago} days ago)";
                        statusBadgeClass = "bg-danger-subtle text-danger border border-danger";
                        if (filter == "overdue" || filter == "all") match = true;
                    }
                    else if (daysDiff > 0 && daysDiff <= 7)
                    {
                        statusText = $"Due in {daysDiff} day{(daysDiff > 1 ? "s" : "")}";
                        statusBadgeClass = "bg-warning text-dark";
                        if (filter == "upcoming7" || filter == "7days" || filter == "all") match = true;
                    }
                    else if (filter == "all")
                    {
                        statusText = dueDate.Value.ToString("dd MMM yyyy");
                        statusBadgeClass = "bg-light text-dark border";
                        match = true;
                    }

                    if (!match) continue;

                    string photo = row["StudentPhoto"] != DBNull.Value ? row["StudentPhoto"].ToString() : "";
                    if (string.IsNullOrWhiteSpace(photo)) photo = "/Content/Admin/assets/images/no-image.png";
                    else if (!photo.StartsWith("/") && !photo.StartsWith("http")) photo = "/Content/Upload/StudentPhoto/" + photo;

                    string mobile = row["WhatsAppNo"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["WhatsAppNo"].ToString())
                        ? row["WhatsAppNo"].ToString()
                        : (row["StudentMobileNo"] != DBNull.Value ? row["StudentMobileNo"].ToString() : "");

                    list.Add(new StudentRemainingFeeReminderItem
                    {
                        StudentID = Convert.ToInt32(row["StudentID"]),
                        FeesInstallmentID = Convert.ToInt32(row["FeesInstallmentID"]),
                        StudentName = row["StudentFullName"].ToString(),
                        EnrollmentNo = row["StudentStringEnrollmentNo"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["StudentStringEnrollmentNo"].ToString())
                            ? row["StudentStringEnrollmentNo"].ToString()
                            : (row["StudentEnrollmentNo"] != DBNull.Value ? row["StudentEnrollmentNo"].ToString() : ""),
                        CourseTitle = row["CourseTitle"] != DBNull.Value ? row["CourseTitle"].ToString() : "N/A",
                        Mobile = mobile,
                        Email = row["StudentEmail"] != DBNull.Value ? row["StudentEmail"].ToString() : "",
                        TotalFee = totalFee,
                        TotalPaid = totalPaid,
                        BalanceFee = balance,
                        InstallmentAmount = Convert.ToDecimal(row["InstallmentAmount"]),
                        InstallmentTitle = row["FeesTypeTitle"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["FeesTypeTitle"].ToString()) ? row["FeesTypeTitle"].ToString() : "Installment",
                        DueDateFormatted = dueDate.Value.ToString("dd MMM yyyy"),
                        DaysDiff = daysDiff,
                        StatusText = statusText,
                        StatusBadgeClass = statusBadgeClass,
                        Photo = photo
                    });
                }
            }

            return list;
        }

        [HttpGet]
        public JsonResult GetHeaderReminderCounts()
        {
            try
            {
                var bdays = FetchBirthdayReminders("7days");
                var fees = FetchRemainingFeesReminders("7days");

                return Json(new
                {
                    Status = true,
                    BirthdayCount = bdays.Count,
                    FeesDueCount = fees.Count,
                    BirthdayList = bdays.Take(5).Select(b => new
                    {
                        b.StudentID,
                        b.StudentName,
                        b.Age,
                        b.Gender,
                        b.DOBFormatted,
                        b.StatusText,
                        b.StatusBadgeClass,
                        b.CourseTitle,
                        b.Photo
                    }).ToList(),
                    FeesList = fees.Take(5).Select(f => new
                    {
                        f.StudentID,
                        f.FeesInstallmentID,
                        f.StudentName,
                        f.CourseTitle,
                        f.BalanceFee,
                        f.DueDateFormatted,
                        f.StatusText,
                        f.StatusBadgeClass,
                        f.Photo
                    }).ToList()
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, BirthdayCount = 0, FeesDueCount = 0, BirthdayList = new List<object>(), FeesList = new List<object>(), Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetBirthdayRemindersList(string filter = "7days")
        {
            try
            {
                var list = FetchBirthdayReminders(filter);
                return Json(new { Status = true, Data = list, Total = list.Count }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetRemainingFeesRemindersList(string filter = "7days")
        {
            try
            {
                var list = FetchRemainingFeesReminders(filter);
                return Json(new { Status = true, Data = list, Total = list.Count }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion
        #region Practical Exam PDF Upload

        [HttpPost]
        public JsonResult UploadPracticalExamPdf(int course_id, string description = "", bool isActive = true)
        {
            try
            {
                if (course_id <= 0)
                {
                    return Json(new { Status = false, Message = "Please select a valid Course." });
                }

                if (Request.Files.Count == 0 || Request.Files[0] == null || Request.Files[0].ContentLength == 0)
                {
                    return Json(new { Status = false, Message = "Please select a PDF file to upload." });
                }

                HttpPostedFileBase file = Request.Files[0];
                string ext = System.IO.Path.GetExtension(file.FileName).ToLower();
                if (ext != ".pdf")
                {
                    return Json(new { Status = false, Message = "Only PDF (.pdf) files are allowed." });
                }

                if (file.ContentLength > 30 * 1024 * 1024)
                {
                    return Json(new { Status = false, Message = "File size exceeds the 30MB limit." });
                }

                string uploadDir = Server.MapPath("~/Uploads/PracticalPDF/");
                if (!System.IO.Directory.Exists(uploadDir))
                {
                    System.IO.Directory.CreateDirectory(uploadDir);
                }

                string safeOriginalName = System.IO.Path.GetFileNameWithoutExtension(file.FileName);
                if (safeOriginalName != null)
                {
                    safeOriginalName = System.Text.RegularExpressions.Regex.Replace(safeOriginalName, @"[^a-zA-Z0-9_\-]", "_");
                    if (safeOriginalName.Length > 40) safeOriginalName = safeOriginalName.Substring(0, 40);
                }
                else
                {
                    safeOriginalName = "Exam";
                }

                string savedFileName = string.Format("Practical_{0}_{1}_{2}.pdf", course_id, DateTime.Now.ToString("yyyyMMdd_HHmmss"), Guid.NewGuid().ToString().Substring(0, 6));
                string fullPhysicalPath = System.IO.Path.Combine(uploadDir, savedFileName);
                file.SaveAs(fullPhysicalPath);

                string relativeFilePath = "/Uploads/PracticalPDF/" + savedFileName;

                int currentUserId = 1;
                if (Session["UserID"] != null)
                {
                    int.TryParse(Session["UserID"].ToString(), out currentUserId);
                    if (currentUserId <= 0) currentUserId = 1;
                }

                int newId = 0;
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    string ensureSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PracticalExamMaster')
                        BEGIN
                            CREATE TABLE [dbo].[PracticalExamMaster] (
                                [PracticalExamID] INT IDENTITY(1,1) PRIMARY KEY,
                                [PracticalExampdf] NVARCHAR(MAX) NOT NULL,
                                [PracticalExamDescription] NVARCHAR(MAX) NULL,
                                [course_id] INT NOT NULL,
                                [isActive] BIT NOT NULL DEFAULT 1,
                                [CreatedDateTime] DATETIME NOT NULL DEFAULT GETDATE(),
                                [CreatedByUserID] INT NOT NULL DEFAULT 1
                            );
                        END
                        ELSE
                        BEGIN
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[PracticalExamMaster]') AND name = 'isActive')
                                ALTER TABLE [dbo].[PracticalExamMaster] ADD [isActive] BIT NOT NULL DEFAULT 1;
                        END";
                    using (SqlCommand cmdEnsure = new SqlCommand(ensureSql, con))
                    {
                        cmdEnsure.ExecuteNonQuery();
                    }

                    string insertSql = @"
                        INSERT INTO [dbo].[PracticalExamMaster]
                        ([PracticalExampdf], [PracticalExamDescription], [course_id], [isActive], [CreatedDateTime], [CreatedByUserID])
                        VALUES
                        (@PracticalExampdf, @PracticalExamDescription, @course_id, @isActive, GETDATE(), @CreatedByUserID);
                        SELECT SCOPE_IDENTITY();";

                    using (SqlCommand cmd = new SqlCommand(insertSql, con))
                    {
                        cmd.Parameters.AddWithValue("@PracticalExampdf", relativeFilePath);
                        cmd.Parameters.AddWithValue("@PracticalExamDescription", (object)description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@course_id", course_id);
                        cmd.Parameters.AddWithValue("@isActive", isActive ? 1 : 0);
                        cmd.Parameters.AddWithValue("@CreatedByUserID", currentUserId);

                        object resId = cmd.ExecuteScalar();
                        if (resId != null && int.TryParse(resId.ToString(), out int parsedId))
                        {
                            newId = parsedId;
                        }
                    }
                }

                return Json(new
                {
                    Status = true,
                    Message = "Practical PDF uploaded and saved successfully!",
                    PracticalExamID = newId,
                    FileName = savedFileName,
                    FilePath = relativeFilePath,
                    isActive = isActive
                });
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = "Error saving PDF: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetPracticalExamList(int course_id)
        {
            try
            {
                if (course_id <= 0)
                {
                    return Json(new { Status = false, Message = "Invalid Course ID." }, JsonRequestBehavior.AllowGet);
                }

                List<object> list = new List<object>();

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    string checkSql = "SELECT COUNT(*) FROM sys.tables WHERE name = 'PracticalExamMaster'";
                    using (SqlCommand cmdCheck = new SqlCommand(checkSql, con))
                    {
                        int exists = Convert.ToInt32(cmdCheck.ExecuteScalar());
                        if (exists == 0)
                        {
                            return Json(new { Status = true, Data = list, TotalCount = 0 }, JsonRequestBehavior.AllowGet);
                        }
                    }

                    string checkColSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[PracticalExamMaster]') AND name = 'isActive')
                            ALTER TABLE [dbo].[PracticalExamMaster] ADD [isActive] BIT NOT NULL DEFAULT 1;";
                    using (SqlCommand cmdCol = new SqlCommand(checkColSql, con))
                    {
                        cmdCol.ExecuteNonQuery();
                    }

                    string query = @"
                        SELECT 
                            [PracticalExamID],
                            [PracticalExampdf],
                            [PracticalExamDescription],
                            [course_id],
                            ISNULL([isActive], 1) AS [isActive],
                            [CreatedDateTime],
                            [CreatedByUserID]
                        FROM [dbo].[PracticalExamMaster]
                        WHERE [course_id] = @course_id
                        ORDER BY [PracticalExamID] DESC;";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@course_id", course_id);
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            int sr = 1;
                            while (rdr.Read())
                            {
                                string pdfPath = rdr["PracticalExampdf"] != DBNull.Value ? rdr["PracticalExampdf"].ToString() : "";
                                string originalName = System.IO.Path.GetFileName(pdfPath);
                                string desc = rdr["PracticalExamDescription"] != DBNull.Value ? rdr["PracticalExamDescription"].ToString() : "";
                                bool active = rdr["isActive"] != DBNull.Value && (Convert.ToString(rdr["isActive"]) == "1" || Convert.ToString(rdr["isActive"]).ToLower() == "true");
                                DateTime cDate = rdr["CreatedDateTime"] != DBNull.Value ? Convert.ToDateTime(rdr["CreatedDateTime"]) : DateTime.Now;

                                list.Add(new
                                {
                                    SrNo = sr++,
                                    PracticalExamID = Convert.ToInt32(rdr["PracticalExamID"]),
                                    PracticalExampdf = pdfPath,
                                    FileName = originalName,
                                    PracticalExamDescription = desc,
                                    CourseID = Convert.ToInt32(rdr["course_id"]),
                                    isActive = active,
                                    CreatedDateFormatted = cDate.ToString("dd MMM yyyy, hh:mm tt")
                                });
                            }
                        }
                    }
                }

                return Json(new { Status = true, Data = list, TotalCount = list.Count }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public JsonResult TogglePracticalExamStatus(int practicalExamID, bool isActive = true)
        {
            try
            {
                if (practicalExamID <= 0)
                {
                    return Json(new { Status = false, Message = "Invalid Practical Exam ID." }, JsonRequestBehavior.AllowGet);
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    string checkColSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[PracticalExamMaster]') AND name = 'isActive')
                            ALTER TABLE [dbo].[PracticalExamMaster] ADD [isActive] BIT NOT NULL DEFAULT 1;";
                    using (SqlCommand cmdCol = new SqlCommand(checkColSql, con))
                    {
                        cmdCol.ExecuteNonQuery();
                    }

                    string updateSql = "UPDATE [dbo].[PracticalExamMaster] SET [isActive] = @isActive WHERE [PracticalExamID] = @PracticalExamID;";
                    using (SqlCommand cmd = new SqlCommand(updateSql, con))
                    {
                        cmd.Parameters.AddWithValue("@isActive", isActive ? 1 : 0);
                        cmd.Parameters.AddWithValue("@PracticalExamID", practicalExamID);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { Status = true, Message = "Status updated successfully!" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult DeletePracticalExam(int practicalExamID)
        {
            try
            {
                if (practicalExamID <= 0)
                {
                    return Json(new { Status = false, Message = "Invalid ID." });
                }

                string pdfPath = "";
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    string selSql = "SELECT [PracticalExampdf] FROM [dbo].[PracticalExamMaster] WHERE [PracticalExamID] = @PracticalExamID;";
                    using (SqlCommand cmdSel = new SqlCommand(selSql, con))
                    {
                        cmdSel.Parameters.AddWithValue("@PracticalExamID", practicalExamID);
                        object res = cmdSel.ExecuteScalar();
                        if (res != null) pdfPath = res.ToString();
                    }

                    string delSql = "DELETE FROM [dbo].[PracticalExamMaster] WHERE [PracticalExamID] = @PracticalExamID;";
                    using (SqlCommand cmdDel = new SqlCommand(delSql, con))
                    {
                        cmdDel.Parameters.AddWithValue("@PracticalExamID", practicalExamID);
                        cmdDel.ExecuteNonQuery();
                    }
                }

                if (!string.IsNullOrWhiteSpace(pdfPath))
                {
                    try
                    {
                        string fullPath = Server.MapPath("~" + pdfPath);
                        if (System.IO.File.Exists(fullPath))
                        {
                            System.IO.File.Delete(fullPath);
                        }
                    }
                    catch { }
                }

                return Json(new { Status = true, Message = "Practical PDF removed successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message });
            }
        }

        #endregion
        #region Hall Ticket
        [HttpGet]
        public ActionResult ViewHallTicketExam(int page = 1, int pageSize = 10)
        {
            List<HallTicketExamViewModel> list = new List<HallTicketExamViewModel>();
            int totalRecords = 0;
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            int cookieBranchId = 0;
            if (Request.Cookies["BranchID"] != null && !string.IsNullOrEmpty(Request.Cookies["BranchID"].Value))
            {
                int.TryParse(Request.Cookies["BranchID"].Value, out cookieBranchId);
            }

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(CS))
                {
                    sqlConnection.Open();
                    string cmdText = @"
                        WITH cte AS 
                        (
                            SELECT ht.HallTicketId, ht.ScheduleExamId, ht.StudentID, ISNULL(sd.BranchID, ht.BranchID) AS BranchID,
                                   ROW_NUMBER() OVER(PARTITION BY ht.ScheduleExamId, ISNULL(sd.BranchID, ht.BranchID) ORDER BY ht.HallTicketId DESC) AS rn 
                            FROM hallticketMaster ht
                            LEFT JOIN studentMaster sd ON sd.StudentID = ht.StudentID
                            WHERE (@BranchID = 0 OR ISNULL(sd.BranchID, ht.BranchID) = @BranchID)
                        ),
                        b AS 
                        (
                            SELECT COUNT(*) AS Total, ht.ScheduleExamId, ISNULL(sd.BranchID, ht.BranchID) AS BranchID,
                                   SE.ExamScheduleDate, C.CourseTitle, SE.PracticalExamTime, SE.TheoryExamTime 
                            FROM hallticketMaster ht 
                            LEFT JOIN studentMaster sd ON sd.StudentID = ht.StudentID
                            LEFT JOIN ScheduleExamMaster SE ON se.ScheduleExamId = HT.ScheduleExamId 
                            LEFT JOIN courseMaster c ON C.CourseID = SE.course_id 
                            WHERE (@BranchID = 0 OR ISNULL(sd.BranchID, ht.BranchID) = @BranchID)
                            GROUP BY SE.ExamScheduleDate, C.CourseTitle, HT.ScheduleExamId, SE.PracticalExamTime, SE.TheoryExamTime, ISNULL(sd.BranchID, ht.BranchID)
                        ) 
                        SELECT ISNULL(b.ExamScheduleDate, '') AS ExamScheduleDate, ISNULL(b.CourseTitle, '') AS CourseTitle, b.Total, 
                               cte.StudentID, cte.ScheduleExamId, b.BranchID, BRN.BranchName, BRN.BranchCode, 
                               b.PracticalExamTime, b.TheoryExamTime, COUNT(*) OVER() AS TotalRecords
                        FROM cte 
                        INNER JOIN b ON cte.ScheduleExamId = b.ScheduleExamId AND cte.rn = 1 AND cte.BranchID = b.BranchID
                        LEFT JOIN branchMaster BRN ON BRN.BranchID = b.BranchID 
                        ORDER BY cte.ScheduleExamId DESC, BRN.BranchName ASC
                        OFFSET (@PageNumber - 1) * @PageSize ROWS
                        FETCH NEXT @PageSize ROWS ONLY;";

                    using (SqlCommand sqlCommand = new SqlCommand(cmdText, sqlConnection))
                    {
                        sqlCommand.Parameters.AddWithValue("@PageNumber", page);
                        sqlCommand.Parameters.AddWithValue("@PageSize", pageSize);
                        sqlCommand.Parameters.AddWithValue("@BranchID", cookieBranchId);

                        using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
                        {
                            int num = (page - 1) * pageSize + 1;
                            while (sqlDataReader.Read())
                            {
                                if (totalRecords == 0 && sqlDataReader["TotalRecords"] != DBNull.Value)
                                {
                                    totalRecords = Convert.ToInt32(sqlDataReader["TotalRecords"]);
                                }

                                string branch = sqlDataReader["BranchName"] != DBNull.Value ? sqlDataReader["BranchName"].ToString() : "";
                                string course = sqlDataReader["CourseTitle"] != DBNull.Value ? sqlDataReader["CourseTitle"].ToString() : "";
                                string examDate = sqlDataReader["ExamScheduleDate"] != DBNull.Value ? sqlDataReader["ExamScheduleDate"].ToString() : "";
                                if (!string.IsNullOrEmpty(examDate) && DateTime.TryParse(examDate, out DateTime dtParsed))
                                {
                                    examDate = dtParsed.ToString("yyyy-MM-dd");
                                }

                                list.Add(new HallTicketExamViewModel
                                {
                                    No = num++,
                                    Schedule_Exam_Id = ((sqlDataReader["ScheduleExamId"] != DBNull.Value) ? Convert.ToInt32(sqlDataReader["ScheduleExamId"]) : 0),
                                    branch_id = ((sqlDataReader["BranchID"] != DBNull.Value) ? new int?(Convert.ToInt32(sqlDataReader["BranchID"])) : ((int?)null)),
                                    Branch = branch,
                                    branch_name = branch,
                                    branch_code = ((sqlDataReader["BranchCode"] != DBNull.Value) ? sqlDataReader["BranchCode"].ToString() : ""),
                                    Exam_schedule_Date = examDate,
                                    Course = course,
                                    course_title = course,
                                    Practical_Exam_Time = ((sqlDataReader["PracticalExamTime"] != DBNull.Value && !string.IsNullOrWhiteSpace(sqlDataReader["PracticalExamTime"].ToString())) ? sqlDataReader["PracticalExamTime"].ToString() : "-"),
                                    Theory_Exam_Time = ((sqlDataReader["TheoryExamTime"] != DBNull.Value && !string.IsNullOrWhiteSpace(sqlDataReader["TheoryExamTime"].ToString())) ? sqlDataReader["TheoryExamTime"].ToString() : "-"),
                                    total = ((sqlDataReader["Total"] != DBNull.Value) ? sqlDataReader["Total"].ToString() : "0")
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.TotalPages = (int)Math.Ceiling((double)(totalRecords > 0 ? totalRecords : list.Count) / pageSize);
            ViewBag.ActiveBranchId = cookieBranchId;

            return View(list);
        }
        [HttpPost]
        public JsonResult GetExamHallTicketStudentDetails(int Schedule_Exam_Id, int? branchId)
        {
            try
            {
                int bId = branchId ?? 0;
                if (bId <= 0 && Request.Cookies["BranchID"] != null && !string.IsNullOrEmpty(Request.Cookies["BranchID"].Value))
                {
                    int.TryParse(Request.Cookies["BranchID"].Value, out bId);
                }

                var students = new List<object>();
                using (SqlConnection sqlConnection = new SqlConnection(CS))
                {
                    sqlConnection.Open();
                    string sql = @"
                        SELECT 
                            HT.ScheduleExamId,
                            HT.StudentID,
                            ISNULL(SD.BranchID, HT.BranchID) AS BranchID,
                            ISNULL(SD.StudentFullName, '') AS student_full_name,
                            ISNULL(SD.Gender, '') AS student_gender,
                            CASE 
                                WHEN SD.StudentStringEnrollmentNo IS NOT NULL AND RTRIM(LTRIM(SD.StudentStringEnrollmentNo)) <> '' THEN SD.StudentStringEnrollmentNo 
                                ELSE CAST(ISNULL(SD.StudentEnrollmentNo, SD.StudentID) AS NVARCHAR(50)) 
                            END AS student_String_En_No,
                            ISNULL(SD.StudentMobileNo, '') AS student_mobile_no
                        FROM HallTicketMaster HT
                        INNER JOIN StudentMaster SD ON SD.StudentID = HT.StudentID
                        WHERE HT.ScheduleExamId = @ScheduleExamId
                          AND (@BranchID = 0 OR ISNULL(SD.BranchID, HT.BranchID) = @BranchID)
                        ORDER BY student_full_name ASC";

                    using (SqlCommand cmd = new SqlCommand(sql, sqlConnection))
                    {
                        cmd.Parameters.AddWithValue("@ScheduleExamId", Schedule_Exam_Id);
                        cmd.Parameters.AddWithValue("@BranchID", bId);
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                students.Add(new
                                {
                                    student_id = Convert.ToInt32(dr["StudentID"]),
                                    student_full_name = dr["student_full_name"].ToString(),
                                    student_gender = dr["student_gender"].ToString(),
                                    student_String_En_No = dr["student_String_En_No"].ToString(),
                                    student_mobile_no = dr["student_mobile_no"].ToString()
                                });
                            }
                        }
                    }
                }

                return Json(new { data = students });
            }
            catch (Exception ex)
            {
                return Json(new { data = new List<object>(), message = ex.Message });
            }
        }
        [HttpGet]
        public async Task<ActionResult> HallTicketGenerate()
        {
            try
            {
                string adminId = GetAdminId();
                if (string.IsNullOrEmpty(adminId))
                {
                    return RedirectToAction("Index", "bctechadmin");
                }

                ViewBag.Title = "Generate Hall Ticket";
                ViewData["PageTitle"] = "Generate Hall Ticket";
                ViewData["ParentMenu"] = "Student Exam";

                DateTime utcTime = DateTime.UtcNow;
                TimeZoneInfo myZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                DateTime custDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, myZone);

                string connStr = System.Configuration.ConfigurationManager.ConnectionStrings["con"].ConnectionString;

                string adminCategory = "Admin";
                int userBranchId = 0;
                DataTable dts = new DataTable();

                using (System.Data.SqlClient.SqlConnection con = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    con.Open();                    
                        using (System.Data.SqlClient.SqlCommand cmdUser = new System.Data.SqlClient.SqlCommand("SELECT * FROM UserMaster WHERE UserID = @AdminId", con))
                        {
                            cmdUser.Parameters.Add("@AdminId", SqlDbType.VarChar, 100).Value = adminId;
                            using (System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(cmdUser))
                            {
                                da.Fill(dts);
                            }
                        }                    

                    if (dts.Rows.Count > 0)
                    {
                      if (dts.Columns.Contains("UserCategory") && dts.Rows[0]["UserCategory"] != DBNull.Value)
                            adminCategory = dts.Rows[0]["UserCategory"].ToString();

                        if (dts.Columns.Contains("BranchID") && dts.Rows[0]["BranchID"] != DBNull.Value)
                            int.TryParse(dts.Rows[0]["BranchID"].ToString(), out userBranchId);

                        if (dts.Columns.Contains("admin_name") && dts.Rows[0]["admin_name"] != DBNull.Value)
                            ViewBag.adminname = dts.Rows[0]["admin_name"].ToString();
                        else if (dts.Columns.Contains("FirstName") && dts.Rows[0]["FirstName"] != DBNull.Value)
                            ViewBag.adminname = (dts.Rows[0]["FirstName"].ToString() + " " + (dts.Columns.Contains("LastName") ? dts.Rows[0]["LastName"].ToString() : "")).Trim();

                        if (dts.Columns.Contains("admin_photo") && dts.Rows[0]["admin_photo"] != DBNull.Value)
                            ViewBag.adminimage = dts.Rows[0]["admin_photo"].ToString();
                    }

                    ViewBag.profileMenu = (adminCategory == "Admin") ? "display:block;" : "display:none;";
                    this.notification();
                    ViewBag.success = "display:none;";
                    ViewBag.error = "display:none;";

                    // 1. Resolve Branch ID from Cookie / User
                    int cookieBranchId = 0;
                    if (Request.Cookies["BranchID"] != null && !string.IsNullOrEmpty(Request.Cookies["BranchID"].Value))
                    {
                        int.TryParse(Request.Cookies["BranchID"].Value, out cookieBranchId);
                    }
                    if (cookieBranchId == 0 && Session["BranchID"] != null)
                    {
                        int.TryParse(Session["BranchID"].ToString(), out cookieBranchId);
                    }
                    if (cookieBranchId == 0 && userBranchId > 0)
                    {
                        cookieBranchId = userBranchId;
                    }

                    string cookieBranchName = "";
                    if (Request.Cookies["BranchName"] != null && !string.IsNullOrEmpty(Request.Cookies["BranchName"].Value))
                    {
                        try { cookieBranchName = System.Web.HttpUtility.UrlDecode(Request.Cookies["BranchName"].Value); }
                        catch { cookieBranchName = Request.Cookies["BranchName"].Value; }
                    }

                    // Branch Dropdown List
                    List<SelectListItem> listitemBranch = new List<SelectListItem>();
                    listitemBranch.Add(new SelectListItem() { Value = "0", Text = "Select Branch" });

                    DataTable ddBranch = new DataTable();
                    using (System.Data.SqlClient.SqlCommand cmdBranch = new System.Data.SqlClient.SqlCommand("SELECT BranchID, BranchName FROM BranchMaster WHERE ISNULL(IsActive, 1) = 1 ORDER BY BranchName ASC", con))
                    using (System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(cmdBranch))
                    {
                        da.Fill(ddBranch);
                    }

                    if (ddBranch.Rows.Count == 0)
                    {
                        using (System.Data.SqlClient.SqlCommand cmdTblBranch = new System.Data.SqlClient.SqlCommand("SELECT branch_id AS BranchID, branch_name AS BranchName FROM tbl_branch ORDER BY branch_name ASC", con))
                        using (System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(cmdTblBranch))
                        {
                            da.Fill(ddBranch);
                        }
                    }

                    string matchedBranchValue = "0";
                    for (int i = 0; i < ddBranch.Rows.Count; i++)
                    {
                        string bIdStr = ddBranch.Rows[i]["BranchID"].ToString();
                        string bNameStr = ddBranch.Rows[i]["BranchName"].ToString();
                        bool isMatch = false;

                        if (cookieBranchId > 0 && bIdStr == cookieBranchId.ToString())
                        {
                            isMatch = true;
                        }
                        else if (cookieBranchId == 0 && !string.IsNullOrEmpty(cookieBranchName) && bNameStr.Trim().Equals(cookieBranchName.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            isMatch = true;
                            int.TryParse(bIdStr, out cookieBranchId);
                        }

                        if (isMatch)
                        {
                            matchedBranchValue = bIdStr;
                        }

                        listitemBranch.Add(new SelectListItem()
                        {
                            Value = bIdStr,
                            Text = bNameStr,
                            Selected = isMatch
                        });
                    }
                    ViewBag.DropDownValueBranch = new SelectList(listitemBranch, "Value", "Text", matchedBranchValue != "0" ? matchedBranchValue : null);
                    ViewBag.SelectedBranchId = matchedBranchValue;

                    // Schedule Exam Dropdown List
                    List<SelectListItem> listitem = new List<SelectListItem>();
                    listitem.Add(new SelectListItem() { Value = "0", Text = "Select Schedule Exam" });

                    string examQuery = @"
                                SELECT se.ScheduleExamId, se.ExamScheduleDate, se.PracticalExamTime, se.TheoryExamTime, C.CourseTitle 
                                FROM ScheduleExamMaster SE 
                                LEFT JOIN CourseMaster C ON C.CourseID = SE.course_id 
                                Where se.ExamScheduleDate >= ' " + custDateTime + "' ORDER BY se.ScheduleExamId DESC";

                    DataTable dd = new DataTable();
                    using (System.Data.SqlClient.SqlCommand cmdExam = new System.Data.SqlClient.SqlCommand(examQuery, con))
                    using (System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(cmdExam))
                    {
                        da.Fill(dd);
                    }

                    for (int i = 0; i < dd.Rows.Count; i++)
                    {
                        string sId = dd.Rows[i]["ScheduleExamId"].ToString();
                        string cTitle = dd.Rows[i]["CourseTitle"] != DBNull.Value ? dd.Rows[i]["CourseTitle"].ToString() : "Exam";
                        string eDate = "";
                        if (dd.Rows[i]["ExamScheduleDate"] != DBNull.Value && DateTime.TryParse(dd.Rows[i]["ExamScheduleDate"].ToString(), out DateTime dtParsed))
                        {
                            eDate = dtParsed.ToString("yyyy/MM/dd");
                        }
                        string pTime = dd.Rows[i]["PracticalExamTime"] != DBNull.Value ? dd.Rows[i]["PracticalExamTime"].ToString() : "";

                        string title = cTitle;
                        if (!string.IsNullOrEmpty(eDate)) title += " - " + eDate;
                        if (!string.IsNullOrEmpty(pTime)) title += " - " + pTime;

                        listitem.Add(new SelectListItem() { Value = sId, Text = title });
                    }
                    ViewBag.DropDownValue = new SelectList(listitem, "Value", "Text");
                }
            }
            catch (Exception ex)
            {
                ViewBag.error = "display:block;";
                ViewBag.errorText = ex.Message;
            }

            return View();
        }
        [HttpPost]
        public JsonResult GetScheduleExamsByBranch(int branchId)
        {
            try
            {
                var exams = new List<object>();
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string query = @"select SE.*, C.CourseTitle from ScheduleExamMaster SE LEFT JOIN CourseMaster C ON C.CourseID = se.course_id order by se.ScheduleExamId desc";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@BranchId", branchId);
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                string sId = dr["ScheduleExamId"].ToString();
                                string cTitle = dr["CourseTitle"] != DBNull.Value ? dr["CourseTitle"].ToString() : "Exam";
                                string eDate = "";
                                if (dr["ExamScheduleDate"] != DBNull.Value && DateTime.TryParse(dr["ExamScheduleDate"].ToString(), out DateTime dtParsed))
                                {
                                    eDate = dtParsed.ToString("yyyy/MM/dd");
                                }
                                string pTime = dr["PracticalExamTime"] != DBNull.Value ? dr["PracticalExamTime"].ToString() : "";

                                string title = cTitle;
                                if (!string.IsNullOrEmpty(eDate)) title += " - " + eDate;
                                if (!string.IsNullOrEmpty(pTime)) title += " - " + pTime;

                                exams.Add(new { id = sId, text = title });
                            }
                        }
                    }
                }

                return Json(new { status = true, exams = exams });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
        [HttpPost]
        public JsonResult GetStudentsForHallTicket(int scheduleExamId, int branchId)
        {
            try
            {
                if (scheduleExamId <= 0)
                {
                    return Json(new { status = false, message = "Please select a valid Schedule Exam." });
                }

                // If branchId is not passed from UI, check cookie
                if (branchId <= 0 && Request.Cookies["BranchID"] != null)
                {
                    int.TryParse(Request.Cookies["BranchID"].Value, out branchId);
                }

                int courseId = 0;
                int secureCode = 0;
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    string getExamInfo = "SELECT course_id, SecureCode FROM ScheduleExamMaster WHERE ScheduleExamId = @ScheduleExamId";
                    using (SqlCommand cmdExam = new SqlCommand(getExamInfo, con))
                    {
                        cmdExam.Parameters.AddWithValue("@ScheduleExamId", scheduleExamId);
                        using (SqlDataReader drExam = cmdExam.ExecuteReader())
                        {
                            if (drExam.Read())
                            {
                                courseId = drExam["course_id"] != DBNull.Value ? Convert.ToInt32(drExam["course_id"]) : 0;
                                if (drExam["SecureCode"] != DBNull.Value)
                                {
                                    int.TryParse(drExam["SecureCode"].ToString(), out secureCode);
                                }
                            }
                        }
                    }

                    string studentQuery = @"
                        SELECT DISTINCT
                            s.StudentID,
                            ISNULL(s.StudentFullName, '') AS StudentFullName,
                            ISNULL(s.StudentStringEnrollmentNo, '') AS StudentEnrollmentNo,
                            ISNULL(s.StudentMobileNo, '') AS StudentMobileNo,
                            ISNULL(fees.TotalFee, 0) AS TotalFee,
                            ISNULL(rec.TotalPaid, 0) AS PaidFee
                        FROM StudentMaster s
                        OUTER APPLY (
                            SELECT 
                                CASE 
                                    WHEN ISNULL(SUM(FeesAmount), 0) > 0 THEN ISNULL(SUM(FeesAmount), 0)
                                    ELSE ISNULL((SELECT SUM(FinalFee) FROM StudentRegistrationCourse WHERE StudentID = s.StudentID), 0)
                                END AS TotalFee
                            FROM FeesInstallmentMaster 
                            WHERE StudentID = s.StudentID
                        ) fees
                        OUTER APPLY (
                            SELECT ISNULL(SUM(AmountReceived), 0) AS TotalPaid
                            FROM StudentFeeReceipt
                            WHERE StudentID = s.StudentID
                        ) rec
                        WHERE (
                            (@BranchId = 0 OR s.BranchID = @BranchId)
                            AND (
                                @CourseId = 0
                                OR EXISTS (SELECT 1 FROM StudentRegistrationCourse src WHERE src.StudentID = s.StudentID AND src.CourseID = @CourseId)
                                OR EXISTS (SELECT 1 FROM StudentMasterCourse smc WHERE smc.StudentID = s.StudentID AND (smc.CourseID = @CourseId OR smc.SelectCourseID = @CourseId))
                                OR EXISTS (
                                    SELECT 1 FROM MasterCourse mc 
                                    WHERE mc.SelectCourseID = @CourseId 
                                    AND (
                                        EXISTS (SELECT 1 FROM StudentRegistrationCourse src2 WHERE src2.StudentID = s.StudentID AND src2.CourseID = mc.CourseID)
                                        OR EXISTS (SELECT 1 FROM StudentMasterCourse smc2 WHERE smc2.StudentID = s.StudentID AND smc2.CourseID = mc.CourseID)
                                    )
                                )
                            )
                            AND s.StudentID NOT IN (SELECT StudentID FROM HallTicketMaster WHERE ScheduleExamId = @ScheduleExamId)
                            AND s.StudentID NOT IN (SELECT StudentID FROM ExamMaster WHERE ScheduleExamId = @ScheduleExamId)
                        )
                        ORDER BY StudentFullName ASC";

                    var students = new List<object>();
                    using (SqlCommand cmdStud = new SqlCommand(studentQuery, con))
                    {
                        cmdStud.Parameters.AddWithValue("@BranchId", branchId);
                        cmdStud.Parameters.AddWithValue("@CourseId", courseId);
                        cmdStud.Parameters.AddWithValue("@ScheduleExamId", scheduleExamId);

                        using (SqlDataReader dr = cmdStud.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                decimal totFee = dr["TotalFee"] != DBNull.Value ? Convert.ToDecimal(dr["TotalFee"]) : 0;
                                decimal pdFee = dr["PaidFee"] != DBNull.Value ? Convert.ToDecimal(dr["PaidFee"]) : 0;

                                students.Add(new
                                {
                                    studentId = Convert.ToInt32(dr["StudentID"]),
                                    studentName = dr["StudentFullName"].ToString(),
                                    enrollment = dr["StudentEnrollmentNo"].ToString(),
                                    mobileNo = dr["StudentMobileNo"].ToString(),
                                    totalFees = "₹ " + totFee.ToString("N2"),
                                    paidFees = "₹ " + pdFee.ToString("N2")
                                });
                            }
                        }
                    }

                    return Json(new
                    {
                        status = true,
                        secureCode = secureCode,
                        total = students.Count,
                        students = students
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
        [HttpPost]
        public JsonResult SaveHallTicketAjax(int scheduleExamId, int branchId, int secureCode, string studentIds)
        {
            try
            {
                if (scheduleExamId <= 0)
                {
                    return Json(new { status = false, message = "Please select a Schedule Exam." });
                }
                if (string.IsNullOrWhiteSpace(studentIds))
                {
                    return Json(new { status = false, message = "Please select at least one student." });
                }

                if (branchId <= 0 && Request.Cookies["BranchID"] != null)
                {
                    int.TryParse(Request.Cookies["BranchID"].Value, out branchId);
                }

                string[] sIdArray = studentIds.Split(new char[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (sIdArray.Length == 0)
                {
                    return Json(new { status = false, message = "No students selected." });
                }

                DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
                int savedCount = 0;

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    using (SqlTransaction trans = con.BeginTransaction())
                    {
                        try
                        {
                            foreach (string sStr in sIdArray)
                            {
                                if (int.TryParse(sStr.Trim(), out int sId) && sId > 0)
                                {
                                    string insertSql = @"
                                        IF NOT EXISTS (SELECT 1 FROM HallTicketMaster WHERE ScheduleExamId = @ScheduleExamId AND StudentID = @StudentID)
                                        BEGIN
                                            INSERT INTO HallTicketMaster (dateandtime, ScheduleExamId, StudentID, IsGivenExam, SecureCode, BranchID)
                                            VALUES (@DateAndTime, @ScheduleExamId, @StudentID, 0, @SecureCode, @BranchID)
                                        END";

                                    using (SqlCommand cmd = new SqlCommand(insertSql, con, trans))
                                    {
                                        cmd.Parameters.AddWithValue("@DateAndTime", now);
                                        cmd.Parameters.AddWithValue("@ScheduleExamId", scheduleExamId);
                                        cmd.Parameters.AddWithValue("@StudentID", sId);
                                        cmd.Parameters.AddWithValue("@SecureCode", secureCode);
                                        cmd.Parameters.AddWithValue("@BranchID", branchId);
                                        cmd.ExecuteNonQuery();
                                    }

                                    try
                                    {
                                        string mirrorSql = @"
                                            IF OBJECT_ID('tbl_hall_ticket', 'U') IS NOT NULL
                                            BEGIN
                                                IF NOT EXISTS (SELECT 1 FROM tbl_hall_ticket WHERE Schedule_Exam_Id = @ScheduleExamId AND student_id = @StudentID)
                                                BEGIN
                                                    INSERT INTO tbl_hall_ticket (dateandtime, Schedule_Exam_Id, student_id, IsGivenExam, SecureCode)
                                                    VALUES (@DateAndTime, @ScheduleExamId, @StudentID, 0, @SecureCode)
                                                END
                                            END";
                                        using (SqlCommand cmdMirror = new SqlCommand(mirrorSql, con, trans))
                                        {
                                            cmdMirror.Parameters.AddWithValue("@DateAndTime", now);
                                            cmdMirror.Parameters.AddWithValue("@ScheduleExamId", scheduleExamId);
                                            cmdMirror.Parameters.AddWithValue("@StudentID", sId);
                                            cmdMirror.Parameters.AddWithValue("@SecureCode", secureCode);
                                            cmdMirror.ExecuteNonQuery();
                                        }
                                    }
                                    catch { }

                                    savedCount++;
                                }
                            }

                            trans.Commit();
                        }
                        catch
                        {
                            trans.Rollback();
                            throw;
                        }
                    }
                }

                return Json(new
                {
                    status = true,
                    savedCount = savedCount,
                    message = "Record save successfully.",
                    redirectUrl = "/bctechAdmin/ViewHallTicketExam"
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
        [HttpPost]
        public async Task<ActionResult> HallTicketGenerate(HallTicket iList, string submit)
        {
            if (submit == "view" && !string.IsNullOrEmpty(iList.all_Student_id))
            {
                string[] sIdArray = iList.all_Student_id.Split(new char[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
                DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    foreach (string sStr in sIdArray)
                    {
                        if (int.TryParse(sStr.Trim(), out int sId) && sId > 0)
                        {
                            string insertSql = @"
                                IF NOT EXISTS (SELECT 1 FROM HallTicketMaster WHERE ScheduleExamId = @ScheduleExamId AND StudentID = @StudentID)
                                BEGIN
                                    INSERT INTO HallTicketMaster (dateandtime, ScheduleExamId, StudentID, IsGivenExam, SecureCode, BranchID)
                                    VALUES (@DateAndTime, @ScheduleExamId, @StudentID, 0, @SecureCode, @BranchID)
                                END";

                            using (SqlCommand cmd = new SqlCommand(insertSql, con))
                            {
                                cmd.Parameters.AddWithValue("@DateAndTime", now);
                                cmd.Parameters.AddWithValue("@ScheduleExamId", iList.Schedule_Exam_Id);
                                cmd.Parameters.AddWithValue("@StudentID", sId);
                                cmd.Parameters.AddWithValue("@SecureCode", iList.SecureCode);
                                cmd.Parameters.AddWithValue("@BranchID", iList.branch_id);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
                return Redirect("/bctechAdmin/ViewHallTicketExam/");
            }

            return await HallTicketGenerate();
        }
        [HttpGet]
        public async Task<ActionResult> ReExamHallTicketGenerate()
        {
            try
            {
                DateTime utcTime = DateTime.UtcNow;
                TimeZoneInfo myZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                DateTime custDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, myZone);

                if (Request.Cookies["admin_id"] == null)
                {
                    return Redirect("/bctechAdmin/");
                }

                string adminId = Request.Cookies["admin_id"].Value.Contains("=")
                    ? Request.Cookies["admin_id"].Value.Split('=')[1]
                    : Request.Cookies["admin_id"].Value;

                DataTable dts = new DataTable();

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    using (SqlCommand cmdUser = new SqlCommand("SELECT * FROM UserMaster WHERE UserID = @AdminId", con))
                    {
                        cmdUser.Parameters.Add("@AdminId", SqlDbType.VarChar, 100).Value = adminId;
                        using (SqlDataAdapter da = new SqlDataAdapter(cmdUser))
                        {
                            da.Fill(dts);
                        }
                    }

                    if (dts.Rows.Count > 0)
                    {
                        if (dts.Columns.Contains("admin_name") && dts.Rows[0]["admin_name"] != DBNull.Value)
                            ViewBag.adminname = dts.Rows[0]["admin_name"].ToString();
                        else if (dts.Columns.Contains("FirstName") && dts.Rows[0]["FirstName"] != DBNull.Value)
                            ViewBag.adminname = (dts.Rows[0]["FirstName"].ToString() + " " + (dts.Columns.Contains("LastName") && dts.Rows[0]["LastName"] != DBNull.Value ? dts.Rows[0]["LastName"].ToString() : "")).Trim();

                        if (dts.Columns.Contains("admin_photo") && dts.Rows[0]["admin_photo"] != DBNull.Value)
                            ViewBag.adminimage = dts.Rows[0]["admin_photo"].ToString();

                        string category = (dts.Columns.Contains("UserCategory") && dts.Rows[0]["UserCategory"] != DBNull.Value)
                            ? dts.Rows[0]["UserCategory"].ToString()
                            : ((dts.Columns.Contains("admin_category") && dts.Rows[0]["admin_category"] != DBNull.Value) ? dts.Rows[0]["admin_category"].ToString() : "");

                        ViewBag.profileMenu = (category == "Admin") ? "display:block;" : "display:none;";
                    }

                    this.notification();

                    ViewBag.success = "display:none;";
                    ViewBag.error = "display:none;";

                    string branchCookieVal = Request.Cookies["BranchID"] != null ? Request.Cookies["BranchID"].Value : "0";
                    ViewBag.SelectedBranchId = branchCookieVal;

                    // Schedule Exam Dropdown List
                    List<SelectListItem> listitem = new List<SelectListItem>();
                    listitem.Add(new SelectListItem() { Value = "0", Text = "Select Schedule Exam" });

                    string examQuery = @"
                        SELECT se.*, C.CourseTitle
                        FROM ScheduleExamMaster SE
                        LEFT JOIN CourseMaster C ON C.CourseID = SE.course_id
                        WHERE SE.ExamScheduleDate >= @ExamDate
                        ORDER BY se.ScheduleExamId DESC";

                    using (SqlCommand cmdExam = new SqlCommand(examQuery, con))
                    {
                        cmdExam.Parameters.Add("@ExamDate", SqlDbType.Date).Value = custDateTime.Date;
                        using (SqlDataAdapter da = new SqlDataAdapter(cmdExam))
                        {
                            DataTable dd = new DataTable();
                            da.Fill(dd);
                            for (int i = 0; i < dd.Rows.Count; i++)
                            {
                                string sId = dd.Rows[i]["ScheduleExamId"].ToString();
                                string cTitle = dd.Rows[i]["CourseTitle"] != DBNull.Value ? dd.Rows[i]["CourseTitle"].ToString() : "Exam";
                                string eDate = "";
                                if (dd.Rows[i]["ExamScheduleDate"] != DBNull.Value && DateTime.TryParse(dd.Rows[i]["ExamScheduleDate"].ToString(), out DateTime dtParsed))
                                {
                                    eDate = dtParsed.ToString("yyyy/MM/dd");
                                }
                                string pTime = dd.Rows[i]["PracticalExamTime"] != DBNull.Value ? dd.Rows[i]["PracticalExamTime"].ToString() : "";

                                string title = cTitle;
                                if (!string.IsNullOrEmpty(eDate)) title += " - " + eDate;
                                if (!string.IsNullOrEmpty(pTime)) title += " - " + pTime;

                                listitem.Add(new SelectListItem() { Value = sId, Text = title });
                            }
                        }
                    }
                    ViewBag.DropDownValue = new SelectList(listitem, "Value", "Text");
                }
            }
            catch (Exception ex)
            {
                ViewBag.error = "display:block;";
                ViewBag.errorText = ex.Message;
            }

            return View();
        }

        [HttpPost]
        public JsonResult GetPreviousExamDates(int scheduleExamId, int branchId = 0)
        {
            try
            {
                if (scheduleExamId <= 0)
                {
                    return Json(new { status = false, message = "Invalid schedule exam ID", dates = new List<string>() });
                }

                List<string> dates = new List<string>();
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    int courseId = 0;
                    string getCourse = "SELECT course_id FROM ScheduleExamMaster WHERE ScheduleExamId = @ScheduleExamId";
                    using (SqlCommand cmdC = new SqlCommand(getCourse, con))
                    {
                        cmdC.Parameters.AddWithValue("@ScheduleExamId", scheduleExamId);
                        object cObj = cmdC.ExecuteScalar();
                        if (cObj != null && cObj != DBNull.Value)
                        {
                            int.TryParse(cObj.ToString(), out courseId);
                        }
                    }

                    if (courseId > 0)
                    {
                        string queryDates = @"
                            SELECT DISTINCT CONVERT(VARCHAR(10), se.ExamScheduleDate, 120) AS ExamDate
                            FROM ScheduleExamMaster se WHERE se.course_id = @CourseId AND se.ScheduleExamId != @ScheduleExamId
                              AND se.ExamScheduleDate IS NOT NULL  ORDER BY ExamDate DESC";
                        using (SqlCommand cmdD = new SqlCommand(queryDates, con))
                        {
                            cmdD.Parameters.AddWithValue("@CourseId", courseId);
                            cmdD.Parameters.AddWithValue("@ScheduleExamId", scheduleExamId);
                            using (SqlDataReader dr = cmdD.ExecuteReader())
                            {
                                while (dr.Read())
                                {
                                    if (dr["ExamDate"] != DBNull.Value)
                                    {
                                        dates.Add(dr["ExamDate"].ToString());
                                    }
                                }
                            }
                        }

                        if (dates.Count == 0)
                        {
                            try
                            {
                                string legacyQuery = @"
                                    IF OBJECT_ID('ScheduleExamMaster', 'U') IS NOT NULL
                                    BEGIN
                                        SELECT DISTINCT CONVERT(VARCHAR(10), ExamScheduleDate, 120) AS ExamDate
                                        FROM ScheduleExamMaster
                                        WHERE course_id = @CourseId
                                          AND ScheduleExamId != @ScheduleExamId
                                          AND ExamScheduleDate IS NOT NULL
                                        ORDER BY ExamDate DESC
                                    END";
                                using (SqlCommand cmdLeg = new SqlCommand(legacyQuery, con))
                                {
                                    cmdLeg.Parameters.AddWithValue("@CourseId", courseId);
                                    cmdLeg.Parameters.AddWithValue("@ScheduleExamId", scheduleExamId);
                                    using (SqlDataReader drLeg = cmdLeg.ExecuteReader())
                                    {
                                        while (drLeg.Read())
                                        {
                                            if (drLeg["ExamDate"] != DBNull.Value)
                                            {
                                                string dStr = drLeg["ExamDate"].ToString();
                                                if (!dates.Contains(dStr)) dates.Add(dStr);
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }

                return Json(new { status = true, dates = dates, data = dates });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message, dates = new List<string>() });
            }
        }
        [HttpPost]
        public JsonResult GetStudentsForReExam(int scheduleExamId, string previousExamDate, int branchId = 0)
        {
            try
            {
                if (scheduleExamId <= 0)
                {
                    return Json(new { status = false, message = "Please select a Schedule Exam." });
                }

                if (string.IsNullOrWhiteSpace(previousExamDate) || previousExamDate == "0")
                {
                    return Json(new { status = false, message = "Please select a Previous Exam Date." });
                }

                if (branchId <= 0 && Request.Cookies["BranchID"] != null)
                {
                    int.TryParse(Request.Cookies["BranchID"].Value, out branchId);
                }

                var students = new List<object>();
                string strbody = "";
                string studid = "";
                int no = 0;
                int PrevScheduleExamId = 0;

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    DataTable dt = new DataTable();
                    using (SqlCommand cmd1 = new SqlCommand("select * from ScheduleExamMaster where ScheduleExamId = @ScheduleExamId", con))
                    {
                        cmd1.Parameters.AddWithValue("@ScheduleExamId", scheduleExamId);
                        using (SqlDataAdapter da1 = new SqlDataAdapter(cmd1))
                        {
                            da1.Fill(dt);
                        }
                    }

                    if (dt.Rows.Count > 0)
                    {
                        DataTable dtSE = new DataTable();
                        using (SqlCommand cmd2 = new SqlCommand("select * from ScheduleExamMaster where course_id = @CourseId and ExamScheduleDate = @PrevDate", con))
                        {
                            cmd2.Parameters.AddWithValue("@CourseId", dt.Rows[0]["course_id"].ToString());
                            cmd2.Parameters.AddWithValue("@PrevDate", Convert.ToDateTime(previousExamDate).ToString("yyyy-MM-dd"));
                            using (SqlDataAdapter da2 = new SqlDataAdapter(cmd2))
                            {
                                da2.Fill(dtSE);
                            }
                        }

                        for (int j = 0; j < dtSE.Rows.Count; j++)
                        {
                            int.TryParse(dtSE.Rows[j]["ScheduleExamId"].ToString(), out PrevScheduleExamId);

                            DataTable dts = new DataTable();
                            using (SqlCommand cmd3 = new SqlCommand("SELECT ScheduleExamId, STUFF((SELECT ', ' + CAST(StudentID AS VARCHAR(10)) [text()] FROM HallTicketMaster WHERE ScheduleExamId = t.ScheduleExamId and t.ScheduleExamId = @ScheduleExamId FOR XML PATH(''), TYPE) .value('.', 'NVARCHAR(MAX)'),1,2,' ') List_Output FROM HallTicketMaster t where t.ScheduleExamId = @ScheduleExamId GROUP BY ScheduleExamId", con))
                            {
                                cmd3.Parameters.AddWithValue("@ScheduleExamId", scheduleExamId);
                                using (SqlDataAdapter da3 = new SqlDataAdapter(cmd3))
                                {
                                    da3.Fill(dts);
                                }
                            }

                            string excludeIds = dts.Rows.Count > 0 ? dts.Rows[0]["List_Output"].ToString().Trim() : "";

                            DataTable dtt = new DataTable();
                            if (!string.IsNullOrEmpty(excludeIds))
                            {
                                using (SqlCommand cmd4 = new SqlCommand("select SD.* from HallTicketMaster HT LEFT JOIN StudentMaster SD ON SD.StudentID = HT.StudentID where HT.ScheduleExamId = @SubScheduleExamId and SD.StudentID not in (" + excludeIds + ")", con))
                                {
                                    cmd4.Parameters.AddWithValue("@SubScheduleExamId", PrevScheduleExamId);
                                    using (SqlDataAdapter da4 = new SqlDataAdapter(cmd4))
                                    {
                                        da4.Fill(dtt);
                                    }
                                }
                            }
                            else
                            {
                                using (SqlCommand cmd5 = new SqlCommand("select SD.* from HallTicketMaster HT LEFT JOIN StudentMaster SD ON SD.StudentID = HT.StudentID where HT.ScheduleExamId = @SubScheduleExamId", con))
                                {
                                    cmd5.Parameters.AddWithValue("@SubScheduleExamId", PrevScheduleExamId);
                                    using (SqlDataAdapter da5 = new SqlDataAdapter(cmd5))
                                    {
                                        da5.Fill(dtt);
                                    }
                                }
                            }

                            if (dtt.Rows.Count > 0)
                            {
                                for (int i = 0; i < dtt.Rows.Count; i++)
                                {
                                    no = no + 1;
                                    string sIdStr = dtt.Rows[i]["StudentID"].ToString();
                                    string sName = dtt.Rows[i]["StudentFullName"].ToString();
                                    string sEnroll = dtt.Rows[i]["StudentStringEnrollmentNo"].ToString();
                                    string sMobile = dtt.Rows[i]["StudentMobileNo"].ToString();

                                    strbody += "<tr>";
                                    strbody += "<td>  <input type='checkbox' id='basic_checkbox_" + no + "' name='" + no + "' class='filled-in' value='" + sIdStr + "' onclick='SelectCheck(this.value)' /> <label for='basic_checkbox_" + no + "'> </label> </td>";
                                    strbody += "<td>" + sName + "</td>";
                                    strbody += "<td>" + sEnroll + "</td>";
                                    strbody += "<td>" + sMobile + "</td>";
                                    strbody += "</tr>";
                                    studid += sIdStr + "|";

                                    int sIdInt = 0;
                                    int.TryParse(sIdStr, out sIdInt);

                                    students.Add(new
                                    {
                                        studentId = sIdInt,
                                        studentName = sName,
                                        enrollment = sEnroll,
                                        mobileNo = sMobile,
                                        totalFees = "₹ 0.00",
                                        paidFees = "₹ 0.00"
                                    });
                                }
                            }
                        }
                    }
                }

                ViewBag.studtable = strbody;
                ViewBag.studid = studid;

                return Json(new
                {
                    status = true,
                    prevScheduleExamId = PrevScheduleExamId,
                    total = students.Count,
                    students = students,
                    studtable = strbody,
                    studid = studid
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult> ReExamHallTicketGenerate(HallTicket iList, string submit)
        {
            if (submit == "view" && !string.IsNullOrEmpty(iList.all_Student_id))
            {
                string[] sIdArray = iList.all_Student_id.Split(new char[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
                DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();
                    foreach (string sStr in sIdArray)
                    {
                        if (int.TryParse(sStr.Trim(), out int sId) && sId > 0)
                        {
                            string insertSql = @"
                                IF NOT EXISTS (SELECT 1 FROM HallTicketMaster WHERE ScheduleExamId = @ScheduleExamId AND StudentID = @StudentID)
                                BEGIN
                                    INSERT INTO HallTicketMaster (dateandtime, ScheduleExamId, StudentID, IsGivenExam, SecureCode, BranchID)
                                    VALUES (@DateAndTime, @ScheduleExamId, @StudentID, 0, @SecureCode, @BranchID)
                                END";

                            using (SqlCommand cmd = new SqlCommand(insertSql, con))
                            {
                                cmd.Parameters.AddWithValue("@DateAndTime", now);
                                cmd.Parameters.AddWithValue("@ScheduleExamId", iList.Schedule_Exam_Id);
                                cmd.Parameters.AddWithValue("@StudentID", sId);
                                cmd.Parameters.AddWithValue("@SecureCode", iList.SecureCode);
                                cmd.Parameters.AddWithValue("@BranchID", iList.branch_id);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
                return Redirect("/bctechAdmin/ViewHallTicketExam/");
            }

            return await ReExamHallTicketGenerate();
        }
        public bool InsertHallTicket(HallTicket iList)
        {
            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(CS))
                {
                    sqlConnection.Open();
                    string cmdText = @"
                DECLARE @bId INT = @branch_id;

                -- Get BranchID from StudentMaster if not supplied
                IF (ISNULL(@bId, 0) = 0)
                BEGIN
                    SELECT TOP 1 @bId = BranchID
                    FROM StudentMaster
                    WHERE StudentID = @student_id;
                END;

                -- Resolve SecureCode: use passed @SecureCode, or fetch from ScheduleExamMaster
                DECLARE @secCode INT = @SecureCode;
                IF (ISNULL(@secCode, 0) = 0)
                BEGIN
                    SELECT TOP 1 @secCode = CASE 
                        WHEN ISNUMERIC(SecureCode) = 1 THEN CAST(SecureCode AS INT) 
                        ELSE 0 
                    END
                    FROM ScheduleExamMaster
                    WHERE ScheduleExamId = @Schedule_Exam_Id;
                END;

                -- Prevent duplicate hall tickets
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM HallTicketMaster
                    WHERE ScheduleExamId = @Schedule_Exam_Id
                      AND StudentID = @student_id
                )
                BEGIN
                    INSERT INTO HallTicketMaster
                    (
                        ScheduleExamId,
                        StudentID,
                        BranchID,
                        SecureCode,
                        dateandtime
                    )
                    VALUES
                    (
                        @Schedule_Exam_Id,
                        @student_id,
                        @bId,
                        ISNULL(@secCode, 0),
                        GETDATE()
                    );
                END
                ELSE
                BEGIN
                    -- If hall ticket exists but SecureCode is 0, update it
                    UPDATE HallTicketMaster
                    SET SecureCode = ISNULL(@secCode, SecureCode)
                    WHERE ScheduleExamId = @Schedule_Exam_Id
                      AND StudentID = @student_id
                      AND (ISNULL(SecureCode, 0) = 0);
                END;
            ";
                    using (SqlCommand sqlCommand = new SqlCommand(cmdText, sqlConnection))
                    {
                        sqlCommand.Parameters.Add("@Schedule_Exam_Id", SqlDbType.Int).Value = iList.Schedule_Exam_Id;
                        sqlCommand.Parameters.Add("@student_id", SqlDbType.Int).Value = iList.student_id;
                        sqlCommand.Parameters.Add("@branch_id", SqlDbType.Int).Value = iList.branch_id;
                        sqlCommand.Parameters.Add("@SecureCode", SqlDbType.Int).Value = iList.SecureCode;
                        int num = sqlCommand.ExecuteNonQuery();
                        return num > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("InsertHallTicket Error: " + ex.ToString());
                return false;
            }
        }
        [HttpGet]
        public ActionResult HallTicketDelete(int id)
        {
            DeleteHallTicket(id);
            return Redirect("/bctechAdmin/ViewHallTicketExam");
        }
        public bool DeleteHallTicket(int id)
        {
            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(CS))
                {
                    sqlConnection.Open();
                    string cmdText = @"
                        IF OBJECT_ID('HallTicketMaster', 'U') IS NOT NULL
                        BEGIN
                            DECLARE @colExam NVARCHAR(50) = CASE WHEN COL_LENGTH('HallTicketMaster', 'ScheduleExamId') IS NOT NULL THEN 'ScheduleExamId' ELSE 'Schedule_Exam_Id' END;
                            DECLARE @sql NVARCHAR(MAX) = N'DELETE FROM HallTicketMaster WHERE ' + @colExam + N' = @id';
                            EXEC sp_executesql @sql, N'@id INT', @id = @id;
                        END
                        IF OBJECT_ID('tbl_hall_ticket', 'U') IS NOT NULL
                        BEGIN
                            DELETE FROM tbl_hall_ticket WHERE Schedule_Exam_Id = @id;
                        END";
                    using (SqlCommand sqlCommand = new SqlCommand(cmdText, sqlConnection))
                    {
                        sqlCommand.Parameters.AddWithValue("@id", id);
                        return sqlCommand.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
        [HttpGet]
        public ActionResult PrintHallTicket(int id = 0)
        {
            if (id <= 0)
            {
                if (RouteData != null && RouteData.Values["id"] != null)
                {
                    int.TryParse(RouteData.Values["id"].ToString(), out id);
                }
                if (id <= 0 && Request != null && !string.IsNullOrEmpty(Request["id"]))
                {
                    int.TryParse(Request["id"], out id);
                }
            }

            DataTable dataTable = new DataTable();
            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(CS))
                {
                    sqlConnection.Open();
                    string cmdText = @"
                        IF OBJECT_ID('HallTicketMaster', 'U') IS NOT NULL AND OBJECT_ID('StudentMaster', 'U') IS NOT NULL
                        BEGIN
                            DECLARE @colExam NVARCHAR(50) = CASE WHEN COL_LENGTH('HallTicketMaster', 'ScheduleExamId') IS NOT NULL THEN 'ScheduleExamId' ELSE 'Schedule_Exam_Id' END;
                            DECLARE @colStudent NVARCHAR(50) = CASE WHEN COL_LENGTH('HallTicketMaster', 'StudentID') IS NOT NULL THEN 'StudentID' ELSE 'student_id' END;

                            DECLARE @sql NVARCHAR(MAX) = N'
                                SELECT 
                                    ISNULL(HT.' + @colExam + N', 0) AS Schedule_Exam_Id,
                                    ISNULL(HT.' + @colStudent + N', 0) AS student_id,
                                    ISNULL(SD.StudentFullName, '''') AS student_full_name,
                                    CASE 
                                        WHEN SD.StudentStringEnrollmentNo IS NOT NULL AND RTRIM(LTRIM(SD.StudentStringEnrollmentNo)) <> '''' THEN SD.StudentStringEnrollmentNo 
                                        ELSE CAST(ISNULL(SD.StudentEnrollmentNo, SD.StudentID) AS NVARCHAR(50)) 
                                    END AS student_String_En_No,
                                    ISNULL(SD.StudentMobileNo, '''') AS student_mobile_no,
                                    ISNULL(SD.StudentPhoto, '''') AS student_photo,
                                    ISNULL(C.CourseTitle, '''') AS course_title,
                                    ISNULL(C.CourseCode, '''') AS course_code,
                                    CONVERT(VARCHAR(10), SE.ExamScheduleDate, 120) AS Exam_schedule_Date,
                                    ISNULL(SE.PracticalExamTime, ''-'') AS Practical_Exam_Time,
                                    ISNULL(SE.TheoryExamTime, ''-'') AS Theory_Exam_Time,
                                    ISNULL(B.BranchName, '''') AS branch_name
                                FROM HallTicketMaster HT
                                LEFT JOIN StudentMaster SD ON SD.StudentID = HT.' + @colStudent + N'
                                LEFT JOIN ScheduleExamMaster SE ON SE.ScheduleExamId = HT.' + @colExam + N'
                                LEFT JOIN CourseMaster C ON C.CourseID = SE.course_id
                                LEFT JOIN BranchMaster B ON B.BranchID = ISNULL(SD.BranchID, HT.BranchID)
                                WHERE HT.' + @colExam + N' = @id';

                            EXEC sp_executesql @sql, N'@id INT', @id = @id;
                        END";
                    using (SqlCommand sqlCommand = new SqlCommand(cmdText, sqlConnection))
                    {
                        sqlCommand.Parameters.AddWithValue("@id", id);
                        using (SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(sqlCommand))
                        {
                            sqlDataAdapter.Fill(dataTable);
                        }
                    }

                    if (dataTable.Rows.Count == 0)
                    {
                        string legacySql = @"
                            IF OBJECT_ID('tbl_hall_ticket', 'U') IS NOT NULL AND OBJECT_ID('tbl_student_details', 'U') IS NOT NULL
                            BEGIN
                                SELECT 
                                    ISNULL(ht.Schedule_Exam_Id, 0) AS Schedule_Exam_Id,
                                    ISNULL(ht.student_id, 0) AS student_id,
                                    ISNULL(s.student_full_name, '') AS student_full_name,
                                    ISNULL(s.student_String_En_No, '') AS student_String_En_No,
                                    ISNULL(s.student_mobile_no, '') AS student_mobile_no,
                                    ISNULL(s.student_photo, '') AS student_photo,
                                    ISNULL(c.course_title, '') AS course_title,
                                    ISNULL(c.course_code, '') AS course_code,
                                    CONVERT(VARCHAR(10), se.Exam_schedule_Date, 120) AS Exam_schedule_Date,
                                    ISNULL(se.Practical_Exam_Time, '-') AS Practical_Exam_Time,
                                    ISNULL(se.Theory_Exam_Time, '-') AS Theory_Exam_Time,
                                    ISNULL(b.branch_name, '') AS branch_name
                                FROM tbl_hall_ticket ht
                                LEFT JOIN tbl_student_details s ON s.student_id = ht.student_id
                                LEFT JOIN tbl_Schedule_Exam se ON se.Schedule_Exam_Id = ht.Schedule_Exam_Id
                                LEFT JOIN tbl_course c ON c.course_id = se.course_id
                                LEFT JOIN tbl_branch b ON b.branch_id = s.branch_id
                                WHERE ht.Schedule_Exam_Id = @id
                            END";
                        using (SqlCommand cmdLeg = new SqlCommand(legacySql, sqlConnection))
                        {
                            cmdLeg.Parameters.AddWithValue("@id", id);
                            using (SqlDataAdapter daLeg = new SqlDataAdapter(cmdLeg))
                            {
                                daLeg.Fill(dataTable);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return View(dataTable);
        }
        public bool DeleteHallTicketStudent(int studentId, int scheduleExamId)
        {
            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(CS))
                {
                    sqlConnection.Open();
                    string cmdText = @"
                        IF OBJECT_ID('HallTicketMaster', 'U') IS NOT NULL
                        BEGIN
                            DECLARE @colExam NVARCHAR(50) = CASE WHEN COL_LENGTH('HallTicketMaster', 'ScheduleExamId') IS NOT NULL THEN 'ScheduleExamId' ELSE 'Schedule_Exam_Id' END;
                            DECLARE @colStudent NVARCHAR(50) = CASE WHEN COL_LENGTH('HallTicketMaster', 'StudentID') IS NOT NULL THEN 'StudentID' ELSE 'student_id' END;
                            DECLARE @sql NVARCHAR(MAX) = N'DELETE FROM HallTicketMaster WHERE ' + @colExam + N' = @seId AND ' + @colStudent + N' = @sId';
                            EXEC sp_executesql @sql, N'@seId INT, @sId INT', @seId = @seId, @sId = @sId;
                        END";
                    using (SqlCommand sqlCommand = new SqlCommand(cmdText, sqlConnection))
                    {
                        sqlCommand.Parameters.AddWithValue("@seId", scheduleExamId);
                        sqlCommand.Parameters.AddWithValue("@sId", studentId);
                        return sqlCommand.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Manage Exam Result / Secure Code
        public List<SecureCode> GetSecureCodeList(DateTime? selectedDate = null)
        {
            DateTime utcTime = DateTime.UtcNow;
            TimeZoneInfo myZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            DateTime custDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, myZone);
            DateTime queryDate = selectedDate ?? custDateTime.Date;

            List<SecureCode> iList = new List<SecureCode>();

            try
            {
                string str = @"
                    WITH a AS (
                        SELECT SE.ScheduleExamId,
                               SE.ExamScheduleDate,
                               SE.NoOfQuestion,
                               SE.ExamTime,
                               SE.ExamTotalMarks,
                               SE.PracticalExamTime,
                               SE.TheoryExamTime,
                               C.CourseTitle,
                               C.CourseImage,
                               HT.SecureCode
                        FROM HallTicketMaster HT
                        LEFT JOIN ScheduleExamMaster SE ON SE.ScheduleExamId = HT.ScheduleExamId
                        LEFT JOIN CourseMaster C ON C.CourseID = SE.course_id
                        WHERE CAST(SE.ExamScheduleDate AS DATE) = @ExamDate
                    )
                    SELECT DISTINCT ScheduleExamId AS Schedule_Exam_Id,
                           ExamScheduleDate AS Exam_schedule_Date,
                           NoOfQuestion AS No_Of_Question,
                           ExamTime AS Exam_Time,
                           ExamTotalMarks AS Exam_Total_Marks,
                           PracticalExamTime AS Practical_Exam_Time,
                           TheoryExamTime AS Theory_Exam_Time,
                           CourseTitle AS course_title,
                           CourseImage AS course_image,
                           SecureCode
                    FROM a
                    GROUP BY ScheduleExamId,
                             ExamScheduleDate,
                             NoOfQuestion,
                             ExamTime,
                             ExamTotalMarks,
                             PracticalExamTime,
                             TheoryExamTime,
                             CourseTitle,
                             CourseImage,
                             SecureCode";

                using (SqlConnection con = new SqlConnection(CS))
                {
                    using (SqlCommand cmd = new SqlCommand(str, con))
                    {
                        cmd.Parameters.Add("@ExamDate", SqlDbType.Date).Value = queryDate.Date;
                        using (SqlDataAdapter adpter = new SqlDataAdapter(cmd))
                        {
                            DataTable dt = new DataTable();
                            con.Open();
                            adpter.Fill(dt);

                            foreach (DataRow dr in dt.Rows)
                            {
                                string cImg = dr["course_image"] != DBNull.Value ? Convert.ToString(dr["course_image"]).Trim() : "";
                                if (!string.IsNullOrEmpty(cImg) && !cImg.StartsWith("/") && !cImg.StartsWith("http"))
                                {
                                    cImg = "/Content/Upload/CourseImage/" + cImg;
                                }

                                iList.Add(new SecureCode
                                {
                                    Schedule_Exam_Id = dr["Schedule_Exam_Id"] != DBNull.Value ? Convert.ToInt32(dr["Schedule_Exam_Id"]) : 0,
                                    Exam_schedule_Date = dr["Exam_schedule_Date"] != DBNull.Value ? Convert.ToDateTime(dr["Exam_schedule_Date"]) : DateTime.MinValue,
                                    No_Of_Question = dr["No_Of_Question"] != DBNull.Value ? Convert.ToInt32(dr["No_Of_Question"]) : 0,
                                    Exam_Time = dr["Exam_Time"] != DBNull.Value ? Convert.ToInt32(dr["Exam_Time"]) : 0,
                                    Exam_Total_Marks = dr["Exam_Total_Marks"] != DBNull.Value ? Convert.ToInt32(dr["Exam_Total_Marks"]) : 0,
                                    course_title = dr["course_title"] != DBNull.Value ? Convert.ToString(dr["course_title"]) : "",
                                    course_image = cImg,
                                    Practical_Exam_Time = dr["Practical_Exam_Time"] != DBNull.Value ? Convert.ToString(dr["Practical_Exam_Time"]) : "",
                                    Theory_Exam_Time = dr["Theory_Exam_Time"] != DBNull.Value ? Convert.ToString(dr["Theory_Exam_Time"]) : "",
                                    SecureCodeNo = dr["SecureCode"] != DBNull.Value ? Convert.ToInt32(dr["SecureCode"]) : 0
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return iList;
        }
        [HttpGet]
        public ActionResult ManageExamResult(DateTime? examDate)
        {
            DateTime utcTime = DateTime.UtcNow;
            TimeZoneInfo myZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            DateTime custDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, myZone);

            DateTime filterDate = examDate ?? custDateTime.Date;
            ViewBag.SelectedDate = filterDate.ToString("yyyy-MM-dd");
            ViewBag.DisplayDate = filterDate.ToString("dd-MMM-yyyy");

            ViewBag.Title = "Manage Exam Result";
            ViewData["PageTitle"] = "Manage Exam Result";
            ViewData["ParentMenu"] = "Student Exam";

            var list = GetSecureCodeList(filterDate);
            return View(list);
        }
        [HttpGet]
        public ActionResult GenerateSecureCode(DateTime? examDate)
        {
            return RedirectToAction("ManageExamResult", new { examDate = examDate });
        }
        [HttpGet]
        public async Task<ActionResult> StudentExamination(int id)
        {
            StringBuilder strBody = new StringBuilder();
            ViewBag.StudentExam = "";

            try
            {
                var adminCookie = Request.Cookies["admin_id"];
                if (adminCookie == null || string.IsNullOrWhiteSpace(adminCookie.Value))
                {
                    return Redirect("/bctechAdmin/");
                }

                string rawCookieVal = adminCookie.Value;
                string adminIdStr = rawCookieVal.Contains("=")
                    ? rawCookieVal.Split('=')[1]
                    : rawCookieVal;

                if (!int.TryParse(adminIdStr.Trim(), out int adminId))
                {
                    return Redirect("/bctechAdmin/");
                }

                using (SqlConnection con = new SqlConnection(CS))
                {
                    con.Open();

                    // 1. Load admin user details
                    string adminSql = @"
                SELECT UserID, FirstName, LastName, Username, ProfilePhoto, UserCategory
                FROM UserMaster
                WHERE UserID = @AdminId";

                    using (SqlCommand cmdAdmin = new SqlCommand(adminSql, con))
                    {
                        cmdAdmin.Parameters.Add("@AdminId", SqlDbType.Int).Value = adminId;
                        using (SqlDataAdapter daAdmin = new SqlDataAdapter(cmdAdmin))
                        {
                            DataTable dts = new DataTable();
                            daAdmin.Fill(dts);

                            if (dts.Rows.Count > 0)
                            {
                                string fullName = (dts.Rows[0]["FirstName"] + " " + dts.Rows[0]["LastName"]).Trim();
                                ViewBag.adminname = string.IsNullOrEmpty(fullName)
                                    ? Convert.ToString(dts.Rows[0]["Username"])
                                    : fullName;
                                ViewBag.adminimage = Convert.ToString(dts.Rows[0]["ProfilePhoto"]);
                                ViewBag.profileMenu = Convert.ToString(dts.Rows[0]["UserCategory"]) == "Admin"
                                    ? "display:block;"
                                    : "display:none;";
                            }
                        }
                    }

                    // 2. Load exam & course details
                    string examSql = @"
                SELECT SE.ScheduleExamId,
                       SE.ExamScheduleDate,
                       SE.NoOfQuestion,
                       SE.ExamTime,
                       SE.ExamTotalMarks,
                       SE.PracticalExamTime,
                       SE.TheoryExamTime,
                       SE.SecureCode,
                       C.CourseTitle
                FROM ScheduleExamMaster SE
                LEFT JOIN CourseMaster C ON C.CourseID = SE.course_id
                WHERE SE.ScheduleExamId = @ScheduleExamId";

                    using (SqlCommand cmdExam = new SqlCommand(examSql, con))
                    {
                        cmdExam.Parameters.Add("@ScheduleExamId", SqlDbType.Int).Value = id;
                        using (SqlDataAdapter daExam = new SqlDataAdapter(cmdExam))
                        {
                            DataTable dt = new DataTable();
                            daExam.Fill(dt);

                            if (dt.Rows.Count > 0)
                            {
                                DataRow row = dt.Rows[0];
                                ViewBag.Exam_schedule_Date = row["ExamScheduleDate"] != DBNull.Value
                                    ? Convert.ToDateTime(row["ExamScheduleDate"]).ToString("dd-MMM-yyyy")
                                    : "-";
                                ViewBag.course_title = Convert.ToString(row["CourseTitle"]);
                                ViewBag.No_Of_Question = Convert.ToString(row["NoOfQuestion"]);
                                ViewBag.Exam_Time = Convert.ToString(row["ExamTime"]);
                                ViewBag.Exam_Total_Marks = Convert.ToString(row["ExamTotalMarks"]);
                                ViewBag.Practical_Exam_Time = Convert.ToString(row["PracticalExamTime"]);
                                ViewBag.Theory_Exam_Time = Convert.ToString(row["TheoryExamTime"]);
                                ViewBag.Schedule_Exam_Id = Convert.ToString(row["ScheduleExamId"]);
                                ViewBag.SecureCode = Convert.ToString(row["SecureCode"]);
                            }
                        }
                    }

                    // 3. Load hall tickets & enrolled students
                    string studentSql = @"
                SELECT HT.HallTicketId,HT.IsGivenExam,SM.StudentStringEnrollmentNo,SM.StudentFullName,SM.Gender,SM.StudentPhoto,
                       CASE WHEN HT.IsGivenExam = 1 THEN 'background-color: lightgreen;' WHEN HT.IsGivenExam = 2 THEN 'background-color: #caca0a73;'
                           ELSE 'background-color: #ff8787;' END AS GivenExam
                FROM HallTicketMaster HT
                LEFT JOIN StudentMaster SM ON SM.StudentID = HT.StudentID
                WHERE HT.ScheduleExamId = @ScheduleExamId
                ORDER BY HT.HallTicketId";

                    using (SqlCommand cmdStud = new SqlCommand(studentSql, con))
                    {
                        cmdStud.Parameters.Add("@ScheduleExamId", SqlDbType.Int).Value = id;
                        using (SqlDataAdapter daStud = new SqlDataAdapter(cmdStud))
                        {
                            DataTable dtS = new DataTable();
                            daStud.Fill(dtS);

                            for (int i = 0; i < dtS.Rows.Count; i++)
                            {
                                int j = i + 1;
                                DataRow sRow = dtS.Rows[i];

                                string rawPhoto = Convert.ToString(sRow["StudentPhoto"]).Trim();
                                string photo;
                                if (string.IsNullOrEmpty(rawPhoto))
                                {
                                    photo = "/Content/assets/images/no-image-available.png";
                                }
                                else if (!rawPhoto.StartsWith("/") && !rawPhoto.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                {
                                    photo = "/Content/Upload/StudentPhoto/" + rawPhoto;
                                }
                                else
                                {
                                    photo = rawPhoto;
                                }

                                string name = WebUtility.HtmlEncode(Convert.ToString(sRow["StudentFullName"]));
                                string enrollNo = WebUtility.HtmlEncode(Convert.ToString(sRow["StudentStringEnrollmentNo"]));
                                string gender = WebUtility.HtmlEncode(Convert.ToString(sRow["Gender"]));
                                string hallTicketId = Convert.ToString(sRow["HallTicketId"]);
                                string rowStyle = Convert.ToString(sRow["GivenExam"]);

                                strBody.Append("<tr style='").Append(rowStyle).Append("'>");
                                strBody.Append("<td>").Append(j).Append("</td>");
                                strBody.Append("<td><img src='").Append(WebUtility.HtmlEncode(photo))
                                       .Append("' data-at2x='").Append(WebUtility.HtmlEncode(photo))
                                       .Append("' class='border-img' alt='").Append(name)
                                       .Append("' title='").Append(name)
                                       .Append("' onerror=\"this.onerror=null;this.src='/Content/assets/images/no-image-available.png';\" ")
                                       .Append("style='height: 50px; width: 50px;'></td>");
                                strBody.Append("<td>").Append(enrollNo).Append("</td>");
                                strBody.Append("<td>").Append(name).Append("</td>");
                                strBody.Append("<td>").Append(gender).Append("</td>");
                                strBody.Append("<td><a class='fa fa-undo text-inverse fa-2x' href='/bctechAdmin/StudentExaminationUpdate/")
                                       .Append(hallTicketId)
                                       .Append("' title='Re-Exam' onclick=\"return confirm('Do you really want to Conduct Re-Examination?')\"></a></td>");
                                strBody.Append("</tr>");
                            }
                        }
                    }
                }

                ViewBag.StudentExam = strBody.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("StudentExamination error: " + ex.Message);
            }

            return await Task.FromResult(View());
        }
        #endregion
    }
}