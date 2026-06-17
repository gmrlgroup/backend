CREATE TABLE [dbo].[entity_status_history] (
    [entity_id]         NVARCHAR (450)  NOT NULL,
    [status]            INT             NOT NULL,
    [status_message]    NVARCHAR (2000) NULL,
    [response_time]     FLOAT (53)      NULL,
    [uptime_percentage] FLOAT (53)      NULL,
    [checked_at]        DATETIME2 (7)   NOT NULL,
    [company_id]        NVARCHAR (10)   NULL,
    [created_on]        DATETIME2 (7)   NULL,
    [modified_on]       DATETIME2 (7)   NULL,
    [created_by]        NVARCHAR (MAX)  NULL,
    [modified_by]       NVARCHAR (MAX)  NULL,
    [is_deleted]        BIT             NOT NULL,
    [deleted_by]        NVARCHAR (MAX)  NULL,
    [deleted_at]        DATETIME2 (7)   NULL,
    [id]                INT             IDENTITY (1, 1) NOT NULL
);
GO

CREATE NONCLUSTERED INDEX [IX_entity_status_history_company_id]
    ON [dbo].[entity_status_history]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_entity_status_history_entity_id]
    ON [dbo].[entity_status_history]([entity_id] ASC);
GO

ALTER TABLE [dbo].[entity_status_history]
    ADD CONSTRAINT [FK_entity_status_history_entity_entity_id] FOREIGN KEY ([entity_id]) REFERENCES [dbo].[entity] ([id]);
GO

ALTER TABLE [dbo].[entity_status_history]
    ADD CONSTRAINT [PK_entity_status_history] PRIMARY KEY CLUSTERED ([id] ASC);
GO

