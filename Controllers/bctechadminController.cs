using BCTECHERP.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace BCTECHERP.Controllers
{
    public class bctechadminController : Controller
    {
        string CS = ConfigurationManager.ConnectionStrings["con"].ConnectionString;
        public ActionResult Index()
        {
            return View();
        }
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
        #endregion
        #region BranchMaster
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
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["con"].ConnectionString))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand(@"
                                UPDATE BranchMaster SET IsActive = 0, EditedDateTime = GETDATE(), EditedByUserID = @UserID
                                WHERE BranchID=@BranchID", con);
                cmd.Parameters.AddWithValue("@BranchID", BranchID);
                cmd.Parameters.AddWithValue("@UserID", Session["UserID"]);
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
                    cmd.Parameters.AddWithValue("@BranchHeadUserID",model.BranchHeadUserID == 0 ? (object)DBNull.Value : model.BranchHeadUserID);
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
            return View();
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
                    obj.DateOfBirth = dr["DateOfBirth"] == DBNull.Value? (DateTime?)null: Convert.ToDateTime(dr["DateOfBirth"]);
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
                    obj.JoiningDate = dr["JoiningDate"] == DBNull.Value? (DateTime?)null : Convert.ToDateTime(dr["JoiningDate"]);
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

                using (SqlConnection con = new SqlConnection(cs))
                {
                    con.Open();

                    SqlCommand cmd = new SqlCommand(@"SELECT *  FROM UserMaster ORDER BY UserID DESC", con);
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
                            DateOfBirth = dr["DateOfBirth"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(dr["DateOfBirth"]),
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
                            JoiningDate = dr["JoiningDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(dr["JoiningDate"]),
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
            SELECT COUNT(*)  FROM CourseMaster WHERE IsActive = 1
            AND (CourseTitle LIKE @Search  OR CourseCode LIKE @Search )";
                SqlCommand countCmd = new SqlCommand(countQuery, con);
                countCmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                totalRecords = Convert.ToInt32(countCmd.ExecuteScalar());
                // Data Query
                string query = @"
            SELECT *
            FROM CourseMaster
            WHERE IsActive = 1
            AND (
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
                        CM.CourseTitle,
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
                case "E": ViewData["PageTitle"] = "Edit Course";
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
                                IsActive=@IsActive                          
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

                    if (MasterCourses != null && MasterCourses.Length > 0)
                    {
                        foreach (string item in MasterCourses)
                        {
                            using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO MasterCourse
                (
                    CourseID,
                    SelectCourseID,
                    CreatedDateTime,
                    CreatedByUserID
                )
                VALUES
                (
                    @CourseID,
                    @SelectCourseID,
                    GETDATE(),
                    @CreatedByUserID
                )", con, trans))
                            {
                                cmd.Parameters.Add("@CourseID", SqlDbType.Int).Value = CourseID;
                                cmd.Parameters.Add("@SelectCourseID", SqlDbType.Int).Value = Convert.ToInt32(item);
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

        public static string Encrypt(string text)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }
        public static string Decrypt(string cipher)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(cipher));
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
    }
}