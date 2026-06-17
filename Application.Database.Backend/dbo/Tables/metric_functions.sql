CREATE TABLE [dbo].[metric_functions] (
    [id]            INT            IDENTITY (1, 1) NOT NULL,
    [metric_id]     INT            NOT NULL,
    [function]      NVARCHAR (200) NOT NULL,
    [sub_function]  NVARCHAR (200) NULL,
    [function_head] NVARCHAR (200) NULL,
    [company_id]    NVARCHAR (10)  NULL,
    [created_on]    DATETIME2 (7)  NULL,
    [modified_on]   DATETIME2 (7)  NULL,
    [created_by]    NVARCHAR (MAX) NULL,
    [modified_by]   NVARCHAR (MAX) NULL
);
GO

ALTER TABLE [dbo].[metric_functions]
    ADD CONSTRAINT [FK_metric_functions_metrics_metric_id] FOREIGN KEY ([metric_id]) REFERENCES [dbo].[metrics] ([id]);
GO

ALTER TABLE [dbo].[metric_functions]
    ADD CONSTRAINT [FK_metric_functions_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO

CREATE NONCLUSTERED INDEX [IX_metric_functions_company_id]
    ON [dbo].[metric_functions]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_metric_functions_metric_id]
    ON [dbo].[metric_functions]([metric_id] ASC);
GO

ALTER TABLE [dbo].[metric_functions]
    ADD CONSTRAINT [PK_metric_functions] PRIMARY KEY CLUSTERED ([id] ASC);
GO

