-- Initialize PostgreSQL test database
\c nocturne_test;

-- Create test schemas
CREATE SCHEMA IF NOT EXISTS migration_test;
CREATE SCHEMA IF NOT EXISTS performance_test;

-- Create test users
CREATE USER testuser_readonly WITH PASSWORD 'readonly123';
GRANT CONNECT ON DATABASE nocturne_test TO testuser_readonly;
GRANT USAGE ON SCHEMA public, migration_test TO testuser_readonly;

-- Performance testing user
CREATE USER perfuser WITH PASSWORD 'perfpass123';
GRANT ALL PRIVILEGES ON DATABASE nocturne_test TO perfuser;
GRANT ALL PRIVILEGES ON SCHEMA public, migration_test, performance_test TO perfuser;

-- Create extensions for testing
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "btree_gin";
CREATE EXTENSION IF NOT EXISTS "btree_gist";

-- Create test tables (simplified versions for basic testing)
CREATE TABLE IF NOT EXISTS test_entries (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    original_id VARCHAR(255) NOT NULL,
    sgv INTEGER,
    date BIGINT NOT NULL,
    date_string VARCHAR(255),
    type VARCHAR(50),
    device VARCHAR(255),
    direction VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS test_treatments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    original_id VARCHAR(255) NOT NULL,
    event_type VARCHAR(100),
    date BIGINT NOT NULL,
    insulin DECIMAL(5,2),
    carbs INTEGER,
    entered_by VARCHAR(255),
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_test_entries_date ON test_entries(date);
CREATE INDEX IF NOT EXISTS idx_test_entries_type ON test_entries(type);
CREATE INDEX IF NOT EXISTS idx_test_entries_device ON test_entries(device);
CREATE INDEX IF NOT EXISTS idx_test_treatments_date ON test_treatments(date);
CREATE INDEX IF NOT EXISTS idx_test_treatments_type ON test_treatments(event_type);

-- Insert sample test data
INSERT INTO test_entries (original_id, sgv, date, date_string, type, device, direction) VALUES
('507f1f77bcf86cd799439011', 120, EXTRACT(EPOCH FROM NOW()) * 1000, NOW()::TEXT, 'sgv', 'test-device', 'Flat'),
('507f1f77bcf86cd799439012', 150, (EXTRACT(EPOCH FROM NOW()) - 300) * 1000, (NOW() - INTERVAL '5 minutes')::TEXT, 'sgv', 'test-device', 'SingleUp');

INSERT INTO test_treatments (original_id, event_type, date, insulin, entered_by) VALUES
('507f1f77bcf86cd799439013', 'Meal Bolus', EXTRACT(EPOCH FROM NOW()) * 1000, 3.5, 'test-user'),
('507f1f77bcf86cd799439014', 'Carb Correction', (EXTRACT(EPOCH FROM NOW()) - 600) * 1000, NULL, 'test-user');

-- Grant permissions on test tables
GRANT SELECT ON test_entries, test_treatments TO testuser_readonly;
GRANT ALL PRIVILEGES ON test_entries, test_treatments TO perfuser;

-- Create performance monitoring functions
CREATE OR REPLACE FUNCTION get_table_stats(table_name TEXT)
RETURNS TABLE(
    table_size TEXT,
    row_count BIGINT,
    index_count BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        pg_size_pretty(pg_total_relation_size(table_name::regclass)) as table_size,
        (SELECT n_tup_ins - n_tup_del FROM pg_stat_user_tables WHERE relname = table_name) as row_count,
        (SELECT COUNT(*) FROM pg_indexes WHERE tablename = table_name) as index_count;
END;
$$ LANGUAGE plpgsql;

-- Create function to simulate load for testing
CREATE OR REPLACE FUNCTION simulate_load(duration_seconds INTEGER DEFAULT 10)
RETURNS TEXT AS $$
DECLARE
    start_time TIMESTAMP := NOW();
    end_time TIMESTAMP := NOW() + INTERVAL '1 second' * duration_seconds;
    counter INTEGER := 0;
BEGIN
    WHILE NOW() < end_time LOOP
        PERFORM COUNT(*) FROM test_entries WHERE sgv > RANDOM() * 400;
        counter := counter + 1;
    END LOOP;
    
    RETURN 'Executed ' || counter || ' queries in ' || duration_seconds || ' seconds';
END;
$$ LANGUAGE plpgsql;

SELECT 'PostgreSQL test database initialized successfully' as status;