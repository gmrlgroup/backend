CREATE TABLE [dbo].[incident_update] (
    [id]            NVARCHAR (450)  NOT NULL,
    [incident_id]   NVARCHAR (450)  NOT NULL,
    [message]       NVARCHAR (2000) NOT NULL,
    [status_change] INT             NULL,
    [author]        NVARCHAR (200)  NULL,
    [posted_at]     DATETIME2 (7)   NOT NULL,
    [company_id]    NVARCHAR (10)   NULL,
    [created_on]    DATETIME2 (7)   NULL,
    [modified_on]   DATETIME2 (7)   NULL,
    [created_by]    NVARCHAR (MAX)  NULL,
    [modified_by]   NVARCHAR (MAX)  NULL,
    [is_deleted]    BIT             NOT NULL,
    [deleted_by]    NVARCHAR (MAX)  NULL,
    [deleted_at]    DATETIME2 (7)   NULL
);
GO

CREATE NONCLUSTERED INDEX [IX_incident_update_company_id]
    ON [dbo].[incident_update]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_incident_update_incident_id]
    ON [dbo].[incident_update]([incident_id] ASC);
GO

ALTER TABLE [dbo].[incident_update]
    ADD CONSTRAINT [PK_incident_update] PRIMARY KEY CLUSTERED ([id] ASC);
GO

ALTER TABLE [dbo].[incident_update]
    ADD CONSTRAINT [FK_incident_update_incident_incident_id] FOREIGN KEY ([incident_id]) REFERENCES [dbo].[incident] ([id]) ON DELETE CASCADE;
GO

