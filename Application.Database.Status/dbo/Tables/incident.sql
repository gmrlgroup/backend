CREATE TABLE [dbo].[incident] (
    [id]                   NVARCHAR (450)  NOT NULL,
    [entity_id]            NVARCHAR (450)  NOT NULL,
    [title]                NVARCHAR (200)  NOT NULL,
    [description]          NVARCHAR (4000) NOT NULL,
    [severity]             INT             NOT NULL,
    [status]               INT             NOT NULL,
    [started_at]           DATETIME2 (7)   NOT NULL,
    [resolved_at]          DATETIME2 (7)   NULL,
    [reported_by]          NVARCHAR (200)  NULL,
    [assigned_to]          NVARCHAR (200)  NULL,
    [impact_description]   NVARCHAR (1000) NULL,
    [resolution_details]   NVARCHAR (2000) NULL,
    [external_incident_id] NVARCHAR (100)  NULL,
    [metadata]             NVARCHAR (4000) NULL,
    [company_id]           NVARCHAR (10)   NULL,
    [created_on]           DATETIME2 (7)   NULL,
    [modified_on]          DATETIME2 (7)   NULL,
    [created_by]           NVARCHAR (MAX)  NULL,
    [modified_by]          NVARCHAR (MAX)  NULL,
    [is_deleted]           BIT             NOT NULL,
    [deleted_by]           NVARCHAR (MAX)  NULL,
    [deleted_at]           DATETIME2 (7)   NULL
);
GO

ALTER TABLE [dbo].[incident]
    ADD CONSTRAINT [FK_incident_entity_entity_id] FOREIGN KEY ([entity_id]) REFERENCES [dbo].[entity] ([id]) ON DELETE CASCADE;
GO

ALTER TABLE [dbo].[incident]
    ADD CONSTRAINT [PK_incident] PRIMARY KEY CLUSTERED ([id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_incident_company_id]
    ON [dbo].[incident]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_incident_entity_id]
    ON [dbo].[incident]([entity_id] ASC);
GO

