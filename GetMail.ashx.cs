using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;
using System.Web.Configuration;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Web.Services;
using System.Web.Services.Protocols;
using System.Xml;
using System.Text;
using System.Web.Script.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using log4net;

namespace GetMail
{
    // Class for Mandrill messages
    //
    // Sample:
    //
    //	[  
    //	   {  
    //	      "event":"hard_bounce",
    //	      "_id":"795432eb5481498bb99cec7a1f267310",
    //	      "msg":{  
    //	         "ts":1445956507,
    //	         "_id":"795432eb5481498bb99cec7a1f267310",
    //	         "state":"bounced",
    //	         "subject":"Host a TIPS Workshop on Your Campus",
    //	         "email":"liz1612@hotmail.com",
    //	         "tags":[  
    //	
    //	         ],
    //	         "smtp_events":[  
    //	
    //	         ],
    //	         "resends":[  
    //	
    //	         ],
    //	         "_version":"CwFbf0_VJIdKx15h6_2sMg",
    //	         "diag":"smtp;550 Requested action not taken: mailbox unavailable (-1389492669:4198:-2147467259)\n\n--9B095B5ADSN=_01D1054E2943069E007679D0BAY004?MC1F17.ho",
    //	         "bgtools_code":10,
    //	         "sender":"estellet@gettips.com",
    //	         "template":null,
    //	         "bounce_description":"bad_mailbox"
    //	      },
    //	      "ts":1445962746
    //	   }
    //	]
    public class SmtpEvent
    {
        public int ts { get; set; }
        public DateTime SmtpTs { get; set; }
        public string type { get; set; }
        public string diag { get; set; }
        public string source_ip { get; set; }
        public string destination_ip { get; set; }
        public int size { get; set; }
        public int smtpId { get; set; } //added for datalayer
    }
    public class Msg
    {
        public int ts { get; set; }
        public string _id { get; set; }
        public string state { get; set; }
        public string subject { get; set; }
        public string email { get; set; }
        public List<object> tags { get; set; }
        public List<object> smtp_events { get; set; }
        public List<object> resends { get; set; }
        public string _version { get; set; }
        public string diag { get; set; }
        public int bgtools_code { get; set; }
        public string sender { get; set; }
        public object template { get; set; }
        public string bounce_description { get; set; }

        public Msg()
        {
            tags = new List<object>();
            smtp_events = new List<object>();
            resends = new List<object>();
        }
    }

    public class Bounce
    {
        public string @event { get; set; }
        public string _id { get; set; }
        public Msg msg { get; set; }
        public int ts { get; set; }

        public Bounce()
        {
            msg = new Msg();
        }
    }

    /// <summary>
    /// This service processes a web hook message generated from the Mandrill service
    /// Bounced/rejected email messages are processed using this function
    /// </summary>
    public class GetMail : IHttpHandler
    {
        // Globals
        System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();

