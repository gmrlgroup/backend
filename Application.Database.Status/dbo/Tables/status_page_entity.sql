CREATE TABLE [dbo].[status_page_entity] (
    [id]             NVARCHAR (450) NOT NULL,
    [status_page_id] NVARCHAR (450) NOT NULL,
    [entity_id]      NVARCHAR (450) NOT NULL,
    [display_order]  INT            NOT NULL,
    [is_visible]     BIT            NOT NULL,
    [group_name]     NVARCHAR (100) NULL,
    [company_id]     NVARCHAR (10)  NULL,
    [created_on]     DATETIME2 (7)  NULL,
    [modified_on]    DATETIME2 (7)  NULL,
    [created_by]     NVARCHAR (MAX) NULL,
    [modified_by]    NVARCHAR (MAX) NULL,
    [is_deleted]     BIT            NOT NULL,
    [deleted_by]     NVARCHAR (MAX) NULL,
    [deleted_at]     DATETIME2 (7)  NULL
);
GO

ALTER TABLE [dbo].[status_page_entity]
    ADD CONSTRAINT [FK_status_page_entity_status_page_status_page_id] FOREIGN KEY ([status_page_id]) REFERENCES [dbo].[status_page] ([id]) ON DELETE CASCADE;
GO

ALTER TABLE [dbo].[status_page_entity]
    ADD CONSTRAINT [FK_status_page_entity_entity_entity_id] FOREIGN KEY ([entity_id]) REFERENCES [dbo].[entity] ([id]) ON DELETE CASCADE;
GO

ALTER TABLE [dbo].[status_page_entity]
    ADD CONSTRAINT [PK_status_page_entity] PRIMARY KEY CLUSTERED ([id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_status_page_entity_status_page_id]
    ON [dbo].[status_page_entity]([status_page_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_status_page_entity_entity_id]
    ON [dbo].[status_page_entity]([entity_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_status_page_entity_company_id]
    ON [dbo].[status_page_entity]([company_id] ASC);
GO

