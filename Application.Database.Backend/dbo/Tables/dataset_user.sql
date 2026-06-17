CREATE TABLE [dbo].[dataset_user] (
    [dataset_id]  NVARCHAR (450) NOT NULL,
    [user_id]     NVARCHAR (450) NOT NULL,
    [type]        INT            NOT NULL,
    [created_at]  DATETIME2 (7)  NOT NULL,
    [modified_at] DATETIME2 (7)  NULL
);
GO

ALTER TABLE [dbo].[dataset_user]
    ADD CONSTRAINT [PK_dataset_user] PRIMARY KEY CLUSTERED ([dataset_id] ASC, [user_id] ASC);
GO

ALTER TABLE [dbo].[dataset_user]
    ADD CONSTRAINT [FK_dataset_user_dataset_dataset_id] FOREIGN KEY ([dataset_id]) REFERENCES [dbo].[dataset] ([id]);
GO

