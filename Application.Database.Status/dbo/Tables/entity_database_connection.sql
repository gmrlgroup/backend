CREATE TABLE [dbo].[entity_database_connection] (
    [id]               NVARCHAR (450) NOT NULL,
    [entity_id]        NVARCHAR (450) NOT NULL,
    [database_type]    INT            NOT NULL,
    [host]             NVARCHAR (500) NULL,
    [port]             INT            NOT NULL,
    [database_name]    NVARCHAR (200) NULL,
    [username]         NVARCHAR (200) NULL,
    [secret_encrypted] NVARCHAR (MAX) NULL,
    [use_ssl]          BIT            NOT NULL,
    [file_path]        NVARCHAR (500) NULL,
    [company_id]       NVARCHAR (10)  NULL,
    [created_on]       DATETIME2 (7)  NULL,
    [modified_on]      DATETIME2 (7)  NULL,
    [created_by]       NVARCHAR (MAX) NULL,
    [modified_by]      NVARCHAR (MAX) NULL
);
GO

ALTER TABLE [dbo].[entity_database_connection]
    ADD CONSTRAINT [PK_entity_database_connection] PRIMARY KEY CLUSTERED ([id] ASC);
GO

-- One database connection per entity.
CREATE UNIQUE NONCLUSTERED INDEX [IX_entity_database_connection_entity_id]
    ON [dbo].[entity_database_connection]([entity_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_entity_database_connection_company_id]
    ON [dbo].[entity_database_connection]([company_id] ASC);
GO

ALTER TABLE [dbo].[entity_database_connection]
    ADD CONSTRAINT [FK_entity_database_connection_entity_entity_id] FOREIGN KEY ([entity_id]) REFERENCES [dbo].[entity] ([id]);
GO
