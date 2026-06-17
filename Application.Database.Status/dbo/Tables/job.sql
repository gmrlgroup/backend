CREATE TABLE [dbo].[job] (
    [id]                     NVARCHAR (450)  NOT NULL,
    [name]                   NVARCHAR (200)  NOT NULL,
    [description]            NVARCHAR (1000) NULL,
    [job_type]               INT             NOT NULL,
    [trigger_type]           INT             NOT NULL,
    [status]                 INT             NOT NULL,
    [entity_id]              NVARCHAR (450)  NULL,
    [cron_expression]        NVARCHAR (100)  NULL,
    [sensor_config]          NVARCHAR (2000) NULL,
    [command]                NVARCHAR (4000) NULL,
    [timeout_seconds]        INT             NOT NULL,
    [max_retries]            INT             NOT NULL,
    [retry_interval_seconds] INT             NOT NULL,
    [is_active]              BIT             NOT NULL,
    [next_run_time]          DATETIME2 (7)   NULL,
    [last_run_time]          DATETIME2 (7)   NULL,
    [last_success_time]      DATETIME2 (7)   NULL,
    [last_result]            NVARCHAR (2000) NULL,
    [last_error]             NVARCHAR (4000) NULL,
    [success_rate]           FLOAT (53)      NULL,
    [average_execution_time] FLOAT (53)      NULL,
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

CREATE NONCLUSTERED INDEX [IX_job_company_id]
    ON [dbo].[job]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_job_entity_id]
    ON [dbo].[job]([entity_id] ASC);
GO

ALTER TABLE [dbo].[job]
    ADD CONSTRAINT [FK_job_entity_entity_id] FOREIGN KEY ([entity_id]) REFERENCES [dbo].[entity] ([id]) ON DELETE SET NULL;
GO

ALTER TABLE [dbo].[job]
    ADD CONSTRAINT [PK_job] PRIMARY KEY CLUSTERED ([id] ASC);
GO

