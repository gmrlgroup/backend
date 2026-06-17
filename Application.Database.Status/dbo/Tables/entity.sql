CREATE TABLE [dbo].[entity] (
    [id]          NVARCHAR (450)  NOT NULL,
    [name]        NVARCHAR (200)  NOT NULL,
    [description] NVARCHAR (1000) NULL,
    [entity_type] INT             NOT NULL,
    [url]         NVARCHAR (500)  NULL,
    [version]     NVARCHAR (100)  NULL,
    [owner]       NVARCHAR (200)  NULL,
    [location]    NVARCHAR (500)  NULL,
    [is_active]   BIT             NOT NULL,
    [is_critical] BIT             NOT NULL,
    [metadata]    NVARCHAR (4000) NULL,
    [company_id]  NVARCHAR (10)   NULL,
    [created_on]  DATETIME2 (7)   NULL,
    [modified_on] DATETIME2 (7)   NULL,
    [created_by]  NVARCHAR (MAX)  NULL,
    [modified_by] NVARCHAR (MAX)  NULL,
    [is_deleted]  BIT             NOT NULL,
    [deleted_by]  NVARCHAR (MAX)  NULL,
    [deleted_at]  DATETIME2 (7)   NULL,
    [group]       NVARCHAR (MAX)  NULL
);
GO

CREATE NONCLUSTERED INDEX [IX_entity_company_id]
    ON [dbo].[entity]([company_id] ASC);
GO

ALTER TABLE [dbo].[entity]
    ADD CONSTRAINT [PK_entity] PRIMARY KEY CLUSTERED ([id] ASC);
GO

