CREATE TABLE [dbo].[alert_instance] (
    [id]                  NVARCHAR (450)  NOT NULL,
    [alert_rule_id]       NVARCHAR (450)  NOT NULL,
    [triggered_at]        DATETIME2 (7)   NOT NULL,
    [resolved_at]         DATETIME2 (7)   NULL,
    [is_resolved]         BIT             NOT NULL,
    [message]             NVARCHAR (2000) NULL,
    [details]             NVARCHAR (4000) NULL,
    [trigger_data]        NVARCHAR (2000) NULL,
    [email_sent]          BIT             NOT NULL,
    [sms_sent]            BIT             NOT NULL,
    [webhook_sent]        BIT             NOT NULL,
    [email_sent_at]       DATETIME2 (7)   NULL,
    [sms_sent_at]         DATETIME2 (7)   NULL,
    [webhook_sent_at]     DATETIME2 (7)   NULL,
    [notification_errors] NVARCHAR (1000) NULL,
    [company_id]          NVARCHAR (10)   NULL,
    [created_on]          DATETIME2 (7)   NULL,
    [modified_on]         DATETIME2 (7)   NULL,
    [created_by]          NVARCHAR (MAX)  NULL,
    [modified_by]         NVARCHAR (MAX)  NULL,
    [is_deleted]          BIT             NOT NULL,
    [deleted_by]          NVARCHAR (MAX)  NULL,
    [deleted_at]          DATETIME2 (7)   NULL
);
GO

CREATE NONCLUSTERED INDEX [IX_alert_instance_company_id]
    ON [dbo].[alert_instance]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_alert_instance_alert_rule_id]
    ON [dbo].[alert_instance]([alert_rule_id] ASC);
GO

ALTER TABLE [dbo].[alert_instance]
    ADD CONSTRAINT [PK_alert_instance] PRIMARY KEY CLUSTERED ([id] ASC);
GO

ALTER TABLE [dbo].[alert_instance]
    ADD CONSTRAINT [FK_alert_instance_alert_rule_alert_rule_id] FOREIGN KEY ([alert_rule_id]) REFERENCES [dbo].[alert_rule] ([id]) ON DELETE CASCADE;
GO

