CREATE TABLE IF NOT EXISTS PrayerOrgSettings (
    Id                  CHAR(36)       NOT NULL PRIMARY KEY,
    OrganizationId      CHAR(36)       NOT NULL UNIQUE,
    Latitude            DECIMAL(10,7)  NOT NULL DEFAULT 21.3891000,
    Longitude           DECIMAL(10,7)  NOT NULL DEFAULT 39.8579000,
    Timezone            VARCHAR(100)   NOT NULL DEFAULT 'Asia/Riyadh',
    CalculationMethod   VARCHAR(50)    NOT NULL DEFAULT 'MWL',
    AsrMethod           VARCHAR(20)    NOT NULL DEFAULT 'Standard',
    FajrAngle           DECIMAL(5,2)   NOT NULL DEFAULT 18.00,
    IshaAngle           DECIMAL(5,2)   NOT NULL DEFAULT 17.00,
    CreatedAt           DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt           DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS PrayerUserSettings (
    Id                  CHAR(36)       NOT NULL PRIMARY KEY,
    OrganizationId      CHAR(36)       NOT NULL,
    UserId              CHAR(36)       NOT NULL,
    Latitude            DECIMAL(10,7)  NULL,
    Longitude           DECIMAL(10,7)  NULL,
    Timezone            VARCHAR(100)   NULL,
    CityName            VARCHAR(150)   NULL,
    CreatedAt           DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt           DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_prayer_user_settings (OrganizationId, UserId)
);

CREATE TABLE IF NOT EXISTS PrayerRecords (
    Id                   CHAR(36)      NOT NULL PRIMARY KEY,
    OrganizationId       CHAR(36)      NOT NULL,
    UserId               CHAR(36)      NOT NULL,
    Username             VARCHAR(100)  NOT NULL,
    PrayerName           VARCHAR(20)   NOT NULL,
    PrayerDate           DATE          NOT NULL,
    PrayerTime           TIME          NOT NULL,
    PrayerEndTime        TIME          NOT NULL,
    ActualCompletionTime DATETIME      NULL,
    Status               VARCHAR(30)   NOT NULL DEFAULT 'Upcoming',
    CongregationType     VARCHAR(50)   NULL,
    UpdatedByUserId      CHAR(36)      NULL,
    UpdatedByUsername    VARCHAR(100)  NULL,
    Notes                TEXT          NULL,
    LastUpdatedAt        DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CreatedAt            DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_prayer_record   (OrganizationId, UserId, PrayerName, PrayerDate),
    INDEX idx_prayer_user_date    (OrganizationId, UserId, PrayerDate),
    INDEX idx_prayer_org_date     (OrganizationId, PrayerDate, Status),
    INDEX idx_prayer_status_trans (OrganizationId, PrayerDate, Status, PrayerTime)
);

CREATE TABLE IF NOT EXISTS PrayerStatusHistory (
    Id                  CHAR(36)       NOT NULL PRIMARY KEY,
    PrayerRecordId      CHAR(36)       NOT NULL,
    OldStatus           VARCHAR(30)    NOT NULL,
    NewStatus           VARCHAR(30)    NOT NULL,
    ChangedByUserId     CHAR(36)       NULL,
    ChangedByUsername   VARCHAR(100)   NULL,
    Note                TEXT           NULL,
    ChangedAt           DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_prayer_history (PrayerRecordId),
    FOREIGN KEY (PrayerRecordId) REFERENCES PrayerRecords(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS PrayerReminderConfigs (
    Id                  CHAR(36)       NOT NULL PRIMARY KEY,
    OrganizationId      CHAR(36)       NOT NULL,
    UserId              CHAR(36)       NOT NULL,
    PrayerName          VARCHAR(20)    NOT NULL,
    ReminderType        VARCHAR(50)    NOT NULL,
    MinutesOffset       INT            NOT NULL DEFAULT 0,
    IsEnabled           TINYINT(1)     NOT NULL DEFAULT 1,
    CreatedAt           DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt           DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_reminder_config (UserId, PrayerName, ReminderType),
    INDEX idx_reminder_org        (OrganizationId, IsEnabled)
);

CREATE TABLE IF NOT EXISTS PrayerStreaks (
    Id                    CHAR(36)     NOT NULL PRIMARY KEY,
    OrganizationId        CHAR(36)     NOT NULL,
    UserId                CHAR(36)     NOT NULL,
    CurrentStreak         INT          NOT NULL DEFAULT 0,
    LongestStreak         INT          NOT NULL DEFAULT 0,
    LastStreakDate        DATE         NULL,
    TotalPrayersCompleted INT          NOT NULL DEFAULT 0,
    TotalPrayersOnTime    INT          NOT NULL DEFAULT 0,
    LastUpdatedAt         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_streak_user (OrganizationId, UserId),
    INDEX idx_streak_org  (OrganizationId, CurrentStreak DESC)
);

INSERT IGNORE INTO Modules (Name) VALUES ('Prayer');

INSERT IGNORE INTO OrganizationModules (OrganizationId, ModuleId, IsEnabled)
SELECT o.Id, m.Id, FALSE
FROM Organizations o
JOIN Modules m ON m.Name = 'Prayer'
WHERE NOT EXISTS (
    SELECT 1 FROM OrganizationModules om
    WHERE om.OrganizationId = o.Id AND om.ModuleId = m.Id
);
