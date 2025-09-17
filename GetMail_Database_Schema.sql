-- ============================================
-- GetMail Webhook Database Schema
-- SQL Server DDL Script
-- ============================================
-- This script creates the database schema required for the GetMail webhook service
-- which processes email bounce/rejection notifications from MailChimp/Mandrill

-- ============================================
-- Database Creation (if needed)
-- ============================================
-- Uncomment the following lines if you need to create the databases
-- CREATE DATABASE [siebeldb];
-- CREATE DATABASE [scanner];

-- ============================================
-- Siebel Database Tables
-- ============================================

-- S_CONTACT table - Main contact information
-- This table stores contact details including email addresses
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='S_CONTACT' AND xtype='U')
BEGIN
    CREATE TABLE [siebeldb].[dbo].[S_CONTACT] (
        [ROW_ID] NVARCHAR(15) NOT NULL PRIMARY KEY,
        [EMAIL_ADDR] NVARCHAR(100) NULL,
        [SUPPRESS_EMAIL_FLG] NVARCHAR(1) NULL DEFAULT 'N',
        [PR_DEPT_OU_ID] NVARCHAR(15) NULL,
        [X_REGISTRATION_NUM] NVARCHAR(50) NULL,
        [X_TRAINER_NUM] NVARCHAR(50) NULL,
        [CREATED] DATETIME NULL,
        [CREATED_BY] NVARCHAR(15) NULL,
        [LAST_UPD] DATETIME NULL,
        [LAST_UPD_BY] NVARCHAR(15) NULL,
        [ROW_STATUS] NVARCHAR(1) NULL DEFAULT 'Y'
    );
    
    -- Create indexes for performance
    CREATE INDEX [IX_S_CONTACT_EMAIL] ON [siebeldb].[dbo].[S_CONTACT] ([EMAIL_ADDR]);
    CREATE INDEX [IX_S_CONTACT_SUPPRESS] ON [siebeldb].[dbo].[S_CONTACT] ([SUPPRESS_EMAIL_FLG]);
END;

-- S_EVT_ACT table - Activity/Event tracking
-- This table stores activity records for email bounce events
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='S_EVT_ACT' AND xtype='U')
BEGIN
    CREATE TABLE [siebeldb].[dbo].[S_EVT_ACT] (
        [ROW_ID] NVARCHAR(15) NOT NULL PRIMARY KEY,
        [ACTIVITY_UID] NVARCHAR(15) NULL,
        [ALARM_FLAG] NVARCHAR(1) NULL DEFAULT 'N',
        [APPT_REPT_FLG] NVARCHAR(1) NULL DEFAULT 'N',
        [APPT_START_DT] DATETIME NULL,
        [ASGN_MANL_FLG] NVARCHAR(1) NULL DEFAULT 'Y',
        [ASGN_USR_EXCLD_FLG] NVARCHAR(1) NULL DEFAULT 'Y',
        [BEST_ACTION_FLG] NVARCHAR(1) NULL DEFAULT 'N',
        [BILLABLE_FLG] NVARCHAR(1) NULL DEFAULT 'N',
        [CAL_DISP_FLG] NVARCHAR(1) NULL DEFAULT 'N',
        [COMMENTS_LONG] NVARCHAR(1500) NULL,
        [CONFLICT_ID] INT NULL DEFAULT 0,
        [COST_CURCY_CD] NVARCHAR(3) NULL DEFAULT 'USD',
        [COST_EXCH_DT] DATETIME NULL,
        [CREATED] DATETIME NULL,
        [CREATED_BY] NVARCHAR(15) NULL,
        [CREATOR_LOGIN] NVARCHAR(50) NULL,
        [DCKING_NUM] INT NULL DEFAULT 0,
        [DURATION_HRS] DECIMAL(5,2) NULL DEFAULT 0.00,
        [EMAIL_ATT_FLG] NVARCHAR(1) NULL DEFAULT 'N',
        [EMAIL_FORWARD_FLG] NVARCHAR(1) NULL DEFAULT 'N',
        [EMAIL_RECIP_ADDR] NVARCHAR(100) NULL,
        [EVT_PRIORITY_CD] NVARCHAR(10) NULL,
        [EVT_STAT_CD] NVARCHAR(10) NULL,
        [LAST_UPD] DATETIME NULL,
        [LAST_UPD_BY] NVARCHAR(15) NULL,
        [MODIFICATION_NUM] INT NULL DEFAULT 0,
        [NAME] NVARCHAR(100) NULL,
        [OWNER_LOGIN] NVARCHAR(50) NULL,
        [OWNER_PER_ID] NVARCHAR(15) NULL,
        [PCT_COMPLETE] INT NULL DEFAULT 100,
        [PRIV_FLG] NVARCHAR(1) NULL DEFAULT 'N',
        [ROW_STATUS] NVARCHAR(1) NULL DEFAULT 'Y',
        [TARGET_OU_ID] NVARCHAR(15) NULL,
        [TARGET_PER_ID] NVARCHAR(15) NULL,
        [TEMPLATE_FLG] NVARCHAR(1) NULL DEFAULT 'N',
        [TMSHT_RLTD_FLG] NVARCHAR(1) NULL DEFAULT 'N',
        [TODO_CD] NVARCHAR(50) NULL,
        [TODO_ACTL_START_DT] DATETIME NULL,
        [TODO_ACTL_END_DT] DATETIME NULL
    );
    
    -- Create indexes for performance
    CREATE INDEX [IX_S_EVT_ACT_EMAIL] ON [siebeldb].[dbo].[S_EVT_ACT] ([EMAIL_RECIP_ADDR]);
    CREATE INDEX [IX_S_EVT_ACT_TARGET] ON [siebeldb].[dbo].[S_EVT_ACT] ([TARGET_PER_ID]);
    CREATE INDEX [IX_S_EVT_ACT_CREATED] ON [siebeldb].[dbo].[S_EVT_ACT] ([CREATED]);
