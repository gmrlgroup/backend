CREATE TABLE [dbo].[ingestion_run] (
    [id]            NVARCHAR (450) NOT NULL,
    [source_id]     NVARCHAR (450) NOT NULL,
    [company_id]    NVARCHAR (10)  NOT NULL,
    [started_at]    DATETIME2 (7)  NOT NULL,
    [finished_at]   DATETIME2 (7)  NULL,
    [status]        NVARCHAR (40)  NOT NULL,
    [rows_ingested] INT            NULL,
    [error_message] NVARCHAR (MAX) NULL,
    [job_id]        NVARCHAR (100) NULL,
    [log]           NVARCHAR (MAX) NULL
);
GO

ALTER TABLE [dbo].[ingestion_run]
    ADD CONSTRAINT [PK_ingestion_run] PRIMARY KEY CLUSTERED ([id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_ingestion_run_source_id]
    ON [dbo].[ingestion_run]([source_id] ASC);
GO

ALTER TABLE [dbo].[ingestion_run]
    ADD CONSTRAINT [FK_ingestion_run_ingestion_source_source_id] FOREIGN KEY ([source_id]) REFERENCES [dbo].[ingestion_source] ([id]);
GO
