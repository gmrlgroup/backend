CREATE TABLE [dbo].[company] (
    [id]          NVARCHAR (10)  NOT NULL,
    [name]        NVARCHAR (100) DEFAULT (N'') NOT NULL,
    [created_on]  DATETIME2 (7)  NULL,
    [modified_on] DATETIME2 (7)  NULL,
    [created_by]  NVARCHAR (MAX) NULL,
    [modified_by] NVARCHAR (MAX) NULL,
    [is_deleted]  BIT            NULL
);
GO

ALTER TABLE [dbo].[company]
    ADD CONSTRAINT [PK_company] PRIMARY KEY CLUSTERED ([id] ASC);
GO

