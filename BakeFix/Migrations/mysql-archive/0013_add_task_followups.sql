ALTER TABLE Tasks ADD COLUMN NeedsFollowUp      TINYINT(1) NOT NULL DEFAULT 0 AFTER CompletedAt;
ALTER TABLE Tasks ADD COLUMN FollowUpDate       DATE       NULL AFTER NeedsFollowUp;
ALTER TABLE Tasks ADD COLUMN FollowUpNotes      TEXT       NULL AFTER FollowUpDate;
ALTER TABLE Tasks ADD COLUMN FollowUpResolvedAt DATETIME   NULL AFTER FollowUpNotes;

ALTER TABLE Tasks ADD INDEX idx_tasks_followup (OrganizationId, NeedsFollowUp, FollowUpResolvedAt, FollowUpDate);
