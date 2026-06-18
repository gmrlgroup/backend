CREATE TABLE [dbo].[metric_targets] (
    [id]                 INT             IDENTITY (1, 1) NOT NULL,
    [metric_id]          INT             NOT NULL,
    [start_date]         DATETIME2 (7)   NOT NULL,
    [end_date]           DATETIME2 (7)   NULL,
    [min_target]         DECIMAL (18, 2) NULL,
    [max_target]         DECIMAL (18, 2) NULL,
    [optimal_target]     DECIMAL (18, 2) NULL,
    [target_description] NVARCHAR (500)  NULL,
    [set_by]             NVARCHAR (200)  NULL,
    [is_active]          BIT             NOT NULL,
    [company_id]         NVARCHAR (10)   NULL,
    [created_on]         DATETIME2 (7)   NULL,
    [modified_on]        DATETIME2 (7)   NULL,
    [created_by]         NVARCHAR (MAX)  NULL,
    [modified_by]        NVARCHAR (MAX)  NULL
);
GO

ALTER TABLE [dbo].[metric_targets]
    ADD CONSTRAINT [FK_metric_targets_metrics_metric_id] FOREIGN KEY ([metric_id]) REFERENCES [dbo].[metrics] ([id]);
GO

ALTER TABLE [dbo].[metric_targets]
    ADD CONSTRAINT [FK_metric_targets_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO

CREATE NONCLUSTERED INDEX [IX_metric_targets_company_id]
    ON [dbo].[metric_targets]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_metric_targets_metric_id]
    ON [dbo].[metric_targets]([metric_id] ASC);
GO

ALTER TABLE [dbo].[metric_targets]
    ADD CONSTRAINT [PK_metric_targets] PRIMARY KEY CLUSTERED ([id] ASC);
GO

