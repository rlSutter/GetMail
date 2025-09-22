# GetMail Webhook Service

## Table of Contents

1. [Overview](#overview)
2. [Design](#design)
3. [Architecture](#architecture)
4. [Data Model](#data-model)
5. [Database Schema](#database-schema)
6. [Data Structures](#data-structures)
7. [Webhook Payload Format](#webhook-payload-format)
8. [Business Logic](#business-logic)
9. [Assumptions](#assumptions)
10. [Error Handling](#error-handling)
11. [Security Considerations](#security-considerations)
12. [Configuration](#configuration)
13. [Deployment](#deployment)
14. [Monitoring and Maintenance](#monitoring-and-maintenance)
15. [API Reference](#api-reference)
16. [References](#references)
17. [Development](#development)
18. [Update History](#update-history)
19. [Notifications](#notifications)
20. [Related Web Services](#related-web-services)
21. [Executing](#executing)
22. [Testing](#testing)
23. [Scheduling](#scheduling)
24. [Monitoring](#monitoring)
25. [Logging](#logging)
26. [Results](#results)
27. [Programming](#programming)
28. [Operational](#operational)
29. [Support](#support)
30. [License](#license)

## Overview

This service implements an integration point for the Mandrill mail processing service. When an email bounces or is rejected, a defined "web hook" on that service forwards it to this service for processing. The email address is either flagged do-not-email or removed entirely, and an activity is created, for everyone processed. This service is complemented by the CMProcessEmailImport agent which processes returned email reports from Mandrill in batch.

The GetMail webhook service is a C# ASP.NET web service that processes email bounce and rejection notifications from Mandrill. When an email fails to deliver (bounces, is rejected, or encounters other delivery issues), Mandrill sends a webhook notification to this service, which then updates the corresponding database records to flag problematic email addresses and create activity records for tracking purposes.

### Purpose
This service serves as a bridge between the Mandrill email platform and the internal customer relationship management system, ensuring that all email delivery issues are properly recorded and tracked for customer service follow-up and email list maintenance.

### Scope
The service handles:
- Real-time email bounce processing
- Email address validation and suppression
- Contact record updates
- Activity record creation in the CRM system
- Message tracking updates
- Destination cleanup for invalid contacts

## Design

### System Architecture

The GetMail service follows a layered architecture pattern with clear separation of concerns:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Mandrill      │──▶│  GetMail.ashx   │───▶│   SQL Server    │
│   Email Service │    │   Web Service   │    │   Database      │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                              │
                              ▼
                       ┌─────────────────┐
                       │   Log4net       │
                       │   Logging       │
                       └─────────────────┘
```

### Design Principles

1. **Single Responsibility**: Each component has a specific, well-defined purpose
2. **Fail-Safe**: Comprehensive error handling and logging
3. **Performance**: Efficient database operations with connection pooling
4. **Maintainability**: Clear code structure and comprehensive documentation
5. **Security**: Input validation and SQL injection prevention

### Integration Overview

See BulkMailServer#Bounce_Mail_Administration for information regarding the integration, and an overview of this system.

## Architecture

### Components

- **GetMail.ashx** - ASP.NET HTTP handler declaration
- **GetMail.ashx.cs** - Main webhook processing logic
- **Database Schema** - SQL Server tables for contact management and activity tracking
- **Logging** - log4net integration for debugging and monitoring

### Data Flow

1. MailChimp/Mandrill sends JSON webhook notification to the service
2. Service deserializes the JSON payload
3. Extracts email address, bounce reason, and other metadata
4. Queries the database to find the contact record
5. Updates contact record (suppresses email or removes email address)
6. Creates activity record for audit trail
7. Updates message tracking table with error information

## Data Model

In order to support the information generated from this service, the following fields have special meaning in an activity created from this service:

| FIELD | DESCRIPTION |
|-------|-------------|
| EMAIL_RECIP_ADDR | Contains the email address operated on |
| SRA_TYPE_CD | Mapped to code "Data Maintenance" |
| COMMENTS_LONG | Contains the notice and the SMTP error message |

The Mandrill service provides data in JSON object format. The JSON.Net library is used to convert this into C# classes.

### Data Structures

The class defined to receive data from JSON is as follows:

```csharp
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
```

## Database Schema

### Primary Tables

#### S_CONTACT (siebeldb.dbo.S_CONTACT)
Stores contact information including email addresses and suppression flags.

**Key Fields:**
- `ROW_ID` - Primary key
- `EMAIL_ADDR` - Contact's email address
- `SUPPRESS_EMAIL_FLG` - Flag to suppress future emails
- `PR_DEPT_OU_ID` - Organizational unit ID
- `X_REGISTRATION_NUM` - Registration number
- `X_TRAINER_NUM` - Trainer number

#### S_EVT_ACT (siebeldb.dbo.S_EVT_ACT)
Tracks activities and events, including email bounce events.

**Key Fields:**
- `ROW_ID` - Primary key
- `ACTIVITY_UID` - Unique activity identifier
- `COMMENTS_LONG` - Detailed description of the bounce
- `EMAIL_RECIP_ADDR` - Email address that bounced
- `TARGET_PER_ID` - Contact ID
- `TODO_CD` - Activity type (set to "Data Maintenance" for bounces)

#### MESSAGES (scanner.dbo.MESSAGES)
Tracks email messages and their delivery status.

**Key Fields:**
- `MS_IDENT` - Message identifier
- `SEND_TO` - Recipient email address
- `SUBJECT` - Email subject
- `ERROR_DATE` - When the error occurred
- `ERROR_MSG` - Error description

#### CX_CON_DEST (siebeldb.dbo.CX_CON_DEST)
Stores contact communication preferences and destinations.

**Key Fields:**
- `ROW_ID` - Primary key
- `TYPE` - Communication type (e.g., "EMAIL")
- `EMAIL_ADDR` - Email address
- `CONTACT_ID` - Reference to contact

## Configuration

### Web.config Settings

```xml
<appSettings>
    <!-- Debug mode: Y=Yes, N=No, T=Trace -->
    <add key="GetMail_debug" value="Y" />
    
    <!-- Employee ID for activity records -->
    <add key="GetMail_EmpId" value="1-XXXXX" />
    
    <!-- Employee login for activity records -->
    <add key="GetMail_EmpLogin" value="TECHNICAL SUPPORT" />
</appSettings>

<connectionStrings>
    <add name="ApplicationServices"
         connectionString="data source=.\SQLEXPRESS;Integrated Security=SSPI;AttachDBFilename=|DataDirectory|\aspnetdb.mdf;User Instance=true"
         providerName="System.Data.SqlClient" />
    <add name="hcidb" 
         connectionString="server=YOUR_SERVER\YOUR_INSTANCE;uid=YOUR_USER;pwd=YOUR_PASSWORD;Min Pool Size=3;Max Pool Size=5;Connect Timeout=10;database=" 
         providerName="System.Data.SqlClient" />
</connectionStrings>
```

### Configuration Item Descriptions

The following is a description of the configuration items:

- **GetMail_debug**: The only way to enable debug mode. The value stored here turns that mode on or off.
- **GetMail_EmpId**: Used to specify the employee id for activities generated.
- **GetMail_EmpLogin**: Used to specify the employee login for activities generated.
- **hcidb connectionString**: Used to specify the database connection string.

This is extracted using the following code in the service:

```csharp
// ============================================
// Debug Setup
mypath = HttpRuntime.AppDomainAppPath;
Logging = "Y";
try
{
    temp = WebConfigurationManager.AppSettings["GetMail_debug"];
    Debug = temp;
    EmpId = WebConfigurationManager.AppSettings["GetMail_EmpId"];
    if (EmpId == "") { EmpId = "1-XXXXX"; }
    EmpLogin = WebConfigurationManager.AppSettings["GetMail_EmpLogin"];
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
    ConnS = "server=YOUR_SERVER\\YOUR_INSTANCE;uid=YOUR_USER;pwd=YOUR_PASSWORD;database=yourdb";
}
```

### log4net Configuration

The web.config file also contains SysLog configuration information:

```xml
<log4net>
    <appender name="RemoteSyslogAppender" type="log4net.Appender.RemoteSyslogAppender">
        <identity value="" />
        <layout type="log4net.Layout.PatternLayout" value="%message"/>
        <remoteAddress value="YOUR_SYSLOG_SERVER_IP" />
        <filter type="log4net.Filter.LevelRangeFilter">
            <levelMin value="DEBUG" />
        </filter>
    </appender>
    
    <appender name="LogFileAppender" type="log4net.Appender.RollingFileAppender">
        <file type="log4net.Util.PatternString" value="%property{LogFileName}"/>
        <appendToFile value="true"/>
        <rollingStyle value="Size"/>
        <maxSizeRollBackups value="3"/>
        <maximumFileSize value="10000KB"/>
        <staticLogFileName value="true"/>
        <layout type="log4net.Layout.PatternLayout">
            <conversionPattern value="%message%newline"/>
        </layout>
    </appender>
    
    <logger name="EventLog">
        <level value="ALL"/>
        <appender-ref ref="RemoteSyslogAppender"/>
    </logger>
    
    <logger name="DebugLog">
        <level value="ALL"/>
        <appender-ref ref="LogFileAppender"/>
    </logger>
</log4net>
```

## Webhook Payload Format

The service expects JSON payloads in the following format (based on Mandrill webhook structure):

```json
[
    {
        "event": "hard_bounce",
        "_id": "795432eb5481498bb99cec7a1f267310",
        "msg": {
            "ts": 1445956507,
            "_id": "795432eb5481498bb99cec7a1f267310",
            "state": "bounced",
            "subject": "Host a Workshop on Your Campus",
            "email": "liz1612@hotmail.com",
            "tags": [],
            "smtp_events": [],
            "resends": [],
            "_version": "CwFbf0_VJIdKx15h6_2sMg",
            "diag": "smtp;550 Requested action not taken: mailbox unavailable",
            "bgtools_code": 10,
            "sender": "estellet@gettips.com",
            "template": null,
            "bounce_description": "bad_mailbox"
        },
        "ts": 1445962746
    }
]
```

## Business Logic

### Email Processing Rules

1. **Registration/Trainer Numbers Present**: If the contact has registration or trainer numbers, the email address is suppressed (`SUPPRESS_EMAIL_FLG = 'Y'`) but not removed.

2. **No Registration/Trainer Numbers**: If the contact has no registration or trainer numbers, the email address is completely removed from the contact record.

3. **Activity Creation**: For all bounces, an activity record is created with:
   - Type: "Data Maintenance"
   - Priority: "2-High"
   - Status: "Done"
   - Detailed description of the bounce reason

4. **Message Tracking**: The corresponding message record is updated with error information.

5. **Destination Cleanup**: For contacts without registration/trainer numbers, related CX_CON_DEST records are deleted.

### Excluded Email Addresses

The service ignores the following email addresses:
- `root@bm2.example.com`
- `root@bm1.example.com`

## Assumptions

The following assumptions are made about the system:

1. **JSON Format**: The supplied JSON file is properly formed and follows the Mandrill webhook format.
2. **Contact Records**: One or more email addresses exist in Contact records in the database.
3. **Database Connectivity**: The database is accessible and the connection string is valid.
4. **Webhook Reliability**: Mandrill will deliver webhook notifications reliably.
5. **Data Integrity**: Contact records maintain referential integrity with related tables.

## Error Handling

### Logging Levels

- **Event Log**: Records successful processing and errors
- **Debug Log**: Detailed trace information (when debug mode is enabled)
- **JSON Log**: Raw webhook payloads stored in `C:\Logs\GetMail-JSON.log`

### Error Scenarios

1. **Database Connection Issues**: Automatic retry with connection pooling disabled
2. **JSON Parsing Errors**: Detailed error logging with original payload
3. **Missing Contact Records**: Logged as informational messages
4. **SQL Execution Errors**: Comprehensive error logging with context

## Security Considerations

### Input Validation

- Email addresses are validated and sanitized
- SQL injection prevention through parameterized queries (where applicable)
- JSON payload size limits
- Error message truncation to prevent buffer overflows

### Access Control

- Database user should have minimal required permissions
- Log files should be secured with appropriate file system permissions
- Webhook endpoint should be protected with authentication if possible

## Deployment

### Prerequisites

1. SQL Server with siebeldb and scanner databases
2. .NET Framework 4.0 or higher
3. log4net library
4. Newtonsoft.Json library
5. Appropriate database permissions

### Installation Steps

1. Deploy the web service files to IIS
2. Run the database schema script (`GetMail_Database_Schema.sql`)
3. Configure web.config with appropriate connection strings and settings
4. Set up log4net configuration
5. Create application user with necessary database permissions
6. Configure MailChimp webhook URL to point to the service endpoint

### Testing

1. Enable debug mode in web.config
2. Send test webhook payload to the service
3. Verify database updates and log entries
4. Test error scenarios (invalid JSON, database unavailable, etc.)

## Monitoring and Maintenance

### Performance Monitoring

- Monitor log file sizes and rotation
- Track database performance for contact lookups
- Monitor webhook response times

### Maintenance Tasks

- Regular cleanup of old bounce records using `SP_CLEANUP_OLD_BOUNCES`
- Log file rotation and archival
- Database index maintenance
- Review and update suppressed email addresses periodically

### Troubleshooting

#### Common Issues

1. **Webhook Not Processing**: Check IIS logs, verify endpoint URL
2. **Database Connection Errors**: Verify connection string and permissions
3. **JSON Parsing Errors**: Check webhook payload format
4. **Missing Activity Records**: Verify web service configuration and permissions

#### Debug Mode

Enable debug mode by setting `GetMail_debug` to "Y" in web.config. This will:
- Log detailed trace information
- Record all SQL queries
- Show step-by-step processing information

## API Reference

### Endpoint

```
POST /GetMail.ashx
Content-Type: application/json
```

### Request

Raw JSON payload from MailChimp/Mandrill webhook.

### Response

- **Success**: HTTP 200 with no content
- **Error**: HTTP 500 with error details in logs

### Headers

The service accepts standard HTTP headers. No special authentication headers are required (consider adding authentication for production use).

## References

The following was referenced during implementation:

- **Mandrill Webhooks Documentation**: https://mandrill.zendesk.com/hc/en-us/articles/205583217-Introduction-to-Webhooks - Description of the "web hook"
- **Stack Overflow - Mandrill Webhooks in .NET**: http://stackoverflow.com/questions/24695846/how-to-handle-mandrill-webhooks-in-net - Useful information on a web-hook project

## Development

To facilitate the development of this service the RunScope service was engaged (https://www.runscope.com/start#basics). The service credentials are:
- Username: developer@example.com
- Password: [REDACTED]

Using this service a http://requestb.in/ was created, and then a web hook was generated to output data to that bin provided. A sample message generated:

```json
[{"event":"reject","_id":"bd7f3cd8255f4cdd89c4c259de5dd595","msg":{"ts":1445962653,"subject":"Host a Workshop on Your Campus","email":"user@example.edu","tags":[],"opens":[],"clicks":[],"state":"rejected","smtp_events":[],"subaccount":null,"resends":[],"reject":null,"_id":"bd7f3cd8255f4cdd89c4c259de5dd595","sender":"sender@example.com","template":null},"ts":1445962653},{"event":"hard_bounce","_id":"f882e997ed9546fdba6d4b78658ca13e","msg":{"ts":1445955776,"_id":"f882e997ed9546fdba6d4b78658ca13e","state":"bounced","subject":"Host a Workshop on Your Campus","email":"user2@example.edu","tags":[],"smtp_events":[],"resends":[],"_version":"13CTOSezVKWJzQUU_bgQ1g","diag":"smtp;550 5.1.1 RESOLVER.ADR.RecipNotFound; not found","bgtools_code":10,"sender":"sender@example.com","template":null,"bounce_description":"bad_mailbox"},"ts":1445962662}]
```

## Update History

### 10/28/15
Modified conditional logic on removing CX_CON_DEST records as well as fixed debug output.

### 1/13/16
Updated to improve logging

### 1/14/16
Improved logging again. Set to update the message record if undeliverable as per the Operational Notes

### 12/30/16
Located and corrected an issue with logic related to handling the errors generated from returned mail that was causing activities to not be generated. Also added "try" logic to generating activity Ids using GenerateRecordId so that it prepares one using randomized strings if it can't do so using the service.

### 5/17/19
Updated to fix logging to LogPerformanceData - the method name was being cast as "PROCESSREQUEST"

### 10/23/19
Updated to resolve an issue with the value of COMMENTS_LONG causing a SQL query error. Fixed issue with scanner.MESSAGE update query. Fixed the comments to make more sense if there is no error message information.

### 2/17/20
Updated to support WebServicesSecurity#Version_Management

## Notifications

None at this time.

## Related Web Services

This service was based on the GetChat service

- **GenerateRecordId**: Used to generate a new S_EVT_ACT.ROW_ID
- **LogPerformanceData**: Logs performance statistics on this service

## Executing

This service is executed by Mandrill using the following URL:
```
http://your-domain.com/GetMail.ashx
```

The service is provided a JSON object similar to the following (and formatted using http://jsonformat.com/):

```json
[  
   {  
      "event":"hard_bounce",
      "_id":"14cbe1655b834ad98b2c2c96a84e3506",
      "msg":{  
         "ts":1445956502,
         "_id":"14cbe1655b834ad98b2c2c96a84e3506",
         "state":"bounced",
         "subject":"Host a Workshop on Your Campus",
         "email":"user@example.edu",
         "tags":[  

         ],
         "smtp_events":[  

         ],
         "resends":[  

         ],
         "_version":"cyyzpOzJVPvaj5hUU59k7A",
         "diag":"smtp;550 5.1.1 RESOLVER.ADR.RecipNotFound; not found",
         "bgtools_code":10,
         "sender":"sender@example.com",
         "template":null,
         "bounce_description":"bad_mailbox"
      },
      "ts":1445962771
   }
]
```

This service attempts to parse this information to determine whom to remove/flag their email address and create an activity.

## Testing

This web service can be tested using Fiddler (available at your-tools-directory\FiddlerSetup.exe) by doing the following using the Composer tab:

1. Create a POST transaction to `http://your-production-server/GetMail.ashx` if using a production server, or `http://localhost:8080/GetMail.ashx` if executing on the development machine.
2. In the Request Headers box add `Content-type: application/json; charset=utf-8`
3. In the Request Body of the transaction, enter a test JSON object (formatted or non-formatted).
4. Click the "Execute" button to send the transaction
5. Check the transaction in the database in the Activities > All Activities view.

The results will be reported in the log file `C:\Logs\GetMail.log` on the server tested or the local development workstation.

## Scheduling

This service is not scheduled. It is invoked ad-hoc by other applications and web services.

## Monitoring

This service may not be monitored at this time.

## Logging

This service provides a "Debug" log, `GetMail.log` which is produced in the log folder (`C:\Logs` on the application servers), which is initiated when the Debug parameter is set to "Y".

### Debug Log Example

```
----------------------------------
Trace Log Started 10/25/2019 5:41:09 AM
Parameters-
jsonString: [{"event":"reject","_id":"4cb0bdb4f7af4457ba00d985c2a3b430","msg":{"ts":1571996464,"subject":"Results of your course","email":"user@example.com","tags":[],"opens":[],"clicks":[],"state":"rejected","smtp_events":[],"subaccount":null,"resends":[],"reject":null,"_id":"4cb0bdb4f7af4457ba00d985c2a3b430","sender":"sender@example.com","template":null},"ts":1571996464}]

MESSAGES: 

0
>id: 4cb0bdb4f7af4457ba00d985c2a3b430
>emailAddress: user@example.com
>EmailTime: 10/25/2019 4:41:04 AM
>subject: Results of your course
>error: Unknown

....
Processing: 0 - user@example.com

Email address query: 
SELECT ROW_ID, PR_DEPT_OU_ID, X_REGISTRATION_NUM, X_TRAINER_NUM FROM yourdb.dbo.S_CONTACT WHERE LOWER(EMAIL_ADDR)='user@example.com'
>CON_ID: 764X75LZ00H2 >OU_ID: 1-DU5YJ >REG_NUM: 764X75LZ00H2    >TRAINER_NUM: 

Update contact query: 
UPDATE yourdb.dbo.S_CONTACT SET SUPPRESS_EMAIL_FLG='Y' WHERE ROW_ID='764X75LZ00H2'
>ACTIVITY_ID: A6M17IAVS30
>temperror: Flagged as do-not-email because the email address user@example.com bounced

Insert Activity query: 
INSERT INTO yourdb.dbo.S_EVT_ACT (ACTIVITY_UID,ALARM_FLAG,APPT_REPT_FLG,APPT_START_DT,ASGN_MANL_FLG,ASGN_USR_EXCLD_FLG,BEST_ACTION_FLG,BILLABLE_FLG,CAL_DISP_FLG,COMMENTS_LONG,CONFLICT_ID,COST_CURCY_CD,COST_EXCH_DT,CREATED,CREATED_BY,CREATOR_LOGIN,DCKING_NUM,DURATION_HRS,EMAIL_ATT_FLG, EMAIL_FORWARD_FLG,EMAIL_RECIP_ADDR,EVT_PRIORITY_CD,EVT_STAT_CD,LAST_UPD,LAST_UPD_BY,MODIFICATION_NUM,NAME,OWNER_LOGIN,OWNER_PER_ID,PCT_COMPLETE,PRIV_FLG,ROW_ID,ROW_STATUS,TARGET_OU_ID,TARGET_PER_ID,TEMPLATE_FLG,TMSHT_RLTD_FLG,TODO_CD,TODO_ACTL_START_DT,TODO_ACTL_END_DT) VALUES ('A6M17IAVS30','N','N',GETDATE(),'Y','Y','N','N','N','Flagged as do-not-email because the email address user@example.com bounced',0,'USD',GETDATE(),GETDATE(),'1-3HIZ7','WEBUSER',0,0.00,'N','N','user@example.com','2-High','Done', GETDATE(),'1-3HIZ7',0,'Flagged bad email address', 'WEBUSER', '',100,'N','A6M17IAVS30','Y','1-DU5YJ','764X75LZ00H2','N','N', 'Data Maintenance', '10/25/2019 4:41:04 AM', GETDATE())

Update message: 
UPDATE scanner.dbo.MESSAGES SET ERROR_DATE=GETDATE(), ERROR_MSG='Unknown' WHERE ERROR_MSG IS NULL AND MS_IDENT IN ( SELECT MS_IDENT FROM scanner.dbo.MESSAGES WHERE SEND_TO='user@example.com' AND SUBJECT='Results of your course' AND ERROR_MSG IS NULL)

LogPerformanceDataAsync: CLOUDSVC2 : 10/25/2019 5:41:09 AM

25/10/2019 05:41:09: Mail id '4cb0bdb4f7af4457ba00d985c2a3b430', for contact id '764X75LZ00H2' with address 'user@example.com' at 10/25/2019 4:41:04 AM stored to activity id A6M17IAVS30
Trace Log Ended 10/25/2019 5:41:09 AM
----------------------------------
```

If debug logging is disabled, transactions are logged to the SysLog server as in the following:

```
Mail id 'fe9819c0c7904175b23f7d0d16be735a', with address 'user@example.edu' at 10/27/2015 2:35:23 PM stored to activity id U3K4X853999Z
```

Finally, the JSON string provided to this service itself is logged to the file `GetMail-JSON.log` in the same directory.

## Results

When this service is executed, it creates activity records in the database. There is no other results provided other than the log file.


