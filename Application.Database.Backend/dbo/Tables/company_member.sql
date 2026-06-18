CREATE TABLE [dbo].[company_member] (
    [company_id]          NVARCHAR (10)  NOT NULL,
    [application_user_id] NVARCHAR (450) NOT NULL
);
GO

ALTER TABLE [dbo].[company_member]
    ADD CONSTRAINT [FK_company_member_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO

ALTER TABLE [dbo].[company_member]
    ADD CONSTRAINT [FK_company_member_application_user_application_user_id] FOREIGN KEY ([application_user_id]) REFERENCES [dbo].[application_user] ([id]);
GO

CREATE NONCLUSTERED INDEX [IX_company_member_application_user_id]
    ON [dbo].[company_member]([application_user_id] ASC);
GO

ALTER TABLE [dbo].[company_member]
    ADD CONSTRAINT [PK_company_member] PRIMARY KEY CLUSTERED ([company_id] ASC, [application_user_id] ASC);
GO

