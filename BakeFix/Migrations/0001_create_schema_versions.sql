-- Ensures the SchemaVersions tracking table exists.
-- The migrator bootstraps this table before running scripts,
-- so this script is a no-op in practice — it exists to make the
-- migration history self-documenting.
CREATE TABLE IF NOT EXISTS SchemaVersions (
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    ScriptName  VARCHAR(255) NOT NULL UNIQUE,
    AppliedAt   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    AppliedBy   VARCHAR(100) NOT NULL
);
