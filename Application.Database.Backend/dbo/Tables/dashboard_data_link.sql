CREATE TABLE [dbo].[dashboard_data_link] (
    [id]         NVARCHAR (450) NOT NULL,
    [company_id] NVARCHAR (10)  NOT NULL,
    [page_url]   NVARCHAR (200) NOT NULL,
    [dataset_id] NVARCHAR (450) NOT NULL,
    [table_name] NVARCHAR (150) NOT NULL,
    [created_at] DATETIME2 (7)  NOT NULL,
    [created_by] NVARCHAR (450) NULL
);
GO

ALTER TABLE [dbo].[dashboard_data_link]
    ADD CONSTRAINT [PK_dashboard_data_link] PRIMARY KEY CLUSTERED ([id] ASC);
GO

-- One link per dashboard page per company; connecting a new table upserts this row.
CREATE UNIQUE NONCLUSTERED INDEX [UX_dashboard_data_link_company_page]
    ON [dbo].[dashboard_data_link]([company_id] ASC, [page_url] ASC);
GO
