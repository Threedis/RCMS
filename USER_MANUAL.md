# User Manual - Enterprise Inventory Management System

## Getting Started

### First Time Login

1. Open your browser and navigate to `https://yourdomain.com`
2. Enter your username and password (provided by administrator)
3. Click **Login**

**First Login Setup**:
- You may be prompted to change your password
- Enter your current password, then new password twice
- Password must contain: uppercase, lowercase, number, special character
- Minimum 8 characters

### Dashboard Overview

The Dashboard displays key inventory metrics and operational data:

**KPI Cards** (Top Section):
- **Inventory Value**: Total value of all stock across warehouses
- **Today's Inward**: Goods received today (count and value)
- **Today's Outward**: Materials issued today (count and value)
- **Pending Approvals**: Documents waiting for your approval
- **Low Stock Items**: Items below reorder level
- **Dead Stock**: Non-moving inventory
- **Month Consumption**: Material usage trend vs last month
- **Month Purchase**: Procurement trend vs last month

**Charts** (Middle Section):
- **Inward vs Outward**: 12-month trend comparison
- **Stock by Category**: Value distribution
- **Stock by Warehouse**: Location-wise inventory
- **Top Vendors**: Supplier performance this year
- **Document Status**: Breakdown of pending/approved documents

**Data Panels** (Bottom Section):
- **Low Stock Alert**: Items requiring reorder with suggested order value
- **Pending Approvals**: Queue of documents waiting for action
- **Recent Activity**: Latest system events and changes

## Master Data Management

### Managing Categories

**Path**: Administration > Masters > Category

**Create Category**:
1. Click **New Category**
2. Enter category code (e.g., "ELECT" for Electrical)
3. Enter category name
4. Optionally select parent category for hierarchy
5. Enter description
6. Set display order (numeric, lower = earlier)
7. Check/uncheck Active status
8. Click **Save**

**Typical Categories**:
- Electrical
- Mechanical
- IT Hardware
- Instrumentation
- Consumables
- Safety Equipment
- Tools
- Civil Materials

### Managing Units

**Path**: Administration > Masters > Unit

**Pre-loaded Units**:
- Nos (Pieces)
- Mtr (Meter)
- Kg (Kilogram)
- Ltr (Liter)
- Set (Set/Assembly)
- Box (Box)
- Rol (Roll)
- Pr (Pair)

**Add Custom Unit**:
1. Click **New Unit**
2. Enter unit code (e.g., "DZN" for Dozen)
3. Enter unit name
4. Enter symbol (used in documents)
5. Specify decimal places for fractional quantities
6. Click **Save**

### Managing Vendors

**Path**: Inventory > Vendors

**Create Vendor**:
1. Click **New Vendor**
2. **General Tab**:
   - Code: Unique identifier (auto-generated or manual)
   - Name: Supplier name
   - Address: Full address
   - City, State, PIN Code
   - Contact Person, Phone, Email
3. **Statutory & Bank Tab**:
   - GSTIN: 15-character GST number (format: 15 digits/letters)
   - PAN: 10-character PAN number
   - Bank Name, Account Number, IFSC Code
4. **Commercial Tab**:
   - Credit Days: Payment terms
   - Rating: 1-5 star rating
   - Display Order: Sort order in lists
   - Active: Enable/disable
   - Blacklisted: Prevent selection on new GRN (checked = cannot use)
   - Notes: Additional information
4. Click **Save**

**Editing Vendors**:
- Click the **Edit** (pencil) icon
- Modify as needed
- Click **Save**

**Blacklisting Vendor**:
- Edit vendor > Check "Blacklisted" > Save
- This vendor will not appear in GRN vendor selection

## Item Master

### Managing Items

**Path**: Inventory > Items

**Create Item**:
1. Click **New Item**
2. Enter item code (or leave blank for auto-generation)
3. Enter item name
4. Enter part/serial number (optional)
5. Select category
6. Select unit of measure
7. Enter type (Raw Material, Component, Finished Goods, Consumable)
8. Enter reorder level (quantity to trigger low stock alert)
9. Enter unit cost
10. Click **Save**

**Bulk Import**:
1. Click **Import** button
2. Click **Download Template** to get Excel format
3. Fill in Excel with item data:
   - Code, Name, Category, Unit, Cost, ReorderLevel
4. Upload file
5. Review validation errors (if any)
6. Check "Validate Only" to do a dry run
7. Uncheck to commit to database
8. Click **Upload**

**Search Items**:
- Use search bar or filters
- Click item to view details
- Click **View Ledger** to see transaction history

## Goods Receipt Notes (GRN)

### Creating a GRN

**Path**: Transactions > Goods Receipt Notes > New GRN

