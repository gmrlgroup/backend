CREATE TABLE [dbo].[workspace_domain] (
    [workspace_id] NVARCHAR (10)  NOT NULL,
    [domain]       NVARCHAR (450) NOT NULL
);
GO

ALTER TABLE [dbo].[workspace_domain]
    ADD CONSTRAINT [PK_workspace_domain] PRIMARY KEY CLUSTERED ([workspace_id] ASC, [domain] ASC);
GO

ALTER TABLE [dbo].[workspace_domain]
    ADD CONSTRAINT [FK_workspace_domain_workspace_workspace_id] FOREIGN KEY ([workspace_id]) REFERENCES [dbo].[workspace] ([id]);
GO

