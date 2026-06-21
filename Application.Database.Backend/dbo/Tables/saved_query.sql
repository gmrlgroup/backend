CREATE TABLE [dbo].[saved_query] (
    [id]          NVARCHAR (450) NOT NULL,
    [company_id]  NVARCHAR (10)  NOT NULL,
    [dataset_id]  NVARCHAR (450) NOT NULL,
    [name]        NVARCHAR (150) NOT NULL,
    [description] NVARCHAR (500) NULL,
    [query_text]  NVARCHAR (MAX) NOT NULL,
    [is_shared]   BIT            NOT NULL,
    [created_at]  DATETIME2 (7)  DEFAULT ('0001-01-01T00:00:00.0000000') NOT NULL,
    [created_by]  NVARCHAR (450) NULL,
    [modified_at] DATETIME2 (7)  NULL
);
GO

ALTER TABLE [dbo].[saved_query]
    ADD CONSTRAINT [PK_saved_query] PRIMARY KEY CLUSTERED ([id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_saved_query_company_id]
    ON [dbo].[saved_query]([company_id] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_saved_query_dataset_id]
    ON [dbo].[saved_query]([dataset_id] ASC);
GO

ALTER TABLE [dbo].[saved_query]
    ADD CONSTRAINT [FK_saved_query_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO

ALTER TABLE [dbo].[saved_query]
    ADD CONSTRAINT [FK_saved_query_dataset_dataset_id] FOREIGN KEY ([dataset_id]) REFERENCES [dbo].[dataset] ([id]);
GO