**Step 1: Header Information**
1. GRN Number: Auto-generated (read-only)
2. GRN Date: Date of goods receipt (defaults to today)
3. Reference Number: Optional PO or invoice number
4. Receipt Date: When goods actually arrived
5. Select Vendor from dropdown
6. Select Warehouse (where goods will be stored)
7. Add Notes (optional)

**Step 2: Add Line Items**
1. Click **Add Line** button
2. Select Item (type item code/name for search)
3. Enter Quantity (how many units received)
4. Enter Rate (unit cost)
5. Amount auto-calculates (Qty × Rate)
6. Repeat for each line item
7. Line total shows at bottom

**Step 3: Submit**
1. Review all details
2. Click **Save** to save as Draft
3. Click **Submit** to send for approval

**GRN Status Flow**:
- Draft: Created but not submitted
- Pending Approval: Waiting for approver
- Approved: Approved, stock has been added
- Rejected: Sent back for correction

### Editing a GRN

**Only Possible in Draft or Rejected Status**:
1. Click **Edit** icon on GRN
2. Modify header or lines as needed
3. Click **Save**
4. Click **Submit** when ready

### Approving a GRN

**Only for Users with Approver Role**:

**Path**: Approvals > [Select GRN]

1. Navigate to **Approvals** > View **Pending Approvals**
2. Click on the GRN you need to approve
3. Review document details carefully:
   - Vendor credibility
   - Prices vs market rate
   - Quantities vs requirements
   - Documentation completeness
4. Decide:
   - **Approve**: Stock is added to warehouse
   - **Reject**: Return to creator with comments
5. If rejecting, add comments explaining why
6. Click **Submit Decision**

**Approval SLA**: Documents older than 3 days are highlighted in red

## Material Issues (Outward)

### Creating an Issue

**Path**: Transactions > Material Issues > New Issue

**Step 1: Issue Details**
1. Issue Number: Auto-generated
2. Issue Date: Date material is being requested
3. Reference Number: Optional reference
4. **Issued To**: Select person or department
   - Engineer: Individual project engineer
   - Department: Entire department
5. Select Warehouse: Source warehouse
6. Add Notes