        public void ProcessRequest(HttpContext context)
        {
            // This web service processes the JSON artifact provided by Mandrill
            // The parameters are as follows:
            //      HttpContext         - the JSON string

            // ============================================
            // Declarations
            //  Generic   
            string mypath = "";
            string errmsg = "";
            string temp = "";

            //  Database 
            string ConnS = "";
            string SqlS = "";
            SqlConnection con = null;
            SqlCommand cmd = null;
            SqlDataReader dr = null;
            SqlConnection con2 = null;
            SqlCommand cmd2 = null;

            //  Logging
            FileStream fs = null;
            string logfile = "";
            string Logging = "";
            DateTime dt = DateTime.Now;
            string LogStartTime = dt.ToString();
            string Debug = "N";
            bool results = false;
            int numrecs = 0;
            string VersionNum = "100";

            // Log4Net configuration
            string ltemp = "";
            log4net.Config.XmlConfigurator.Configure();
            log4net.ILog eventlog = log4net.LogManager.GetLogger("EventLog");
            log4net.ILog debuglog = log4net.LogManager.GetLogger("DebugLog");

            // Web Service 
            GetChat.com.certegrity.cloudsvc.Service wsvcs = new GetChat.com.certegrity.cloudsvc.Service();

            // Value array
            string[] EmailId = new string[500];
            string[] EmailAddress = new string[500];
            string[] EmailEvent = new string[500];
            string[] EmailError = new string[500];
            string[] EmailTime = new string[500];
            string[] EmailSubject = new string[500];
            string TempError = "";
            string TempError2 = "";
            int ctr = 0;

            // Activity variables
            string EmpId = "";
            string EmpLogin = "";
            string CON_ID = "";
            string OU_ID = "";
            string REG_NUM = "";
            string TRAINER_NUM = "";
            string ACTIVITY_ID = "";
            DateTime timestamp = DateTime.Now;

            // JSON
            var myMessages = new Bounce();
            
            // String variables
            int ii = 10;
            char crtn = (char)ii;
            ii = 13;
            char lfeed = (char)ii;
            string crlf = lfeed.ToString() + crtn.ToString();

            // ============================================
            // Debug Setup
            mypath = HttpRuntime.AppDomainAppPath;
            Logging = "Y";
            try
            {
                temp = WebConfigurationManager.AppSettings["GetMail_debug"];
                Debug = temp;
                EmpId = WebConfigurationManager.AppSettings["GetChat_EmpId"];
                if (EmpId == "") { EmpId = "1-EMN4X"; }
                EmpLogin = WebConfigurationManager.AppSettings["GetChat_EmpLogin"];
                if (EmpLogin == "") { EmpLogin = "TECHNICAL SUPPORT"; }
            }
            catch { }

            // ============================================
            // Get system defaults
            ConnectionStringSettings connSettings = ConfigurationManager.ConnectionStrings["hcidb"];
            if (connSettings != null)
            {
                ConnS = connSettings.ConnectionString;
            }
            if (ConnS == "")
            {
                ConnS = "server=";
            }

            // ============================================
            // Get the JSON object
            JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
            string jsonString = String.Empty;
            HttpContext.Current.Request.InputStream.Position = 0;
            using (StreamReader inputStream = new StreamReader(HttpContext.Current.Request.InputStream))
            {
                jsonString = inputStream.ReadToEnd();
            }

            // Check to see if object is encoded
            if (jsonString.IndexOf("%7B%") > 0)
            {
                jsonString = System.Web.HttpContext.Current.Server.UrlDecode(jsonString);
                jsonString = jsonString.Replace("mandrill_events=", "");
            }

            // ============================================
            // Store JSON in a separate file
            try
            {
                string Jsonfile = "C:\\Logs\\GetMail-JSON.log";
                if (File.Exists(Jsonfile))
                {
                    fs = new FileStream(Jsonfile, FileMode.Append, FileAccess.Write, FileShare.Write);
                }
                else
                {
                    fs = new FileStream(Jsonfile, FileMode.CreateNew, FileAccess.Write, FileShare.Write);
                }
                writeoutputfs(ref fs, jsonString);
                fs.Flush();
                fs.Close();
                fs.Dispose();
            }
            catch (Exception e)
            {
                errmsg = errmsg + "Error opening JSON log: " + e.ToString();
            }

            // ============================================
            // Open log file if applicable
            if ((Logging == "Y" & Debug != "T") | Debug == "Y")
            {
                logfile = "C:\\Logs\\GetMail.log";
                try
                {
                    log4net.GlobalContext.Properties["LogFileName"] = logfile;
                    log4net.Config.XmlConfigurator.Configure();
                }
                catch (Exception e)
                {
                    errmsg = errmsg + "Error opening debug Log: " + e.ToString();
                }

                if (Debug == "Y" & errmsg == "")
                {
                    debuglog.Debug("----------------------------------");
                    debuglog.Debug("Trace Log Started " + LogStartTime);
                    debuglog.Debug("Parameters-");
                    debuglog.Debug("  jsonString: " + jsonString);
                    debuglog.Debug(" ");
                }
            }

            // ============================================
            // Open database connections
            try
            {
                errmsg = OpenDBConnection(ref ConnS, ref con, ref cmd);
            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
                goto CloseLog;
            }
            try
            {
                errmsg = OpenDBConnection(ref ConnS, ref con2, ref cmd2);
            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
                goto CloseLog;
            }

            // ============================================
            // Extract message information
            //  Assume there is one message
            ctr = 0;
            if (Debug == "Y")
            {
                debuglog.Debug("MESSAGES: \r\n");
            }
            try
            {
                EmailAddress[0] = "";
                EmailEvent[0] = "";
                EmailTime[0] = "";
                EmailError[0] = "";

                // ============================================
                // Deserialize the string into objects
                try
                {
                    List<Bounce> MyBounces = JsonConvert.DeserializeObject<List<Bounce>>(jsonString);

                    foreach (Bounce item in MyBounces)
                    {
                        if (item._id != null)
                        {
                            EmailId[ctr] = item._id.ToString();
                        }
                        if (item.msg.state != null)
                        {
                            EmailEvent[ctr] = item.msg.state.ToString();
                        }
                        if (item.msg.email != null)
                        {
                            EmailAddress[ctr] = item.msg.email.ToString();
                        }
                        if (item.msg.subject != null)
                        {
                            EmailSubject[ctr] = item.msg.subject.ToString();
                        }
                        if (item.msg.diag != null)
                        {
                            EmailError[ctr] = item.msg.diag.ToString() + ' ' + item.msg.bounce_description.ToString();
                        }
                        else { EmailError[ctr] = "Unknown"; }
                        if (item.msg.ts != null)
                        {
                            timestamp = GetMail.ConvertFromUnixTimestamp(System.Convert.ToDouble(item.msg.ts));
                            EmailTime[ctr] = timestamp.ToString();
                        }

                        if (Debug == "Y")
                        {
                            debuglog.Debug(ctr.ToString() + "\r\n >id: " + EmailId[ctr] + "\r\n >emailAddress: " + EmailAddress[ctr] + "\r\n >EmailTime: " + EmailTime[ctr] + "\r\n >subject: " + EmailSubject[ctr] + "\r\n >error: " + EmailError[ctr]);
                        }
                        ctr = ctr + 1;
                    }
                }
                catch (Exception e2)
                {
                    errmsg = errmsg + ", Deserialize the string into objects: " + e2.ToString();
                    goto CloseLog;
                }

            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
            }

            // ============================================
            // Process email addresses
            for (int i = 0; i < ctr; i++)
            {
                if (Debug == "Y") { debuglog.Debug("\r\n....\r\nProcessing: " + i.ToString() + " - " + EmailAddress[i]); }

                if (EmailAddress[i] != "" && EmailAddress[i] != "root@bm2.gettips.com" && EmailAddress[i] != "root@bm1.gettips.com")
                {
                    SqlS = "SELECT ROW_ID, PR_DEPT_OU_ID, X_REGISTRATION_NUM, X_TRAINER_NUM " +
                    "FROM siebeldb.dbo.S_CONTACT " +
                    "WHERE LOWER(EMAIL_ADDR)='" + EmailAddress[i].ToLower() + "'";
                    if (Debug == "Y") { debuglog.Debug("\r\n Email address query: \r\n " + SqlS); }
                    try
                    {
                        cmd = new SqlCommand(SqlS, con);
                        cmd.CommandType = System.Data.CommandType.Text;
                        dr = cmd.ExecuteReader();
                        if (dr.HasRows)
                        {
                            while (dr.Read())
                            {
                                results = false;
                                if (dr[0] == DBNull.Value) { CON_ID = ""; } else { CON_ID = dr[0].ToString(); }
                                if (dr[1] == DBNull.Value) { OU_ID = ""; } else { OU_ID = dr[1].ToString(); }
                                if (dr[2] == DBNull.Value) { REG_NUM = ""; } else { REG_NUM = dr[2].ToString(); }
                                if (dr[3] == DBNull.Value) { TRAINER_NUM = ""; } else { 
                                    TRAINER_NUM = dr[3].ToString();
                                    if (TRAINER_NUM == "0") { TRAINER_NUM = ""; }
                                }
                                if (Debug == "Y")
                                {
                                    debuglog.Debug("  >CON_ID: " + CON_ID + " >OU_ID: " + OU_ID + " >REG_NUM: " + REG_NUM + " >TRAINER_NUM: " + TRAINER_NUM);
                                }

                                // Update contact record
                                if (CON_ID != "")
                                {
                                    if (REG_NUM != "" || TRAINER_NUM != "")
                                    {
                                        SqlS = "UPDATE siebeldb.dbo.S_CONTACT SET SUPPRESS_EMAIL_FLG='Y' WHERE ROW_ID='" + CON_ID + "'";
                                    }
                                    else {
                                        SqlS = "UPDATE siebeldb.dbo.S_CONTACT SET EMAIL_ADDR='' WHERE ROW_ID='" + CON_ID + "'";
                                    }
                                    if (Debug == "Y") { debuglog.Debug("\r\n Update contact query: \r\n " + SqlS); }
                                    cmd2 = new SqlCommand(SqlS, con2);
                                    cmd2.CommandType = System.Data.CommandType.Text;
                                    numrecs = cmd2.ExecuteNonQuery();
                                    if (numrecs>0) { results = true; }

                                    // Generate activity 
                                    ACTIVITY_ID = "";
                                    try {
                                        // Get an activity id
                                        ACTIVITY_ID = wsvcs.GenerateRecordId("S_EVT_ACT", "N", Debug);
                                    }
                                    catch (Exception e2)
                                    {
                                        errmsg = errmsg + ", " + e2.ToString();
                                        ACTIVITY_ID = DateTime.Now.Month.ToString() + DateTime.Now.Day.ToString() + DateTime.Now.Hour.ToString() 
                                            + DateTime.Now.Minute.ToString() + DateTime.Now.Second.ToString() + DateTime.Now.Millisecond.ToString(); 
                                    }
                                    if (Debug == "Y") { debuglog.Debug("  >ACTIVITY_ID: " + ACTIVITY_ID); }
                                    if (ACTIVITY_ID != "")
                                    {
                                        string temperror = "";
                                        temperror = "Flagged as do-not-email because the email address " + EmailAddress[i] + " bounced";
                                        if (EmailError[i] != "Unknown") { temperror = temperror + " based on the email activity report: " + EmailError[i]; }
                                        temperror = temperror.Replace("'", "''");
                                        if (temperror.Length > 1498) { temperror = temperror.Substring(0, 1498); }
                                        if (Debug == "Y") { debuglog.Debug("  >temperror: " + temperror); }
                                        if (REG_NUM != "" || TRAINER_NUM != "")
                                        {
                                            SqlS = "INSERT INTO siebeldb.dbo.S_EVT_ACT " +
                                            "(ACTIVITY_UID,ALARM_FLAG,APPT_REPT_FLG,APPT_START_DT,ASGN_MANL_FLG,ASGN_USR_EXCLD_FLG,BEST_ACTION_FLG,BILLABLE_FLG,CAL_DISP_FLG," +
                                            "COMMENTS_LONG,CONFLICT_ID,COST_CURCY_CD,COST_EXCH_DT," +
                                            "CREATED,CREATED_BY,CREATOR_LOGIN,DCKING_NUM,DURATION_HRS,EMAIL_ATT_FLG, " +
                                            "EMAIL_FORWARD_FLG,EMAIL_RECIP_ADDR,EVT_PRIORITY_CD,EVT_STAT_CD,LAST_UPD,LAST_UPD_BY,MODIFICATION_NUM," +
                                            "NAME,OWNER_LOGIN,OWNER_PER_ID,PCT_COMPLETE,PRIV_FLG,ROW_ID,ROW_STATUS,TARGET_OU_ID," +
                                            "TARGET_PER_ID,TEMPLATE_FLG,TMSHT_RLTD_FLG,TODO_CD,TODO_ACTL_START_DT,TODO_ACTL_END_DT) " +
                                            "VALUES ('" + ACTIVITY_ID + "','N','N',GETDATE(),'Y','Y','N','N','N'," +
                                            "'" + temperror + "',0,'USD',GETDATE()," +
                                            "GETDATE(),'1-3HIZ7','WEBUSER',0,0.00,'N'," +
                                            "'N','" + EmailAddress[i] + "','2-High','Done', GETDATE(),'1-3HIZ7',0," +
                                            "'Flagged bad email address', 'WEBUSER', '" + EmpId + "',100,'N','" + ACTIVITY_ID + "','Y','" + OU_ID + "'," +
                                            "'" + CON_ID + "','N','N', 'Data Maintenance', '" + EmailTime[i] + "', GETDATE())";
                                        }
                                        else
                                        {
                                            SqlS = "INSERT INTO siebeldb.dbo.S_EVT_ACT " +
                                            "(ACTIVITY_UID,ALARM_FLAG,APPT_REPT_FLG,APPT_START_DT,ASGN_MANL_FLG,ASGN_USR_EXCLD_FLG,BEST_ACTION_FLG,BILLABLE_FLG,CAL_DISP_FLG," +
                                            "COMMENTS_LONG,CONFLICT_ID,COST_CURCY_CD,COST_EXCH_DT," +
                                            "CREATED,CREATED_BY,CREATOR_LOGIN,DCKING_NUM,DURATION_HRS,EMAIL_ATT_FLG, " +
                                            "EMAIL_FORWARD_FLG,EMAIL_RECIP_ADDR,EVT_PRIORITY_CD,EVT_STAT_CD,LAST_UPD,LAST_UPD_BY,MODIFICATION_NUM," +
                                            "NAME,OWNER_LOGIN,OWNER_PER_ID,PCT_COMPLETE,PRIV_FLG,ROW_ID,ROW_STATUS,TARGET_OU_ID," +
                                            "TARGET_PER_ID,TEMPLATE_FLG,TMSHT_RLTD_FLG,TODO_CD,TODO_ACTL_START_DT,TODO_ACTL_END_DT) " +
                                            "VALUES ('" + ACTIVITY_ID + "','N','N',GETDATE(),'Y','Y','N','N','N'," +
                                            "'" + temperror + "',0,'USD',GETDATE()," +
                                            "GETDATE(),'1-3HIZ7','WEBUSER',0,0.00,'N'," +
                                            "'N','" + EmailAddress[i] + "','2-High','Done', GETDATE(),'1-3HIZ7',0," +
                                            "'Flagged bad email address', 'WEBUSER', '" + EmpId + "',100,'N','" + ACTIVITY_ID + "','Y','" + OU_ID + "'," +
                                            "'" + CON_ID + "','N','N', 'Data Maintenance', '" + EmailTime[i] + "', GETDATE())";
                                        }
                                        if (Debug == "Y") { debuglog.Debug("\r\n Insert Activity query: \r\n " + SqlS); }
                                        cmd2 = new SqlCommand(SqlS, con2);
                                        cmd2.CommandType = System.Data.CommandType.Text;
                                        cmd2.ExecuteNonQuery();

                                        // Remove CX_CON_DEST records
                                        if (REG_NUM == "" && TRAINER_NUM == "")
                                        {
                                            SqlS = "DELETE FROM siebeldb.dbo.CX_CON_DEST WHERE [TYPE]='EMAIL' AND EMAIL_ADDR IS NOT NULL AND LOWER(EMAIL_ADDR)='" + EmailAddress[i].ToLower() + "'";
                                            if (Debug == "Y") { debuglog.Debug("\r\n Delete CX_CON_DEST records: \r\n " + SqlS); }
                                            cmd2 = new SqlCommand(SqlS, con2);
                                            cmd2.CommandType = System.Data.CommandType.Text;
                                            cmd2.ExecuteNonQuery();
                                        }
                                    }

                                    // Flag message
                                    TempError = EmailError[i].Replace("'", "''");
                                    if (TempError.Length > 200)
                                    {
                                        TempError2 = TempError.Substring(0, 195);
                                        if (Debug == "Y") { debuglog.Debug("\r\n MESSAGES Truncated error message: " + TempError2); }
                                    }
                                    else { TempError2 = TempError; }

                                    SqlS = "UPDATE scanner.dbo.MESSAGES " +
                                    "SET ERROR_DATE=GETDATE(), ERROR_MSG='" + TempError2 + "' " +
                                    "WHERE ERROR_MSG IS NULL AND MS_IDENT IN ( " +
                                    "SELECT MS_IDENT " +
                                    "FROM scanner.dbo.MESSAGES " +
                                    "WHERE SEND_TO='" + EmailAddress[i] + "' AND SUBJECT='" + EmailSubject[i].Replace("'", "''") + "' AND ERROR_MSG IS NULL)";
                                    if (Debug == "Y") { debuglog.Debug("\r\n Update message: \r\n " + SqlS); }
                                    cmd2 = new SqlCommand(SqlS, con2);
                                    cmd2.CommandType = System.Data.CommandType.Text;
                                    numrecs = cmd2.ExecuteNonQuery();
                                    if (numrecs > 0) { results = true; }
                                }

                                // Log
                                if (results) { ltemp = ltemp + String.Format("{0:d/M/yyyy HH:mm:ss}", dt) + ": Mail id '" + EmailId[i] + "', for contact id '" + CON_ID + "' with address '" + EmailAddress[i] + "' at " + EmailTime[i] + " stored to activity id " + ACTIVITY_ID; }
                            }
                        }
                        else
                        {
                            errmsg = "No contact with this email address found: " + EmailAddress[i];
                        }
                        dr.Close();
                    }
                    catch (Exception e)
                    {
                        if (Debug == "Y") { debuglog.Debug("Email address query Error: " + e.ToString()); }
                        errmsg = errmsg + "\r\n Email address query: " + e.ToString();
                        goto CloseDB;
                    }
                }
            }
 
        CloseDB:
            // ============================================
            // Close database connections and objects
            try
            {
                CloseDBConnection(ref con, ref cmd, ref dr);
            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
            }
            try
            {
                CloseDBConnection(ref con2, ref cmd2, ref dr);
            }
            catch (Exception e)
            {
                errmsg = errmsg + ", " + e.ToString();
            }

        CloseLog:
            // ============================================
            // Log Performance Data
            try
            {
                String MyMachine = System.Environment.MachineName.ToString();
                if (Debug == "Y") { debuglog.Debug("\r\n LogPerformanceDataAsync: " + MyMachine + " : " + LogStartTime + "\r\n"); }
                wsvcs.LogPerformanceData2Async(MyMachine, "GETMAIL", LogStartTime, VersionNum, Debug);
            }
            catch
            {
            }

            // ============================================
            // Close the log file, if any            
            if (ltemp != "") { eventlog.Info("GetMail : " + ltemp); }
            if (errmsg != "" && errmsg != "No error") { eventlog.Error("GetMail : Error" + errmsg); }
            try
            {
                if ((Logging == "Y" & Debug != "T") | Debug == "Y")
                {
                    DateTime et = DateTime.Now;
                    if (errmsg != "") { debuglog.Debug("Error: " + errmsg); }
                    if (Logging == "Y")
                    {
                        debuglog.Debug(ltemp);
                    }
                    if (Debug == "Y")
                    {
                        debuglog.Debug("Trace Log Ended " + et.ToString());
                        debuglog.Debug("----------------------------------");
                    }
                }
            }
            catch { }

            // ============================================
            // Release Objects
            try
            {
                fs.Flush();
                fs.Close();
                fs.Dispose();
                fs = null;
                myMessages = null;
                jsonSerializer = null;
                GC.Collect();
            }
            catch { }

        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }

