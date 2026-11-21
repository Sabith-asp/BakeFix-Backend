CREATE TABLE IF NOT EXISTS Tasks (
    Id                  CHAR(36)      NOT NULL PRIMARY KEY,
    OrganizationId      CHAR(36)      NOT NULL,
    CreatedByUserId     CHAR(36)      NOT NULL,
    CreatedByUsername   VARCHAR(100)  NOT NULL,
    AssignedToUserId    CHAR(36)      NULL,
    AssignedToUsername  VARCHAR(100)  NULL,
    Title               VARCHAR(255)  NOT NULL,
    Description         TEXT          NULL,
    Category            VARCHAR(100)  NOT NULL,
    Priority            ENUM('High','Medium','Low')         NOT NULL DEFAULT 'Medium',
    Status              ENUM('Pending','InProgress','Completed') NOT NULL DEFAULT 'Pending',
    Visibility          ENUM('Personal','Organisation')     NOT NULL DEFAULT 'Personal',
    OriginalTargetDate  DATE          NOT NULL,
    CurrentTargetDate   DATE          NOT NULL,
    CompletedAt         DATETIME      NULL,
    DeletedAt           DATETIME      NULL,
    CreatedAt           DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt           DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_tasks_user     (OrganizationId, CreatedByUserId, CurrentTargetDate, DeletedAt),
    INDEX idx_tasks_org      (OrganizationId, Visibility, CurrentTargetDate, DeletedAt),
    INDEX idx_tasks_assigned (OrganizationId, AssignedToUserId, CurrentTargetDate),
    INDEX idx_tasks_status   (OrganizationId, Status, Visibility)
);

CREATE TABLE IF NOT EXISTS TaskActivityLog (
    Id                   CHAR(36)     NOT NULL PRIMARY KEY,
    TaskId               CHAR(36)     NOT NULL,
    PerformedByUserId    CHAR(36)     NOT NULL,
    PerformedByUsername  VARCHAR(100) NOT NULL,
    ActivityType         VARCHAR(50)  NOT NULL,
    OldValue             VARCHAR(500) NULL,
    NewValue             VARCHAR(500) NULL,
    Comment              TEXT         NULL,
    CreatedAt            DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_activity_task (TaskId),
    FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS DailyNotes (
    Id                CHAR(36)     NOT NULL PRIMARY KEY,
    OrganizationId    CHAR(36)     NOT NULL,
    CreatedByUserId   CHAR(36)     NOT NULL,
    CreatedByUsername VARCHAR(100) NOT NULL,
    NoteDate          DATE         NOT NULL,
    Content           TEXT         NOT NULL,
    Visibility        ENUM('Personal','Organisation') NOT NULL DEFAULT 'Personal',
    CreatedAt         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_note_per_user_date_vis (OrganizationId, CreatedByUserId, NoteDate, Visibility),
    INDEX idx_notes_org_date (OrganizationId, NoteDate, Visibility)
);

INSERT IGNORE INTO Modules (Name) VALUES ('Tasks');

INSERT IGNORE INTO OrganizationModules (OrganizationId, ModuleId, IsEnabled)
SELECT o.Id, m.Id, FALSE
FROM Organizations o
JOIN Modules m ON m.Name = 'Tasks'
WHERE NOT EXISTS (
    SELECT 1 FROM OrganizationModules om
    WHERE om.OrganizationId = o.Id AND om.ModuleId = m.Id
);
