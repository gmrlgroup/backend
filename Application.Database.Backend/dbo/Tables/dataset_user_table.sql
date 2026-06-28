CREATE TABLE [dbo].[dataset_user_table] (
    [dataset_id] NVARCHAR (450) NOT NULL,
    [user_id]    NVARCHAR (450) NOT NULL,
    [table_name] NVARCHAR (450) NOT NULL,
    [created_at] DATETIME2 (7)  NOT NULL
);
GO

ALTER TABLE [dbo].[dataset_user_table]
    ADD CONSTRAINT [PK_dataset_user_table] PRIMARY KEY CLUSTERED ([dataset_id] ASC, [user_id] ASC, [table_name] ASC);
GO

ALTER TABLE [dbo].[dataset_user_table]
    ADD CONSTRAINT [FK_dataset_user_table_dataset_dataset_id] FOREIGN KEY ([dataset_id]) REFERENCES [dbo].[dataset] ([id]);
GO
