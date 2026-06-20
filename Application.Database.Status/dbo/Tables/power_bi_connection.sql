CREATE TABLE [dbo].[power_bi_connection] (
    [id]                      NVARCHAR (450) NOT NULL,
    [name]                    NVARCHAR (200) NOT NULL,
    [tenant_id]               NVARCHAR (100) NOT NULL,
    [client_id]               NVARCHAR (100) NOT NULL,
    [client_secret_encrypted] NVARCHAR (MAX) NULL,
    [is_active]               BIT            NOT NULL,
    [company_id]              NVARCHAR (10)  NULL,
    [created_on]              DATETIME2 (7)  NULL,
    [modified_on]             DATETIME2 (7)  NULL,
    [created_by]              NVARCHAR (MAX) NULL,
    [modified_by]             NVARCHAR (MAX) NULL
);
GO

ALTER TABLE [dbo].[power_bi_connection]
    ADD CONSTRAINT [PK_power_bi_connection] PRIMARY KEY CLUSTERED ([id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_power_bi_connection_company_id]
    ON [dbo].[power_bi_connection]([company_id] ASC);
GO
