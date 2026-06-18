CREATE TABLE [dbo].[status_page] (
    [id]                       NVARCHAR (450)  NOT NULL,
    [name]                     NVARCHAR (200)  NOT NULL,
    [description]              NVARCHAR (1000) NULL,
    [slug]                     NVARCHAR (100)  NOT NULL,
    [is_public]                BIT             NOT NULL,
    [is_default]               BIT             NOT NULL,
    [is_active]                BIT             NOT NULL,
    [theme_color]              NVARCHAR (7)    NULL,
    [logo_url]                 NVARCHAR (500)  NULL,
    [header_message]           NVARCHAR (2000) NULL,
    [footer_message]           NVARCHAR (2000) NULL,
    [refresh_interval_seconds] INT             NOT NULL,
    [show_uptime]              BIT             NOT NULL,
    [show_response_time]       BIT             NOT NULL,
    [show_dependencies]        BIT             NOT NULL,
    [display_config]           NVARCHAR (4000) NULL,
    [company_id]               NVARCHAR (10)   NULL,
    [created_on]               DATETIME2 (7)   NULL,
    [modified_on]              DATETIME2 (7)   NULL,
    [created_by]               NVARCHAR (MAX)  NULL,
    [modified_by]              NVARCHAR (MAX)  NULL,
    [is_deleted]               BIT             NOT NULL,
    [deleted_by]               NVARCHAR (MAX)  NULL,
    [deleted_at]               DATETIME2 (7)   NULL
);
GO

CREATE NONCLUSTERED INDEX [IX_status_page_company_id]
    ON [dbo].[status_page]([company_id] ASC);
GO

ALTER TABLE [dbo].[status_page]
    ADD CONSTRAINT [PK_status_page] PRIMARY KEY CLUSTERED ([id] ASC);
GO

