CREATE TABLE [dbo].[alert_rule] (
    [id]               NVARCHAR (450)  NOT NULL,
    [name]             NVARCHAR (200)  NOT NULL,
    [description]      NVARCHAR (1000) NULL,
    [entity_id]        NVARCHAR (450)  NULL,
    [conditions]       NVARCHAR (4000) NOT NULL,
    [severity]         INT             NOT NULL,
    [is_active]        BIT             NOT NULL,
    [send_email]       BIT             NOT NULL,
    [send_sms]         BIT             NOT NULL,
    [send_webhook]     BIT             NOT NULL,
    [email_recipients] NVARCHAR (1000) NULL,
    [sms_recipients]   NVARCHAR (500)  NULL,
    [webhook_url]      NVARCHAR (500)  NULL,
    [cooldown_minutes] INT             NOT NULL,
    [last_triggered]   DATETIME2 (7)   NULL,
    [company_id]       NVARCHAR (10)   NULL,
    [created_on]       DATETIME2 (7)   NULL,
    [modified_on]      DATETIME2 (7)   NULL,
    [created_by]       NVARCHAR (MAX)  NULL,
    [modified_by]      NVARCHAR (MAX)  NULL,
    [is_deleted]       BIT             NOT NULL,
    [deleted_by]       NVARCHAR (MAX)  NULL,
    [deleted_at]       DATETIME2 (7)   NULL
);
GO

ALTER TABLE [dbo].[alert_rule]
    ADD CONSTRAINT [FK_alert_rule_entity_entity_id] FOREIGN KEY ([entity_id]) REFERENCES [dbo].[entity] ([id]) ON DELETE SET NULL;
GO

CREATE NONCLUSTERED INDEX [IX_alert_rule_entity_id]
    ON [dbo].[alert_rule]([entity_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_alert_rule_company_id]
    ON [dbo].[alert_rule]([company_id] ASC);
GO

ALTER TABLE [dbo].[alert_rule]
    ADD CONSTRAINT [PK_alert_rule] PRIMARY KEY CLUSTERED ([id] ASC);
GO

