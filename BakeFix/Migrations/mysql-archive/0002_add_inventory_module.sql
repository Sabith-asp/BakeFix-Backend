CREATE TABLE IF NOT EXISTS ProductCategories (
    Id              CHAR(36)     NOT NULL PRIMARY KEY,
    OrganizationId  CHAR(36)     NOT NULL,
    Name            VARCHAR(100) NOT NULL,
    CreatedAt       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_categories_org (OrganizationId)
);

CREATE TABLE IF NOT EXISTS Products (
    Id              CHAR(36)      NOT NULL PRIMARY KEY,
    OrganizationId  CHAR(36)      NOT NULL,
    CategoryId      CHAR(36)      NULL,
    Name            VARCHAR(200)  NOT NULL,
    Description     VARCHAR(500)  NULL,
    SKU             VARCHAR(100)  NULL,
    Unit            VARCHAR(50)   NOT NULL DEFAULT 'pcs',
    CostPrice       DECIMAL(12,2) NOT NULL DEFAULT 0,
    SellingPrice    DECIMAL(12,2) NOT NULL DEFAULT 0,
    CurrentStock    DECIMAL(12,3) NOT NULL DEFAULT 0,
    LowStockAlert   DECIMAL(12,3) NULL,
    IsActive        TINYINT(1)    NOT NULL DEFAULT 1,
    CreatedAt       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FULLTEXT INDEX  idx_products_search   (Name, Description),
    INDEX           idx_products_org      (OrganizationId, IsActive),
    INDEX           idx_products_category (OrganizationId, CategoryId)
);

CREATE TABLE IF NOT EXISTS StockTransactions (
    Id                  CHAR(36)      NOT NULL PRIMARY KEY,
    OrganizationId      CHAR(36)      NOT NULL,
    ProductId           CHAR(36)      NOT NULL,
    Type                VARCHAR(10)   NOT NULL,
    Quantity            DECIMAL(12,3) NOT NULL,
    UnitPrice           DECIMAL(12,2) NOT NULL,
    TotalAmount         DECIMAL(12,2) NOT NULL,
    SupplierOrCustomer  VARCHAR(200)  NULL,
    PaymentMethod       VARCHAR(50)   NULL,
    DivisionId          CHAR(36)      NULL,
    Note                VARCHAR(500)  NULL,
    Date                DATETIME      NOT NULL,
    CreatedAt           DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_stock_org     (OrganizationId, Date),
    INDEX idx_stock_product (ProductId, Date)
);

INSERT IGNORE INTO Modules (Name) VALUES ('Inventory');

INSERT IGNORE INTO OrganizationModules (OrganizationId, ModuleId, IsEnabled)
SELECT o.Id, m.Id, FALSE
FROM Organizations o
JOIN Modules m ON m.Name = 'Inventory'
WHERE NOT EXISTS (
    SELECT 1 FROM OrganizationModules om
    WHERE om.OrganizationId = o.Id AND om.ModuleId = m.Id
);
