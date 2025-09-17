# GetMail Webhook Service

## Overview

The GetMail webhook service is a C# ASP.NET web service that processes email bounce and rejection notifications from MailChimp/Mandrill. When an email fails to deliver (bounces, is rejected, or encounters other delivery issues), MailChimp sends a webhook notification to this service, which then updates the corresponding database records to flag problematic email addresses and create activity records for tracking purposes.

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
    <add key="GetMail_debug" value="N" />
    
    <!-- Employee ID for activity records -->
    <add key="GetChat_EmpId" value="1-EMN4X" />
    
    <!-- Employee login for activity records -->
    <add key="GetChat_EmpLogin" value="TECHNICAL SUPPORT" />
</appSettings>

<connectionStrings>
    <add name="hcidb" 
         connectionString="server=HCIDBSQL\HCIDB;uid=SIEBEL;pwd=SIEBEL;database=siebeldb" 
         providerName="System.Data.SqlClient" />
</connectionStrings>
```

### log4net Configuration

The service uses log4net for logging. Configure in web.config:

```xml
<log4net>
    <appender name="EventLogAppender" type="log4net.Appender.EventLogAppender">
        <applicationName value="GetMail" />
        <layout type="log4net.Layout.PatternLayout">
            <conversionPattern value="%date [%thread] %-5level %logger - %message%newline" />
        </layout>
    </appender>
    
    <appender name="DebugLogAppender" type="log4net.Appender.RollingFileAppender">
        <file value="C:\Logs\GetMail.log" />
        <appendToFile value="true" />
        <rollingStyle value="Size" />
        <maxSizeRollBackups value="10" />
        <maximumFileSize value="10MB" />
        <staticLogFileName value="false" />
        <layout type="log4net.Layout.PatternLayout">
            <conversionPattern value="%date [%thread] %-5level %logger - %message%newline" />
        </layout>
    </appender>
    
    <logger name="EventLog">
        <level value="INFO" />
        <appender-ref ref="EventLogAppender" />
    </logger>
    
    <logger name="DebugLog">
        <level value="DEBUG" />
        <appender-ref ref="DebugLogAppender" />
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
            "subject": "Host a TIPS Workshop on Your Campus",
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
- `root@bm2.gettips.com`
- `root@bm1.gettips.com`

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

## Version History

- **v1.0.0** - Initial implementation with basic bounce processing
- **v1.0.1** - Added comprehensive logging and error handling
- **v1.0.2** - Enhanced database schema with indexes and views

## Support

For technical support or questions about this service, please refer to the application logs and database records. The service includes comprehensive logging to assist with troubleshooting.

## License

This software is proprietary and confidential. Unauthorized copying, distribution, or modification is prohibited.
