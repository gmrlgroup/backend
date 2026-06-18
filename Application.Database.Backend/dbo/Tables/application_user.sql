CREATE TABLE [dbo].[application_user] (
    [id]                     NVARCHAR (450)     NOT NULL,
    [created_on]             DATETIME2 (7)      NULL,
    [modified_on]            DATETIME2 (7)      NULL,
    [created_by]             NVARCHAR (MAX)     NULL,
    [modified_by]            NVARCHAR (MAX)     NULL,
    [is_deleted]             BIT                NULL,
    [user_name]              NVARCHAR (MAX)     NULL,
    [normalized_user_name]   NVARCHAR (MAX)     NULL,
    [email]                  NVARCHAR (MAX)     NULL,
    [normalized_email]       NVARCHAR (MAX)     NULL,
    [email_confirmed]        BIT                NOT NULL,
    [password_hash]          NVARCHAR (MAX)     NULL,
    [security_stamp]         NVARCHAR (MAX)     NULL,
    [concurrency_stamp]      NVARCHAR (MAX)     NULL,
    [phone_number]           NVARCHAR (MAX)     NULL,
    [phone_number_confirmed] BIT                NOT NULL,
    [two_factor_enabled]     BIT                NOT NULL,
    [lockout_end]            DATETIMEOFFSET (7) NULL,
    [lockout_enabled]        BIT                NOT NULL,
    [access_failed_count]    INT                NOT NULL,
    [deleted_at]             DATETIME2 (7)      NULL,
    [deleted_by]             NVARCHAR (MAX)     NULL
);
GO

ALTER TABLE [dbo].[application_user]
    ADD CONSTRAINT [PK_application_user] PRIMARY KEY CLUSTERED ([id] ASC);
GO

