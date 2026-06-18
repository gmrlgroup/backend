CREATE TABLE [dbo].[workspace] (
    [id]          NVARCHAR (10)  NOT NULL,
    [name]        NVARCHAR (100) NOT NULL,
    [created_on]  DATETIME2 (7)  NULL,
    [modified_on] DATETIME2 (7)  NULL,
    [created_by]  NVARCHAR (MAX) NULL,
    [modified_by] NVARCHAR (MAX) NULL,
    [is_deleted]  BIT            NULL
);
GO

ALTER TABLE [dbo].[workspace]
    ADD CONSTRAINT [PK_workspace] PRIMARY KEY CLUSTERED ([id] ASC);
GO

