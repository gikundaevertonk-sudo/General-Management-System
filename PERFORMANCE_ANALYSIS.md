# GMS Web Performance Analysis

## Executive Summary
The web application has **three critical performance bottlenecks** that cause excessive data loading and database queries on nearly every page view. These are in the Razor Page models, not the Core services. Fixing these would eliminate the majority of performance issues.

---

## Critical Issues

### 1. **Transactions/Index.cshtml.cs** - Loading 2,000+ Records per Page View
**Severity:** CRITICAL  
**Location:** `GMS.Web/Pages/Transactions/Index.cshtml.cs`, lines 45-48

**Problem:**
```vb
' Runs EVERY time the Transactions page loads
var cs = customers.Search(new QueryOptions { PageSize = 1000 });
if (cs.Succeeded) CustomerNames = cs.Value.Items.ToDictionary(c => c.Id, c => c.Name);
var ss = suppliers.Search(new QueryOptions { PageSize = 1000 });
if (ss.Succeeded) SupplierNames = ss.Value.Items.ToDictionary(s => s.Id, s => s.Name);
```

**Impact:**
- Loads **up to 2,000 records** (1000 customers + 1000 suppliers) on every page view
- Even if you only have 50 customers and 50 suppliers, it still queries for 2,000
- This is done to populate a dropdown/lookup for the transaction list
- **Every filter, search, or pagination click reloads all 2,000 records**

**Why It's Happening:**
The page needs to display customer/supplier names next to transactions, so it pre-loads all of them to avoid N+1 queries in the template. But it's loading way too many records.

**Solution Options:**
1. **Load-on-demand (Async):** Fetch names only for the 20 transactions shown on the current page
2. **Smart caching:** Cache the customer/supplier names in memory for 5-10 minutes
3. **Lazy loading:** Load names in the Razor view with small batch queries instead of up-front
4. **Pagination on lookups:** Load customers/suppliers 100 at a time instead of 1000

**Recommended Fix:** Load-on-demand for the displayed transactions only (~20 lookups instead of 2,000)

---

### 2. **Inventory/Index.cshtml.cs** - Loading All Products
**Severity:** CRITICAL  
**Location:** `GMS.Web/Pages/Inventory/Index.cshtml.cs`, line 39

**Problem:**
```vb
' Runs EVERY time the Inventory page loads
var ps = products.Search(new QueryOptions { PageSize = 1000, SortBy = "name" });
if (ps.Succeeded)
    ProductOptions = ps.Value.Items
        .Select(p => new SelectListItem($"{p.Sku} — {p.Name}", p.Id.ToString()))
        .ToList();
```

**Impact:**
- Loads **up to 1,000 product records** to populate a dropdown
- This is for selecting which product to view inventory for
- Dropdown searches typically have autocomplete, making PageSize=1000 wasteful

**Why It's Happening:**
The page needs a list of products for the dropdown/selector widget.

**Solution:**
1. **Implement dropdown search/autocomplete:** Load only 10-20 products matching search term
2. **Lazy load on focus:** Load products only when dropdown is opened
3. **Reasonable pagination:** Load 100 products, show "more results" if needed

**Recommended Fix:** Implement product search/autocomplete dropdown (typically 20 results, searchable)

---

### 3. **Products/Index.cshtml.cs** - Loading All Categories
**Severity:** MEDIUM  
**Location:** `GMS.Web/Pages/Products/Index.cshtml.cs`, lines 27-29

**Problem:**
```vb
var cats = categories.List();
if (cats.Succeeded)
    CategoryNames = cats.Value.ToDictionary(c => c.Id, c => c.Name);
```

**Impact:**
- Loads all categories on every Products page view
- Typically not as bad as the above two (categories are usually < 100)
- But still wasteful if categories rarely change

**Why It's Happening:**
The product list displays category names next to each product, so it pre-loads all categories.

**Solution:**
1. **Cache for 5-10 minutes:** Categories don't change frequently, perfect for caching
2. **Load-on-demand:** Fetch only category IDs used by products on current page
3. **Include in product query:** Some ORMs can eager-load related categories

**Recommended Fix:** Cache categories (expires every 10 minutes) or include in product search query

---

## Secondary Issues

### 4. **ReportService - Inventory Valuation Report**
**Severity:** MEDIUM  
**Location:** `GMS.Web/Pages/Reports/Index.cshtml.cs`, line 33

**Problem:**
- The `InventoryValuation()` report likely loads ALL products with ALL transaction history
- No pagination, no date filtering
- Could be 10,000+ rows for mature businesses

**Recommended Fix:** Add pagination or date-range filtering to report queries

---

### 5. **Missing Database Indexes**
**Severity:** MEDIUM  
**Location:** Database schema

**Likely Missing:**
- Index on `Transaction.CustomerId` and `Transaction.SupplierId` (for lookups)
- Index on `TransactionLine.ProductId` (for product-used checks)
- Index on `Category.Name` (for duplicate checking)
- Index on `Customer.Code` / `Supplier.Code` (for duplicate checking)
- Index on audit queries (type, action, entity)

**Recommended Fix:** Add indexes to frequently filtered/joined columns

---

## Performance Test Recommendations

1. **Database Query Logging:**
   - Enable SQL logging to see actual queries being executed
   - Profile how many queries run per page load

2. **Load Testing:**
   - Test with realistic data (10,000 products, 5,000 customers, 50,000 transactions)
   - Measure page load time before and after fixes

3. **Browser DevTools:**
   - Check network tab to see request sizes and timing
   - Monitor for long-running requests during search/filter

---

## Implementation Priority

| Priority | Issue | Est. Impact | Est. Effort |
|----------|-------|-------------|-------------|
| 1 | Transactions customers/suppliers loading | 40-50% improvement | Medium |
| 2 | Inventory products dropdown | 20-30% improvement | Medium |
| 3 | Database indexes | 15-25% improvement | Low |
| 4 | Category caching | 5-10% improvement | Low |
| 5 | Report pagination | 5-15% improvement | Medium |

---

## Root Cause Analysis

**Why these issues exist:**
1. The in-memory store (previously used) masked these inefficiencies - it was fast regardless of load size
2. Moving to Postgres database exposed the real cost of loading 2,000 records per page
3. Web pages were written for small demo data, not real-world scale

**Why they weren't caught earlier:**
- Demo data is small (~50 customers, ~50 products)
- Page feels responsive with small data
- Database persistence was recently added, so the real impact is just becoming apparent

---

## Next Steps

1. Start with Issue #1 (Transactions page) - highest impact, medium complexity
2. Follow with Issue #2 (Inventory dropdown) - similar fix approach
3. Add category caching - quick win
4. Add database indexes - foundation for further optimization
5. Monitor with real data to find any remaining bottlenecks
