CREATE TABLE IF NOT EXISTS TaskSortOrder (
    Id         CHAR(36)             NOT NULL PRIMARY KEY,
    TaskId     CHAR(36)             NOT NULL,
    ScopeType  ENUM('User','Team')  NOT NULL,
    ScopeId    CHAR(36)             NOT NULL,
    SortOrder  INT                  NOT NULL DEFAULT 0,
    UpdatedAt  DATETIME             NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_task_sort_scope (ScopeType, ScopeId, TaskId),
    INDEX idx_sort_task (TaskId),
    FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE
);
