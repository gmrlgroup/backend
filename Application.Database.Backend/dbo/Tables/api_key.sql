CREATE TABLE [dbo].[api_key] (
    [id]           NVARCHAR (450) NOT NULL,
    [company_id]   NVARCHAR (10)  NOT NULL,
    [name]         NVARCHAR (100) NOT NULL,
    [key_hash]     NVARCHAR (128) NOT NULL,
    [key_prefix]   NVARCHAR (20)  NOT NULL,
    [expires_at]   DATETIME2 (7)  NULL,
    [revoked_at]   DATETIME2 (7)  NULL,
    [last_used_at] DATETIME2 (7)  NULL,
    [created_at]   DATETIME2 (7)  DEFAULT ('0001-01-01T00:00:00.0000000') NOT NULL,
    [created_by]   NVARCHAR (MAX) NULL,
    [modified_at]  DATETIME2 (7)  NULL
);
GO

ALTER TABLE [dbo].[api_key]
    ADD CONSTRAINT [PK_api_key] PRIMARY KEY CLUSTERED ([id] ASC);
GO

-- Lookups authenticate by hashing the presented key and matching key_hash, so it must be indexed.
CREATE NONCLUSTERED INDEX [IX_api_key_key_hash]
    ON [dbo].[api_key]([key_hash] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_api_key_company_id]
    ON [dbo].[api_key]([company_id] ASC);
GO

ALTER TABLE [dbo].[api_key]
    ADD CONSTRAINT [FK_api_key_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO
