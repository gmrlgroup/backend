CREATE TABLE [dbo].[user_role] (
    [user_id] NVARCHAR (450) NOT NULL,
    [role_id] NVARCHAR (450) NOT NULL
);
GO

ALTER TABLE [dbo].[user_role]
    ADD CONSTRAINT [PK_user_role] PRIMARY KEY CLUSTERED ([user_id] ASC, [role_id] ASC);
GO

ALTER TABLE [dbo].[user_role]
    ADD CONSTRAINT [FK_user_role_role_role_id] FOREIGN KEY ([role_id]) REFERENCES [dbo].[role] ([id]);
GO

ALTER TABLE [dbo].[user_role]
    ADD CONSTRAINT [FK_user_role_application_user_user_id] FOREIGN KEY ([user_id]) REFERENCES [dbo].[application_user] ([id]);
GO

CREATE NONCLUSTERED INDEX [IX_user_role_role_id]
    ON [dbo].[user_role]([role_id] ASC);
GO

