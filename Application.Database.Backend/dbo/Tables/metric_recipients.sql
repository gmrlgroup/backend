CREATE TABLE [dbo].[metric_recipients] (
    [id]             INT            IDENTITY (1, 1) NOT NULL,
    [metric_id]      INT            NOT NULL,
    [recipient_name] NVARCHAR (200) NOT NULL,
    [company_id]     NVARCHAR (10)  NULL,
    [created_on]     DATETIME2 (7)  NULL,
    [modified_on]    DATETIME2 (7)  NULL,
    [created_by]     NVARCHAR (MAX) NULL,
    [modified_by]    NVARCHAR (MAX) NULL
);
GO

CREATE NONCLUSTERED INDEX [IX_metric_recipients_company_id]
    ON [dbo].[metric_recipients]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_metric_recipients_metric_id]
    ON [dbo].[metric_recipients]([metric_id] ASC);
GO

ALTER TABLE [dbo].[metric_recipients]
    ADD CONSTRAINT [FK_metric_recipients_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO

ALTER TABLE [dbo].[metric_recipients]
    ADD CONSTRAINT [FK_metric_recipients_metrics_metric_id] FOREIGN KEY ([metric_id]) REFERENCES [dbo].[metrics] ([id]);
GO

ALTER TABLE [dbo].[metric_recipients]
    ADD CONSTRAINT [PK_metric_recipients] PRIMARY KEY CLUSTERED ([id] ASC);
GO

