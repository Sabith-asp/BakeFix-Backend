ALTER TABLE Organizations ADD COLUMN Timezone VARCHAR(100) NOT NULL DEFAULT 'Asia/Kolkata';

ALTER TABLE DailyNotes DROP INDEX uq_note_per_user_date_vis;

ALTER TABLE DailyNotes ADD COLUMN Title VARCHAR(255) NULL AFTER CreatedByUsername;
