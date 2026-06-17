CREATE TABLE [dbo].[entity_dependency] (
    [id]                   NVARCHAR (450) NOT NULL,
    [entity_id]            NVARCHAR (450) NOT NULL,
    [depends_on_entity_id] NVARCHAR (450) NOT NULL,
    [description]          NVARCHAR (500) NULL,
    [is_active]            BIT            NOT NULL,
    [is_critical]          BIT            NOT NULL,
    [dependency_type]      INT            NULL,
    [order]                INT            NOT NULL,
    [company_id]           NVARCHAR (10)  NULL,
    [created_on]           DATETIME2 (7)  NULL,
    [modified_on]          DATETIME2 (7)  NULL,
    [created_by]           NVARCHAR (MAX) NULL,
    [modified_by]          NVARCHAR (MAX) NULL,
    [is_deleted]           BIT            NOT NULL,
    [deleted_by]           NVARCHAR (MAX) NULL,
    [deleted_at]           DATETIME2 (7)  NULL
);
GO

ALTER TABLE [dbo].[entity_dependency]
    ADD CONSTRAINT [PK_entity_dependency] PRIMARY KEY CLUSTERED ([id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_entity_dependency_entity_id]
    ON [dbo].[entity_dependency]([entity_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_entity_dependency_company_id]
    ON [dbo].[entity_dependency]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_entity_dependency_depends_on_entity_id]
    ON [dbo].[entity_dependency]([depends_on_entity_id] ASC);
GO

ALTER TABLE [dbo].[entity_dependency]
    ADD CONSTRAINT [FK_entity_dependency_entity_depends_on_entity_id] FOREIGN KEY ([depends_on_entity_id]) REFERENCES [dbo].[entity] ([id]);
GO

ALTER TABLE [dbo].[entity_dependency]
    ADD CONSTRAINT [FK_entity_dependency_entity_entity_id] FOREIGN KEY ([entity_id]) REFERENCES [dbo].[entity] ([id]);
GO

