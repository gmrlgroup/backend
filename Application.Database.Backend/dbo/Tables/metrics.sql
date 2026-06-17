CREATE TABLE [dbo].[metrics] (
    [id]                      INT             IDENTITY (1, 1) NOT NULL,
    [contact_email]           NVARCHAR (200)  NULL,
    [contact_number]          NVARCHAR (50)   NULL,
    [key_performance_area]    NVARCHAR (300)  NOT NULL,
    [kpi]                     NVARCHAR (500)  NOT NULL,
    [formula]                 NVARCHAR (1000) NULL,
    [type]                    INT             NOT NULL,
    [perspective]             INT             NOT NULL,
    [kpilevel]                INT             NOT NULL,
    [target]                  NVARCHAR (200)  NULL,
    [unintended_consequences] NVARCHAR (1000) NULL,
    [mitigating_factors]      NVARCHAR (1000) NULL,
    [unit_of_measure]         NVARCHAR (100)  NULL,
    [kpicontrols]             NVARCHAR (2000) NULL,
    [data_capture]            INT             NULL,
    [data_reporting]          INT             NULL,
    [polarity]                INT             NOT NULL,
    [data_source]             NVARCHAR (300)  NULL,
    [data_integrity]          INT             NULL,
    [revision_date]           DATETIME2 (7)   NULL,
    [data_ready]              BIT             NOT NULL,
    [report]                  NVARCHAR (200)  NULL,
    [comment]                 NVARCHAR (2000) NULL,
    [is_active]               BIT             NOT NULL,
    [company_id]              NVARCHAR (10)   NULL,
    [created_on]              DATETIME2 (7)   NULL,
    [modified_on]             DATETIME2 (7)   NULL,
    [created_by]              NVARCHAR (MAX)  NULL,
    [modified_by]             NVARCHAR (MAX)  NULL,
    [query]                   NVARCHAR (MAX)  NULL,
    [metric_data_source_id]   INT             NULL
);
GO

ALTER TABLE [dbo].[metrics]
    ADD CONSTRAINT [PK_metrics] PRIMARY KEY CLUSTERED ([id] ASC);
GO

ALTER TABLE [dbo].[metrics]
    ADD CONSTRAINT [FK_metrics_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO

ALTER TABLE [dbo].[metrics]
    ADD CONSTRAINT [FK_metrics_metric_data_sources_metric_data_source_id] FOREIGN KEY ([metric_data_source_id]) REFERENCES [dbo].[metric_data_sources] ([id]);
GO

CREATE NONCLUSTERED INDEX [IX_metrics_company_id]
    ON [dbo].[metrics]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_metrics_metric_data_source_id]
    ON [dbo].[metrics]([metric_data_source_id] ASC);
GO

