CREATE TABLE [dbo].[database] (
    [id]                     INT            IDENTITY (1, 1) NOT NULL,
    [host_ip]                NVARCHAR (MAX) NOT NULL,
    [host_name]              NVARCHAR (MAX) NOT NULL,
    [name]                   NVARCHAR (MAX) NOT NULL,
    [location]               NVARCHAR (MAX) NOT NULL,
    [database_type]          NVARCHAR (MAX) NOT NULL,
    [default_login_user]     NVARCHAR (MAX) NOT NULL,
    [default_login_password] NVARCHAR (MAX) NOT NULL
);
GO

ALTER TABLE [dbo].[database]
    ADD CONSTRAINT [PK_database] PRIMARY KEY CLUSTERED ([id] ASC);
GO