       // ============================================
        // DATABASE FUNCTIONS
        // Open Database Connection
        private string OpenDBConnection(ref string ConnS, ref SqlConnection con, ref SqlCommand cmd)
        {
            string SqlS = "";
            string result = "";
            try
            {
                con = new SqlConnection(ConnS);
                con.Open();
                if (con != null)
                {
                    try
                    {
                        cmd = new SqlCommand(SqlS, con);
                        cmd.CommandTimeout = 300;
                    }
                    catch (Exception ex2) { result = "Open error: " + ex2.ToString(); }
                }
            }
            catch
            {
                if (con.State != System.Data.ConnectionState.Closed) { con.Dispose(); }
                ConnS = ConnS + ";Pooling=false";
                try
                {
                    con = new SqlConnection(ConnS);
                    con.Open();
                    if (con != null)
                    {
                        try
                        {
                            cmd = new SqlCommand(SqlS, con);
                            cmd.CommandTimeout = 300;
                        }
                        catch (Exception ex2)
                        {
                            result = "Open error: " + ex2.ToString();
                        }
                    }
                }
                catch (Exception ex2)
                {
                    result = "Open error: " + ex2.ToString();
                }
            }
            return result;
        }

        // Close Database Connection
        private void CloseDBConnection(ref SqlConnection con, ref SqlCommand cmd, ref SqlDataReader dr)
        {
            // This function closes a database connection safely

            // Handle datareader
            try
            {
                dr.Close();
            }
            catch { }

            try
            {
                dr = null;
            }
            catch { }


            // Handle command
            try
            {
                cmd.Dispose();
            }
            catch { }

            try
            {
                cmd = null;
            }
            catch { }


            // Handle connection
            try
            {
                con.Close();
            }
            catch { }

            try
            {
                SqlConnection.ClearPool(con);
            }
            catch { }

            try
            {
                con.Dispose();
            }
            catch { }

            try
            {
                con = null;
            }
            catch { }
        }

        // ============================================
        // OTHER FUNCTIONS
        public static DateTime ConvertFromUnixTimestamp(double timestamp)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            timestamp = timestamp - 18000;
            return origin.AddSeconds(timestamp); 
        }

       // ============================================
        // DEBUG FUNCTIONS
        private bool writeoutputfs(ref FileStream fs, String instring)
        {
            // This function writes a line to a previously opened filestream, and then flushes it
            // promptly.  This assists in debugging services
            Boolean result;
            try
            {
                instring = instring + "\r\n";
                //byte[] bytesStream = new byte[instring.Length];
                Byte[] bytes = encoding.GetBytes(instring);
                fs.Write(bytes, 0, bytes.Length);
                result = true;
            }
            catch
            {
                result = false;
            }
            fs.Flush();
            return result;
        }
    }
}

