CREATE TABLE [dbo].[job_execution] (
    [id]                     NVARCHAR (450)  NOT NULL,
    [job_id]                 NVARCHAR (450)  NOT NULL,
    [status]                 INT             NOT NULL,
    [start_time]             DATETIME2 (7)   NOT NULL,
    [end_time]               DATETIME2 (7)   NULL,
    [execution_time_seconds] FLOAT (53)      NULL,
    [result]                 NVARCHAR (4000) NULL,
    [error_message]          NVARCHAR (4000) NULL,
    [output]                 NVARCHAR (2000) NULL,
    [exit_code]              INT             NULL,
    [retry_attempt]          INT             NOT NULL,
    [triggered_by]           NVARCHAR (100)  NULL,
    [metadata]               NVARCHAR (2000) NULL,
    [company_id]             NVARCHAR (10)   NULL,
    [created_on]             DATETIME2 (7)   NULL,
    [modified_on]            DATETIME2 (7)   NULL,
    [created_by]             NVARCHAR (MAX)  NULL,
    [modified_by]            NVARCHAR (MAX)  NULL,
    [is_deleted]             BIT             NOT NULL,
    [deleted_by]             NVARCHAR (MAX)  NULL,
    [deleted_at]             DATETIME2 (7)   NULL
);
GO

ALTER TABLE [dbo].[job_execution]
    ADD CONSTRAINT [PK_job_execution] PRIMARY KEY CLUSTERED ([id] ASC);
GO

ALTER TABLE [dbo].[job_execution]
    ADD CONSTRAINT [FK_job_execution_job_job_id] FOREIGN KEY ([job_id]) REFERENCES [dbo].[job] ([id]) ON DELETE CASCADE;
GO

CREATE NONCLUSTERED INDEX [IX_job_execution_company_id]
    ON [dbo].[job_execution]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_job_execution_job_id]
    ON [dbo].[job_execution]([job_id] ASC);
GO