END;

-- ============================================
-- Scanner Database Tables
-- ============================================

-- MESSAGES table - Email message tracking
-- This table tracks email messages and their delivery status
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MESSAGES' AND xtype='U')
BEGIN
    CREATE TABLE [scanner].[dbo].[MESSAGES] (
        [MS_IDENT] NVARCHAR(50) NOT NULL PRIMARY KEY,
        [SEND_TO] NVARCHAR(100) NULL,
        [SUBJECT] NVARCHAR(200) NULL,
        [ERROR_DATE] DATETIME NULL,
        [ERROR_MSG] NVARCHAR(200) NULL,
        [CREATED_DATE] DATETIME NULL DEFAULT GETDATE(),
        [STATUS] NVARCHAR(20) NULL DEFAULT 'PENDING'
    );
    
    -- Create indexes for performance
    CREATE INDEX [IX_MESSAGES_SEND_TO] ON [scanner].[dbo].[MESSAGES] ([SEND_TO]);
    CREATE INDEX [IX_MESSAGES_ERROR_DATE] ON [scanner].[dbo].[MESSAGES] ([ERROR_DATE]);
    CREATE INDEX [IX_MESSAGES_STATUS] ON [scanner].[dbo].[MESSAGES] ([STATUS]);
END;

-- CX_CON_DEST table - Contact destination preferences
-- This table stores contact communication preferences
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='CX_CON_DEST' AND xtype='U')
BEGIN
    CREATE TABLE [siebeldb].[dbo].[CX_CON_DEST] (
        [ROW_ID] NVARCHAR(15) NOT NULL PRIMARY KEY,
        [TYPE] NVARCHAR(20) NULL,
        [EMAIL_ADDR] NVARCHAR(100) NULL,
        [CONTACT_ID] NVARCHAR(15) NULL,
        [ACTIVE_FLG] NVARCHAR(1) NULL DEFAULT 'Y',
        [CREATED] DATETIME NULL DEFAULT GETDATE(),
        [CREATED_BY] NVARCHAR(15) NULL,
        [LAST_UPD] DATETIME NULL DEFAULT GETDATE(),
        [LAST_UPD_BY] NVARCHAR(15) NULL
    );
    
    -- Create indexes for performance
    CREATE INDEX [IX_CX_CON_DEST_TYPE] ON [siebeldb].[dbo].[CX_CON_DEST] ([TYPE]);
    CREATE INDEX [IX_CX_CON_DEST_EMAIL] ON [siebeldb].[dbo].[CX_CON_DEST] ([EMAIL_ADDR]);
    CREATE INDEX [IX_CX_CON_DEST_CONTACT] ON [siebeldb].[dbo].[CX_CON_DEST] ([CONTACT_ID]);
END;

-- ============================================
-- Sample Data (Optional)
-- ============================================
-- Uncomment the following section to insert sample data for testing

