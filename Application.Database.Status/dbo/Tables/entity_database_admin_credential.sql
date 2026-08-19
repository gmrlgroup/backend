CREATE TABLE [dbo].[entity_database_admin_credential] (
    [id]               NVARCHAR (450) NOT NULL,
    [entity_id]        NVARCHAR (450) NOT NULL,
    [username]         NVARCHAR (200) NULL,
    [secret_encrypted] NVARCHAR (MAX) NULL,
    [company_id]       NVARCHAR (10)  NULL,
    [created_on]       DATETIME2 (7)  NULL,
    [modified_on]      DATETIME2 (7)  NULL,
    [created_by]       NVARCHAR (MAX) NULL,
    [modified_by]      NVARCHAR (MAX) NULL
);
GO

ALTER TABLE [dbo].[entity_database_admin_credential]
    ADD CONSTRAINT [PK_entity_database_admin_credential] PRIMARY KEY CLUSTERED ([id] ASC);
GO

-- One admin credential per entity.
CREATE UNIQUE NONCLUSTERED INDEX [IX_entity_database_admin_credential_entity_id]
    ON [dbo].[entity_database_admin_credential]([entity_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_entity_database_admin_credential_company_id]
    ON [dbo].[entity_database_admin_credential]([company_id] ASC);
GO

ALTER TABLE [dbo].[entity_database_admin_credential]
    ADD CONSTRAINT [FK_entity_database_admin_credential_entity_entity_id] FOREIGN KEY ([entity_id]) REFERENCES [dbo].[entity] ([id]);
GO
