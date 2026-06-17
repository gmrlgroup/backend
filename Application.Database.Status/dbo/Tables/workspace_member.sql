CREATE TABLE [dbo].[workspace_member] (
    [workspace_id]        NVARCHAR (10)  NOT NULL,
    [application_user_id] NVARCHAR (450) NOT NULL,
    [role]                INT            NOT NULL,
    [joined_date]         DATETIME2 (7)  NULL,
    [is_active]           BIT            NOT NULL,
    [notes]               NVARCHAR (200) NULL
);
GO

ALTER TABLE [dbo].[workspace_member]
    ADD CONSTRAINT [PK_workspace_member] PRIMARY KEY CLUSTERED ([workspace_id] ASC, [application_user_id] ASC);
GO

ALTER TABLE [dbo].[workspace_member]
    ADD CONSTRAINT [FK_workspace_member_application_user_application_user_id] FOREIGN KEY ([application_user_id]) REFERENCES [dbo].[application_user] ([id]);
GO

ALTER TABLE [dbo].[workspace_member]
    ADD CONSTRAINT [FK_workspace_member_workspace_workspace_id] FOREIGN KEY ([workspace_id]) REFERENCES [dbo].[workspace] ([id]);
GO

CREATE NONCLUSTERED INDEX [IX_workspace_member_application_user_id]
    ON [dbo].[workspace_member]([application_user_id] ASC);
GO

