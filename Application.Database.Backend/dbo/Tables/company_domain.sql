CREATE TABLE [dbo].[company_domain] (
    [company_id] NVARCHAR (10)  NOT NULL,
    [domain]     NVARCHAR (450) NOT NULL
);
GO

ALTER TABLE [dbo].[company_domain]
    ADD CONSTRAINT [FK_company_domain_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO

ALTER TABLE [dbo].[company_domain]
    ADD CONSTRAINT [PK_company_domain] PRIMARY KEY CLUSTERED ([company_id] ASC, [domain] ASC);
GO

