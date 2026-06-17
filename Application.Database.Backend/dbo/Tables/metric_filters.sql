CREATE TABLE [dbo].[metric_filters] (
    [id]             INT             IDENTITY (1, 1) NOT NULL,
    [metric_id]      INT             NOT NULL,
    [column_name]    NVARCHAR (100)  NOT NULL,
    [filter_label]   NVARCHAR (100)  NOT NULL,
    [filter_type]    NVARCHAR (50)   NOT NULL,
    [default_value]  NVARCHAR (1000) NULL,
    [select_options] NVARCHAR (2000) NULL,
    [is_required]    BIT             NOT NULL,
    [sort_order]     INT             NOT NULL,
    [placeholder]    NVARCHAR (500)  NULL,
    [description]    NVARCHAR (1000) NULL,
    [company_id]     NVARCHAR (10)   NULL,
    [created_on]     DATETIME2 (7)   NULL,
    [modified_on]    DATETIME2 (7)   NULL,
    [created_by]     NVARCHAR (MAX)  NULL,
    [modified_by]    NVARCHAR (MAX)  NULL,
    [operator]       NVARCHAR (20)   DEFAULT (N'') NOT NULL
);
GO

ALTER TABLE [dbo].[metric_filters]
    ADD CONSTRAINT [PK_metric_filters] PRIMARY KEY CLUSTERED ([id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_metric_filters_company_id]
    ON [dbo].[metric_filters]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_metric_filters_metric_id]
    ON [dbo].[metric_filters]([metric_id] ASC);
GO

ALTER TABLE [dbo].[metric_filters]
    ADD CONSTRAINT [FK_metric_filters_metrics_metric_id] FOREIGN KEY ([metric_id]) REFERENCES [dbo].[metrics] ([id]);
GO

ALTER TABLE [dbo].[metric_filters]
    ADD CONSTRAINT [FK_metric_filters_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO

