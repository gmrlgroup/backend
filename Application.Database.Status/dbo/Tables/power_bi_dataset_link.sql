CREATE TABLE [dbo].[power_bi_dataset_link] (
    [id]                     NVARCHAR (450) NOT NULL,
    [entity_id]              NVARCHAR (450) NOT NULL,
    [power_bi_connection_id] NVARCHAR (450) NOT NULL,
    [workspace_id]           NVARCHAR (100) NOT NULL,
    [dataset_id]             NVARCHAR (100) NOT NULL,
    [company_id]             NVARCHAR (10)  NULL,
    [created_on]             DATETIME2 (7)  NULL,
    [modified_on]            DATETIME2 (7)  NULL,
    [created_by]             NVARCHAR (MAX) NULL,
    [modified_by]            NVARCHAR (MAX) NULL
);
GO

ALTER TABLE [dbo].[power_bi_dataset_link]
    ADD CONSTRAINT [PK_power_bi_dataset_link] PRIMARY KEY CLUSTERED ([id] ASC);
GO

-- One Power BI link per entity.
CREATE UNIQUE NONCLUSTERED INDEX [IX_power_bi_dataset_link_entity_id]
    ON [dbo].[power_bi_dataset_link]([entity_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_power_bi_dataset_link_company_id]
    ON [dbo].[power_bi_dataset_link]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_power_bi_dataset_link_power_bi_connection_id]
    ON [dbo].[power_bi_dataset_link]([power_bi_connection_id] ASC);
GO

ALTER TABLE [dbo].[power_bi_dataset_link]
    ADD CONSTRAINT [FK_power_bi_dataset_link_entity_entity_id] FOREIGN KEY ([entity_id]) REFERENCES [dbo].[entity] ([id]);
GO

ALTER TABLE [dbo].[power_bi_dataset_link]
    ADD CONSTRAINT [FK_power_bi_dataset_link_power_bi_connection_id] FOREIGN KEY ([power_bi_connection_id]) REFERENCES [dbo].[power_bi_connection] ([id]);
GO
