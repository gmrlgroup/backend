CREATE TABLE [dbo].[entity_audience] (
    [id]                  NVARCHAR (450) NOT NULL,
    [entity_id]           NVARCHAR (450) NOT NULL,
    [application_user_id] NVARCHAR (450) NOT NULL,
    [email]               NVARCHAR (256) NOT NULL,
    [display_name]        NVARCHAR (256) NULL,
    [audience_type]       INT            NOT NULL,
    [is_active]           BIT            NOT NULL,
    [company_id]          NVARCHAR (10)  NULL,
    [created_on]          DATETIME2 (7)  NULL,
    [modified_on]         DATETIME2 (7)  NULL,
    [created_by]          NVARCHAR (MAX) NULL,
    [modified_by]         NVARCHAR (MAX) NULL
);
GO

ALTER TABLE [dbo].[entity_audience]
    ADD CONSTRAINT [PK_entity_audience] PRIMARY KEY CLUSTERED ([id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_entity_audience_entity_id]
    ON [dbo].[entity_audience]([entity_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_entity_audience_company_id]
    ON [dbo].[entity_audience]([company_id] ASC);
GO

ALTER TABLE [dbo].[entity_audience]
    ADD CONSTRAINT [FK_entity_audience_entity_entity_id] FOREIGN KEY ([entity_id]) REFERENCES [dbo].[entity] ([id]);
GO
