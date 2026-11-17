-- GĐ3c: kiểm tra planner có dùng index board feed sau khi chạy seed-large-data.sql.
EXPLAIN (ANALYZE, BUFFERS)
SELECT id, board_id, title, score, hot_rank, reply_count, image_count, last_reply_at
FROM threads
WHERE board_id = (SELECT id FROM boards WHERE slug = 'general')
  AND status = 'visible'
ORDER BY last_reply_at DESC, id DESC
LIMIT 10 OFFSET 0;

EXPLAIN (ANALYZE, BUFFERS)
SELECT id, board_id, title, score, hot_rank, reply_count, image_count, last_reply_at
FROM threads
WHERE board_id = (SELECT id FROM boards WHERE slug = 'general')
  AND status = 'visible'
  AND title ILIKE '%sample%'
ORDER BY last_reply_at DESC, id DESC
LIMIT 10 OFFSET 0;
