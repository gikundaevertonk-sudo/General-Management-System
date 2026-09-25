-- ============================================================================
-- Performance Optimization Indexes
--
-- These indexes improve query performance for the most common lookup patterns.
-- They complement the existing schema.sql indexes.
--
-- Run in the Supabase SQL Editor to apply these indexes.
-- ============================================================================

-- Customers: often searched by name or code
create index if not exists ix_customers_org_name
    on customers (organization_id, lower(name));

-- Suppliers: similar lookup patterns
create index if not exists ix_suppliers_org_name
    on suppliers (organization_id, lower(name));

-- Products: commonly filtered by active status and sorted by name
create index if not exists ix_products_org_name_active
    on products (organization_id, is_active, lower(name));

-- Transaction searches: often filtered by date range
create index if not exists ix_transactions_org_date
    on transactions (organization_id, transaction_date desc);

-- Stock movements: commonly queried for a product's history
create index if not exists ix_stock_movements_org_product
    on stock_movements (organization_id, product_id, moved_at_utc desc);

-- Audit: often filtered by entity type and sorted by timestamp
create index if not exists ix_audit_entries_org_entity_timestamp
    on audit_entries (organization_id, entity_name, timestamp_utc desc);
