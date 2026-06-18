CREATE TABLE [dbo].[metric_data_sources] (
    [id]              INT            IDENTITY (1, 1) NOT NULL,
    [type]            INT            NOT NULL,
    [host]            NVARCHAR (200) NOT NULL,
    [port]            INT            NOT NULL,
    [database]        NVARCHAR (100) NOT NULL,
    [username]        NVARCHAR (100) NOT NULL,
    [password]        NVARCHAR (500) NULL,
    [connection_name] NVARCHAR (200) NULL,
    [use_ssl]         BIT            NOT NULL,
    [company_id]      NVARCHAR (10)  NULL,
    [created_on]      DATETIME2 (7)  NULL,
    [modified_on]     DATETIME2 (7)  NULL,
    [created_by]      NVARCHAR (MAX) NULL,
    [modified_by]     NVARCHAR (MAX) NULL
);
GO

ALTER TABLE [dbo].[metric_data_sources]
    ADD CONSTRAINT [FK_metric_data_sources_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO

ALTER TABLE [dbo].[metric_data_sources]
    ADD CONSTRAINT [PK_metric_data_sources] PRIMARY KEY CLUSTERED ([id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_metric_data_sources_company_id]
    ON [dbo].[metric_data_sources]([company_id] ASC);
GO

