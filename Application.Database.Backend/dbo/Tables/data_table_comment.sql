CREATE TABLE [dbo].[data_table_comment] (
    [id]                 NVARCHAR (450) NOT NULL,
    [dataset_id]         NVARCHAR (MAX) NOT NULL,
    [table_name]         NVARCHAR (MAX) NOT NULL,
    [user_id]            NVARCHAR (MAX) NOT NULL,
    [content]            NVARCHAR (MAX) NOT NULL,
    [mentioned_user_ids] NVARCHAR (MAX) NOT NULL,
    [created_at]         DATETIME2 (7)  NOT NULL,
    [updated_at]         DATETIME2 (7)  NULL,
    [user_name]          NVARCHAR (MAX) NOT NULL,
    [user_email]         NVARCHAR (MAX) NOT NULL
);
GO

ALTER TABLE [dbo].[data_table_comment]
    ADD CONSTRAINT [PK_data_table_comment] PRIMARY KEY CLUSTERED ([id] ASC);
GO

