CREATE TABLE [dbo].[entity_table_check] (
    [id]               NVARCHAR (450) NOT NULL,
    [entity_id]        NVARCHAR (450) NOT NULL,
    [freshness_column] NVARCHAR (200) NULL,
    [max_age_minutes]  INT            NOT NULL,
    [is_enabled]       BIT            NOT NULL,
    [company_id]       NVARCHAR (10)  NULL,
    [created_on]       DATETIME2 (7)  NULL,
    [modified_on]      DATETIME2 (7)  NULL,
    [created_by]       NVARCHAR (MAX) NULL,
    [modified_by]      NVARCHAR (MAX) NULL
);
GO

ALTER TABLE [dbo].[entity_table_check]
    ADD CONSTRAINT [PK_entity_table_check] PRIMARY KEY CLUSTERED ([id] ASC);
GO

-- One freshness check per Table entity.
CREATE UNIQUE NONCLUSTERED INDEX [IX_entity_table_check_entity_id]
    ON [dbo].[entity_table_check]([entity_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_entity_table_check_company_id]
    ON [dbo].[entity_table_check]([company_id] ASC);
GO

ALTER TABLE [dbo].[entity_table_check]
    ADD CONSTRAINT [FK_entity_table_check_entity_entity_id] FOREIGN KEY ([entity_id]) REFERENCES [dbo].[entity] ([id]);
GO
