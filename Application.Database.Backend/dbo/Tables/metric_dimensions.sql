CREATE TABLE [dbo].[metric_dimensions] (
    [id]            INT            IDENTITY (1, 1) NOT NULL,
    [metric_id]     INT            NOT NULL,
    [name]          NVARCHAR (200) NOT NULL,
    [description]   NVARCHAR (500) NULL,
    [source_table]  NVARCHAR (200) NULL,
    [source_column] NVARCHAR (200) NULL,
    [company_id]    NVARCHAR (10)  NULL,
    [created_on]    DATETIME2 (7)  NULL,
    [modified_on]   DATETIME2 (7)  NULL,
    [created_by]    NVARCHAR (MAX) NULL,
    [modified_by]   NVARCHAR (MAX) NULL
);
GO

ALTER TABLE [dbo].[metric_dimensions]
    ADD CONSTRAINT [FK_metric_dimensions_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO

ALTER TABLE [dbo].[metric_dimensions]
    ADD CONSTRAINT [FK_metric_dimensions_metrics_metric_id] FOREIGN KEY ([metric_id]) REFERENCES [dbo].[metrics] ([id]);
GO

CREATE NONCLUSTERED INDEX [IX_metric_dimensions_company_id]
    ON [dbo].[metric_dimensions]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_metric_dimensions_metric_id]
    ON [dbo].[metric_dimensions]([metric_id] ASC);
GO

ALTER TABLE [dbo].[metric_dimensions]
    ADD CONSTRAINT [PK_metric_dimensions] PRIMARY KEY CLUSTERED ([id] ASC);
GO