**Step 2: Add Line Items**
1. Click **Add Line**
2. Select Item (search by code/name)
3. **Available Qty**: Shows current stock in warehouse
4. Enter Required Qty (how much needed)
5. Enter Rate (unit cost)
6. Amount auto-calculates
7. **Note**: System prevents overbilling (won't allow qty > available)

**Step 3: Workflow**
1. **Save** as Draft
2. **Submit** for approval
3. Approver reviews and approves
4. After approval, **Dispatch** to assign courier

### Dispatch Material

**After Issue is Approved**:
1. Issue shows **Dispatch** option
2. Click **Dispatch**
3. Select Courier (delivery partner)
4. Enter Tracking Number (courier's reference)
5. Click **Dispatch**
6. Issue status changes to **Issued**

**Status Flow**:
- Draft → Pending Approval → Approved → Issued → Closed

## Stock Inquiry

### Current Stock

**Path**: Inventory > Stock > Current Stock

**View Options**:
- Displays all items with current balances
- Shows by warehouse (can filter)
- Shows by category (can filter)

**Columns**:
- Item Code/Name
- Warehouse location
- Current Quantity
- Reorder Level
- Unit Cost
- Total Stock Value

**Color Coding**:
- Green badge: In stock
- Red badge: Low stock (below reorder level)
- Gray badge: Out of stock

**Export**: Click Excel/PDF to export data

### Stock Ledger

**Path**: Inventory > Stock > Ledger

**Shows Transaction History**:
- Date and transaction type (Inward/Outward)
- Reference number (GRN/Issue)
- Quantity in/out
- Running balance
- Unit rate and value

**Filters**:
- Date range
- Warehouse
- Category
- Item

**Use Case**: Track the lifecycle of a particular item

### Low Stock Items

**Path**: Inventory > Stock > Low Stock Items

**Shows**:
- Items currently below reorder level
- Current quantity vs reorder level
- Shortage amount
- Estimated order value
- Unit cost

**Action**: Use this to create purchase orders

## Reports

### Accessing Reports

**Path**: Reports

**Available Reports**:

1. **Receipt Register**: All inward goods with vendor details
2. **Issue Register**: All outward materials with recipient info
3. **Current Stock**: Real-time inventory balances
4. **Low Stock Report**: Items requiring reorder
5. **Dead Stock Report**: Slow/non-moving inventory
6. **Vendor Performance**: Supplier analysis and rankings
7. **ABC Analysis**: Inventory classification (Pareto)
8. **Consumption Analysis**: Department-wise usage trends
9. **Inventory Valuation**: Total stock value assessment
10. **Site-wise Stock**: Location distribution

### Running a Report

1. Select report from catalog
2. Configure filters (if available):
   - Date range
   - Vendor/Category/Department
   - Warehouse/Site
3. Click **Run Report**
4. Review results in table
5. **Export** to Excel or PDF if needed

**Report Features**:
- Sortable columns
- Scrollable for large datasets
- Summary rows with totals
- Proper formatting for printing

## Approval Queue

### Managing Your Approvals

**Path**: Approvals

**View**:
- All documents pending your approval
- Sorted by age (oldest first)
- Color-coded by urgency:
  - Red: Older than 3 days
  - Orange: 2-3 days
  - Gray: Fresh

**Approval Process**:
1. Click on document (GRN or Issue)
2. Review all details thoroughly
3. Check: quantities, vendor/recipient, prices, documentation
4. Decide to Approve or Reject
5. If Rejecting: Add detailed comments
6. Click **Submit Decision**

**Your Role**:
- Approvers ensure compliance and accuracy
- Catch errors before stock movement
- Protect company from fraud or overpayment
- Resolve discrepancies with creating user

## Notifications

### Notification Bell

**Location**: Top-right corner of screen

**Alerts Include**:
- Pending document awaiting approval
- Low stock item alerts
- System maintenance notices
- Access denied alerts (for tracking)

**Actions**:
- Click bell to expand dropdown
- Click notification to go to that document
- Mark as read (changes appearance)
- Mark all as read (bulk action)

## Dashboard Auto-Refresh

**Top-Right Button**: "Refresh" (circular arrow icon)

**Refreshes**:
- KPI values
- Chart data
- Approval count
- Recent activity

**Auto-Refresh**: Dashboard data updates periodically without manual refresh

## User Profile

### Changing Your Password

**Path**: Click your name (top-right) > Change Password

**Requirements**:
- Must contain uppercase and lowercase letters
- Must contain number and special character (!@#$%^&*)
- Minimum 8 characters
- Cannot reuse last 5 passwords
- Password expires every 90 days (if policy enabled)

### Viewing Your Profile

**Click your name (top-right)**:
- Full name
- Email address
- Roles assigned
- Last login date
- Current department

## Common Tasks

### Finding a Specific Item

1. Go to Inventory > Items
2. Use the search/filter option
3. Enter item code or name
4. Results update in real-time
5. Click item to view full details

### Tracking a GRN from Receipt to Approval

1. Go to Transactions > GRN
2. Find your GRN in the list
3. Click "View" to see full details
4. Bottom section shows "Audit Trail":
   - Creation date/user
   - Submission date/user
   - Approval date/user
   - Any rejection history

### Generating a Stock Report

1. Go to Reports > Current Stock
2. Set filters (optional):
   - Select Category
   - Select Warehouse
   - Set date range
3. Click "Run Report"
4. Click "Excel" to download
5. Open in Microsoft Excel
6. Pivot/sort as needed
7. Print or email

### Monitoring Low Stock

**Daily Task**:
1. Go to Dashboard (home page)
2. Check "Low Stock" panel
3. Note items requiring reorder
4. Create purchase order or GRN
5. Contact vendor for delivery

## Tips & Tricks

### Keyboard Shortcuts

- `Tab`: Move between form fields
- `Ctrl+S`: Submit form (some forms)
- `Esc`: Close modal dialog
- `Ctrl+P`: Print current page
- `Ctrl+Shift+Delete`: Clear browser cache

### Browser Compatibility

**Tested On**:
- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (iOS Safari, Chrome Mobile)

**Recommended**: Chrome or Edge (latest version)

### Performance Tips

1. **Clear Browser Cache**: If pages load slowly
2. **Use Filters**: Don't view all 10,000 items at once
3. **Export for Analysis**: Large datasets work better in Excel
4. **Refresh Dashboard**: Every 30 minutes for latest KPIs

### Error Messages & Solutions

| Error | Solution |
|-------|----------|
| "Session expired" | Click to login again |
| "Access denied" | Check your assigned role |
| "Duplicate item code" | Item code already exists, use different code |
| "Invalid GSTIN format" | Check format: 15 characters with proper structure |
| "Quantity exceeds available stock" | Reduce quantity requested |
| "Vendor is blacklisted" | Select different vendor |

## Getting Help

### Contact Your Administrator

- Report system issues
- Request new features
- Ask about permissions
- Get password reset

### Common Questions

**Q: How long does approval take?**
A: Typically 24 hours; check "Pending Approvals" section

**Q: Can I edit a submitted GRN?**
A: No, only in Draft state. Ask approver to reject if changes needed.

**Q: Where is my exported file?**
A: Downloads folder on your computer

**Q: Why am I locked out after 5 failed logins?**
A: Security feature. Wait 30 minutes or contact admin to unlock.

---

**Version**: 1.0  
**Last Updated**: August 4, 2024  
**For Technical Support**: Contact your administrator or development team
