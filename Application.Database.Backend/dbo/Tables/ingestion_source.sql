CREATE TABLE [dbo].[ingestion_source] (
    [id]                    NVARCHAR (450)  NOT NULL,
    [company_id]            NVARCHAR (10)   NOT NULL,
    [dataset_id]            NVARCHAR (450)  NOT NULL,
    [target_table]          NVARCHAR (150)  NOT NULL,
    [name]                  NVARCHAR (150)  NOT NULL,
    [description]           NVARCHAR (500)  NULL,
    [source_kind]           NVARCHAR (40)   NOT NULL,
    [source_entity_id]      NVARCHAR (450)  NULL,
    [source_config]         NVARCHAR (MAX)  NULL,
    [secret_encrypted]      NVARCHAR (MAX)  NULL,
    [import_mode]           NVARCHAR (20)   NOT NULL,
    [key_columns]           NVARCHAR (MAX)  NULL,
    [create_if_not_exists]  BIT             DEFAULT ((1)) NOT NULL,
    [incremental_column]    NVARCHAR (150)  NULL,
    [incremental_last_value] NVARCHAR (MAX) NULL,
    [cron_expression]       NVARCHAR (120)  NOT NULL,
    [time_zone]             NVARCHAR (80)   NULL,
    [is_enabled]            BIT             NOT NULL,
    [last_run_at]           DATETIME2 (7)   NULL,
    [last_run_status]       NVARCHAR (40)   NULL,
    [last_run_message]      NVARCHAR (MAX)  NULL,
    [last_run_rows]         INT             NULL,
    [created_at]            DATETIME2 (7)   DEFAULT ('0001-01-01T00:00:00.0000000') NOT NULL,
    [created_by]            NVARCHAR (450)  NULL,
    [modified_at]           DATETIME2 (7)   NULL
);
GO

ALTER TABLE [dbo].[ingestion_source]
    ADD CONSTRAINT [PK_ingestion_source] PRIMARY KEY CLUSTERED ([id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ingestion_source_company_id]
    ON [dbo].[ingestion_source]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ingestion_source_dataset_id]
    ON [dbo].[ingestion_source]([dataset_id] ASC);
GO

ALTER TABLE [dbo].[ingestion_source]
    ADD CONSTRAINT [FK_ingestion_source_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO

ALTER TABLE [dbo].[ingestion_source]
    ADD CONSTRAINT [FK_ingestion_source_dataset_dataset_id] FOREIGN KEY ([dataset_id]) REFERENCES [dbo].[dataset] ([id]);
GO
