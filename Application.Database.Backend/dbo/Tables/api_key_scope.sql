CREATE TABLE [dbo].[api_key_scope] (
    [id]         NVARCHAR (450) NOT NULL,
    [api_key_id] NVARCHAR (450) NOT NULL,
    [dataset_id] NVARCHAR (450) NOT NULL,
    [table_name] NVARCHAR (200) NULL,
    [can_read]   BIT            NOT NULL,
    [can_import] BIT            NOT NULL,
    [created_at] DATETIME2 (7)  DEFAULT ('0001-01-01T00:00:00.0000000') NOT NULL
);
GO

ALTER TABLE [dbo].[api_key_scope]
    ADD CONSTRAINT [PK_api_key_scope] PRIMARY KEY CLUSTERED ([id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_api_key_scope_api_key_id]
    ON [dbo].[api_key_scope]([api_key_id] ASC);
GO

ALTER TABLE [dbo].[api_key_scope]
    ADD CONSTRAINT [FK_api_key_scope_api_key_api_key_id] FOREIGN KEY ([api_key_id]) REFERENCES [dbo].[api_key] ([id]);
GO

ALTER TABLE [dbo].[api_key_scope]
    ADD CONSTRAINT [FK_api_key_scope_dataset_dataset_id] FOREIGN KEY ([dataset_id]) REFERENCES [dbo].[dataset] ([id]);
GO
