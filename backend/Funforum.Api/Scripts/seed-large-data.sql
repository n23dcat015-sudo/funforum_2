-- GĐ3a: tạo dữ liệu thử phân trang/index trên PostgreSQL.
-- Chạy sau khi đã áp dụng migrations. Mặc định tạo 20.000 thread visible.
DO $$
DECLARE
    board_id integer;
BEGIN
    SELECT id INTO board_id FROM boards WHERE slug = 'general';

    IF board_id IS NULL THEN
        INSERT INTO boards (slug, name, description, sort_order, created_at)
        VALUES ('general', 'General', 'General discussion', 1, now())
        RETURNING id INTO board_id;
    END IF;

    INSERT INTO threads (
        board_id, title, content, status, score, hot_rank, reply_count,
        image_count, images_posted, last_reply_at, created_at, updated_at
    )
    SELECT
        board_id,
        'Sample thread ' || n,
        'Generated sample content for pagination and index testing.',
        'visible',
        (random() * 200 - 100)::integer,
        random() * 100,
        (random() * 50)::integer,
        0,
        0,
        now() - (n || ' minutes')::interval,
        now() - (n || ' minutes')::interval,
        now() - (n || ' minutes')::interval
    FROM generate_series(1, 20000) AS series(n);
END $$;

ANALYZE boards;
ANALYZE threads;
