CREATE TABLE [dbo].[role_claim] (
    [id]          INT            IDENTITY (1, 1) NOT NULL,
    [role_id]     NVARCHAR (450) NOT NULL,
    [claim_type]  NVARCHAR (MAX) NULL,
    [claim_value] NVARCHAR (MAX) NULL
);
GO

CREATE NONCLUSTERED INDEX [IX_role_claim_role_id]
    ON [dbo].[role_claim]([role_id] ASC);
GO

ALTER TABLE [dbo].[role_claim]
    ADD CONSTRAINT [FK_role_claim_role_role_id] FOREIGN KEY ([role_id]) REFERENCES [dbo].[role] ([id]);
GO

ALTER TABLE [dbo].[role_claim]
    ADD CONSTRAINT [PK_role_claim] PRIMARY KEY CLUSTERED ([id] ASC);
GO