/*
-- Sample contact record
INSERT INTO [siebeldb].[dbo].[S_CONTACT] 
([ROW_ID], [EMAIL_ADDR], [SUPPRESS_EMAIL_FLG], [PR_DEPT_OU_ID], [X_REGISTRATION_NUM], [X_TRAINER_NUM], [CREATED], [CREATED_BY], [ROW_STATUS])
VALUES 
('1-SAMPLE', 'test@example.com', 'N', '1-OU001', 'REG123', 'TRAIN456', GETDATE(), 'SYSTEM', 'Y');

-- Sample message record
INSERT INTO [scanner].[dbo].[MESSAGES] 
([MS_IDENT], [SEND_TO], [SUBJECT], [STATUS])
VALUES 
('MSG001', 'test@example.com', 'Test Email Subject', 'PENDING');
*/

-- ============================================
-- Views for Reporting
-- ============================================

-- View to show email bounce statistics
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='V_EMAIL_BOUNCE_STATS' AND xtype='V')
BEGIN
    EXEC('CREATE VIEW [siebeldb].[dbo].[V_EMAIL_BOUNCE_STATS] AS
    SELECT 
        c.EMAIL_ADDR,
        c.SUPPRESS_EMAIL_FLG,
        COUNT(e.ROW_ID) as BounceCount,
        MAX(e.CREATED) as LastBounceDate,
        MAX(e.COMMENTS_LONG) as LastBounceReason
    FROM [siebeldb].[dbo].[S_CONTACT] c
    LEFT JOIN [siebeldb].[dbo].[S_EVT_ACT] e ON c.ROW_ID = e.TARGET_PER_ID
    WHERE e.TODO_CD = ''Data Maintenance''
    GROUP BY c.EMAIL_ADDR, c.SUPPRESS_EMAIL_FLG
    HAVING COUNT(e.ROW_ID) > 0');
END;

-- ============================================
-- Stored Procedures
-- ============================================

-- Procedure to clean up old bounce records
IF EXISTS (SELECT * FROM sysobjects WHERE name='SP_CLEANUP_OLD_BOUNCES' AND xtype='P')
    DROP PROCEDURE [siebeldb].[dbo].[SP_CLEANUP_OLD_BOUNCES];

EXEC('CREATE PROCEDURE [siebeldb].[dbo].[SP_CLEANUP_OLD_BOUNCES]
    @DaysOld INT = 90
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Delete old bounce activity records
    DELETE FROM [siebeldb].[dbo].[S_EVT_ACT] 
    WHERE TODO_CD = ''Data Maintenance'' 
    AND CREATED < DATEADD(DAY, -@DaysOld, GETDATE());
    
    -- Delete old error messages
    DELETE FROM [scanner].[dbo].[MESSAGES] 
    WHERE ERROR_DATE < DATEADD(DAY, -@DaysOld, GETDATE());
    
    SELECT @@ROWCOUNT as RecordsDeleted;
END');

-- ============================================
-- Permissions
-- ============================================
-- Grant necessary permissions to the application user
-- Replace 'GetMailUser' with your actual application user

/*
-- Create application user (uncomment if needed)
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = ''GetMailUser'')
BEGIN
    CREATE LOGIN [GetMailUser] WITH PASSWORD = ''YourSecurePassword123!'';
END;

-- Create database user
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = ''GetMailUser'')
BEGIN
    USE [siebeldb];
    CREATE USER [GetMailUser] FOR LOGIN [GetMailUser];
    
    USE [scanner];
    CREATE USER [GetMailUser] FOR LOGIN [GetMailUser];
END;

-- Grant permissions
USE [siebeldb];
GRANT SELECT, INSERT, UPDATE, DELETE ON [dbo].[S_CONTACT] TO [GetMailUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON [dbo].[S_EVT_ACT] TO [GetMailUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON [dbo].[CX_CON_DEST] TO [GetMailUser];
GRANT EXECUTE ON [dbo].[SP_CLEANUP_OLD_BOUNCES] TO [GetMailUser];

USE [scanner];
GRANT SELECT, INSERT, UPDATE, DELETE ON [dbo].[MESSAGES] TO [GetMailUser];
*/

-- ============================================
-- Script Completion
-- ============================================
PRINT 'GetMail Database Schema created successfully!';
PRINT 'Remember to:';
PRINT '1. Update connection strings in web.config';
PRINT '2. Create and configure application user with appropriate permissions';
PRINT '3. Test the webhook endpoint with sample data';
PRINT '4. Set up log4net configuration for logging';
