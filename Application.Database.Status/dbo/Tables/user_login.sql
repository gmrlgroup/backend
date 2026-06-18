CREATE TABLE [dbo].[user_login] (
    [login_provider]        NVARCHAR (450) NOT NULL,
    [provider_key]          NVARCHAR (450) NOT NULL,
    [provider_display_name] NVARCHAR (MAX) NULL,
    [user_id]               NVARCHAR (450) NOT NULL
);
GO

ALTER TABLE [dbo].[user_login]
    ADD CONSTRAINT [FK_user_login_application_user_user_id] FOREIGN KEY ([user_id]) REFERENCES [dbo].[application_user] ([id]);
GO

ALTER TABLE [dbo].[user_login]
    ADD CONSTRAINT [PK_user_login] PRIMARY KEY CLUSTERED ([login_provider] ASC, [provider_key] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_user_login_user_id]
    ON [dbo].[user_login]([user_id] ASC);
GO

