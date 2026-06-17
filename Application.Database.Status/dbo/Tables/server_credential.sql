CREATE TABLE [dbo].[server_credential] (
    [id]               NVARCHAR (450) NOT NULL,
    [entity_id]        NVARCHAR (450) NOT NULL,
    [name]             NVARCHAR (200) NOT NULL,
    [platform]         INT            NOT NULL,
    [auth_type]        INT            NOT NULL,
    [host]             NVARCHAR (500) NULL,
    [port]             INT            NOT NULL,
    [username]         NVARCHAR (200) NULL,
    [secret_encrypted] NVARCHAR (MAX) NULL,
    [is_default]       BIT            NOT NULL,
    [company_id]       NVARCHAR (10)  NULL,
    [created_on]       DATETIME2 (7)  NULL,
    [modified_on]      DATETIME2 (7)  NULL,
    [created_by]       NVARCHAR (MAX) NULL,
    [modified_by]      NVARCHAR (MAX) NULL
);
GO

ALTER TABLE [dbo].[server_credential]
    ADD CONSTRAINT [PK_server_credential] PRIMARY KEY CLUSTERED ([id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_server_credential_entity_id]
    ON [dbo].[server_credential]([entity_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_server_credential_company_id]
    ON [dbo].[server_credential]([company_id] ASC);
GO

