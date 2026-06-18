CREATE TABLE [dbo].[sales_data] (
    [id]                       NVARCHAR (450)  NOT NULL,
    [scheme]                   NVARCHAR (100)  NOT NULL,
    [store_code]               NVARCHAR (50)   NOT NULL,
    [NetAmountAcy]             DECIMAL (18, 2) NOT NULL,
    [total_transactions]       INT             NOT NULL,
    [received_at]              DATETIME2 (7)   DEFAULT ('0001-01-01T00:00:00.0000000') NOT NULL,
    [source]                   NVARCHAR (MAX)  NULL,
    [is_processed]             BIT             NOT NULL,
    [company_id]               NVARCHAR (10)   NULL,
    [created_on]               DATETIME2 (7)   NULL,
    [modified_on]              DATETIME2 (7)   NULL,
    [created_by]               NVARCHAR (MAX)  NULL,
    [modified_by]              NVARCHAR (MAX)  NULL,
    [hour]                     INT             DEFAULT ((0)) NOT NULL,
    [category_name]            NVARCHAR (500)  NULL,
    [division_name]            NVARCHAR (500)  NULL,
    [total_store_transactions] DECIMAL (18, 2) NULL
);
GO

ALTER TABLE [dbo].[sales_data]
    ADD CONSTRAINT [FK_sales_data_company_company_id] FOREIGN KEY ([company_id]) REFERENCES [dbo].[company] ([id]);
GO

CREATE NONCLUSTERED INDEX [IX_sales_data_company_id]
    ON [dbo].[sales_data]([company_id] ASC);
GO

ALTER TABLE [dbo].[sales_data]
    ADD CONSTRAINT [PK_sales_data] PRIMARY KEY CLUSTERED ([id] ASC);
GO

