For a hospital ERP, inventory should be more than a simple “products + quantity” module. It should track what you have, where it is, which batch it belongs to, when it expires, how it was acquired, and where it went.

I’d structure it like this:

1. Inventory dashboard

The main screen should immediately show:

Total stock value
Items below reorder level
Items out of stock
Items expiring soon
Expired items
Pending purchase orders
Pending stock requests
Recent stock movements
Stock discrepancies
Department/warehouse balances

For example:

┌─────────────────────────────────────────────────────────┐
│ INVENTORY                                                │
├──────────────┬──────────────┬──────────────┬─────────────┤
│ Stock Value  │ Low Stock    │ Expiring     │ Out of Stock│
│ ₦48.2M       │ 37 items     │ 14 items     │ 6 items     │
├──────────────┴──────────────┴──────────────┴─────────────┤
│                                                         │
│  ⚠ 14 items expire within 90 days                       │
│  ⚠ 37 items below reorder level                         │
│  ✓ 8 purchase orders received today                     │
│                                                         │
│ Recent Stock Movements                                   │
│ Item       From       To          Qty     Date           │
│ Gloves     Main Store Pharmacy    200     Aug 10         │
│ Ceftriaxone Pharmacy  Ward 2       40     Aug 10         │
└─────────────────────────────────────────────────────────┘
2. Item master

Every inventory item should have a central record.

Item
 ├── Basic information
 ├── Category
 ├── Unit of measure
 ├── Manufacturer
 ├── Supplier
 ├── Storage requirements
 ├── Reorder settings
 ├── Batch tracking
 ├── Expiry tracking
 ├── Pricing
 ├── Tax
 └── Inventory locations

Example:

Ceftriaxone 1g Injection

SKU: MED-CEF-001
Category: Antibiotics
Unit: Vial
Generic Name: Ceftriaxone
Manufacturer: XYZ Pharma

Minimum Stock: 100
Reorder Level: 200
Maximum Stock: 1,000

Batch Tracking: YES
Expiry Tracking: YES
Prescription Controlled: YES
3. Categories

Don't hard-code categories. Make them configurable.

For example:

Pharmaceuticals
 ├── Antibiotics
 ├── Analgesics
 ├── Antimalarials
 ├── IV Fluids
 └── Controlled Drugs

Medical Consumables
 ├── Gloves
 ├── Syringes
 ├── Catheters
 ├── Dressings
 └── Sutures

Laboratory
 ├── Reagents
 ├── Test Kits
 └── Consumables

Surgical
 ├── Instruments
 ├── Implants
 └── Theatre Consumables

General
 ├── Cleaning Supplies
 ├── Stationery
 └── PPE
4. Multiple stores/locations

This is very important for a hospital.

Don't have one global quantity field.

Instead:

Hospital
│
├── Main Store
│   ├── Pharmaceuticals
│   ├── Consumables
│   └── General Supplies
│
├── Pharmacy
│
├── Laboratory Store
│
├── Theatre Store
│
├── Emergency Store
│
└── Ward 2 Store

An item can therefore have:

Ceftriaxone

Main Store:       500
Pharmacy:         180
Emergency:         40
Ward 2:             25
5. Batch and expiry management

For hospitals, this should be first-class functionality.

Don't simply store:

Ceftriaxone = 745

Store:

Ceftriaxone
│
├── Batch A
│   ├── Quantity: 500
│   ├── Expiry: 2027-01-15
│   └── Location: Main Store
│
├── Batch B
│   ├── Quantity: 180
│   ├── Expiry: 2027-06-20
│   └── Location: Pharmacy
│
└── Batch C
    ├── Quantity: 65
    ├── Expiry: 2028-02-10
    └── Location: Main Store

This allows the system to implement FEFO — First Expiry, First Out.

6. Stock movements

Every quantity change should create an immutable movement record.

Types could include:

Opening balance
Purchase receipt
Transfer
Issue
Return
Adjustment
Damaged
Expired
Lost
Stock count
Patient consumption
Supplier return

For example:

Stock Movement

Item: Ceftriaxone 1g
Batch: CEF-2027-001

Type: Transfer
From: Main Store
To: Pharmacy

Quantity: 100

Requested By: Store Officer
Approved By: Store Manager
Date: Aug 10, 2026

The key principle:

Never silently update inventory quantity. Record the transaction that caused the change.

7. Purchase → receiving → inventory

Inventory should connect directly to procurement.

The workflow can be:

Department Request
       ↓
Purchase Requisition
       ↓
Approval
       ↓
Purchase Order
       ↓
Supplier
       ↓
Goods Received
       ↓
Batch / Expiry captured
       ↓
Quality Check
       ↓
Stock Added
       ↓
Invoice
       ↓
Accounts Payable

This gives you a proper audit trail from request → purchase → receipt → stock.

8. Internal stock requests

Departments shouldn't simply take stock from the store.

For example:

Emergency Department
        │
        │ Request:
        │ 100 gloves
        │ 20 syringes
        │ 10 ceftriaxone
        ↓
     Main Store
        │
        │ Approve
        ↓
     Issue Stock
        │
        ↓
 Emergency Store

The system should record:

Requested → Approved → Picked → Issued → Received

9. Stock transfers

For multiple stores:

Main Store
    │
    │ Transfer 50
    ↓
Pharmacy

Ideally the transfer has states:

DRAFT
  ↓
REQUESTED
  ↓
APPROVED
  ↓
DISPATCHED
  ↓
RECEIVED
  ↓
COMPLETED

This prevents stock from disappearing during transfers.

10. Stock counts

You should have a proper stock-count workflow.

Stock Count
    ↓
Select Location
    ↓
Freeze / Snapshot expected quantities
    ↓
Staff counts physical stock
    ↓
Enter actual quantity
    ↓
System calculates variance
    ↓
Supervisor reviews
    ↓
Adjustment approved

Example:

                 System     Physical    Variance

Gloves            1,000        970        -30
Syringes            500        500          0
Ceftriaxone         200        195         -5

The adjustment should require an explanation, particularly for sensitive/high-value items.

11. Pharmacy integration

This is where hospital ERP inventory becomes much more interesting.

When a pharmacist dispenses:

Prescription
     ↓
Pharmacy
     ↓
Dispense
     ↓
Select appropriate batch
     ↓
FEFO
     ↓
Inventory decreases
     ↓
Patient billing

So you don't want pharmacy maintaining a completely separate stock database.

12. Patient consumption

Depending on how detailed your ERP is, you can track:

Ward stock
   ↓
Nurse administers medication
   ↓
Medication Administration Record
   ↓
Inventory consumption
   ↓
Patient charge

This allows you to answer:

How much ceftriaxone was actually consumed by Ward 3 this month?

rather than merely:

How much did Ward 3 receive?

13. Reorder management

Each item can have:

Minimum Stock
Reorder Point
Maximum Stock
Reorder Quantity
Lead Time
Preferred Supplier

Example:

Gloves

Current:       800
Reorder Point: 1,000
Maximum:       5,000

→ Generate replenishment suggestion

I'd initially make this a suggestion, rather than automatically creating a purchase order.

14. Supplier management

Inventory should connect items to suppliers:

Supplier
 ├── Products
 ├── Purchase Orders
 ├── Prices
 ├── Delivery history
 ├── Lead times
 ├── Returns
 └── Performance

This eventually lets the hospital compare supplier pricing and delivery performance.

The database structure I'd use

At a high level:

Item
 ├── ItemCategory
 ├── UnitOfMeasure
 ├── Manufacturer
 └── Supplier

InventoryLocation
 └── InventoryBalance
       ├── Item
       ├── Batch
       └── Quantity

Batch
 ├── Item
 ├── BatchNumber
 ├── ExpiryDate
 └── Manufacturer

StockMovement
 ├── Item
 ├── Batch
 ├── SourceLocation
 ├── DestinationLocation
 ├── Quantity
 ├── MovementType
 ├── User
 └── Timestamp

PurchaseOrder
 └── PurchaseOrderLine
       └── Item

GoodsReceipt
 └── GoodsReceiptLine
       ├── Item
       ├── Batch
       └── Quantity

StockRequest
 └── StockRequestLine
       └── Item

StockTransfer
 └── StockTransferLine
       └── Item

StockCount
 └── StockCountLine
       ├── ExpectedQuantity
       ├── ActualQuantity
       └── Variance
One architectural decision I'd strongly recommend

Make StockMovement the financial/audit backbone of inventory.

Instead of:

UPDATE inventory
SET quantity = quantity - 20

conceptually think:

StockMovement
-----------------------------
item       = Ceftriaxone
batch      = CEF-001
location   = Pharmacy
type       = ISSUE
quantity   = -20
reference  = DISP-000421
created_by = pharmacist
created_at = ...

Then your balance can be derived/reconciled from movements.

That gives you a strong audit trail and makes it much easier to answer questions like “who moved this stock, from where, why, and when?”

If you're building the ERP from scratch, I'd make the inventory module connect directly to Procurement, Pharmacy, Laboratory, Theatre, Wards, Billing, and Finance rather than treating inventory as an isolated module.

yes building from scratch

Then I’d design it as a hospital-wide inventory engine, not just a store-management screen.

If you're building from scratch, I’d use this architecture as the foundation:

                         ┌──────────────────┐
                         │   ITEM MASTER    │
                         │ Drugs / Supplies │
                         │ Reagents / Assets│
                         └────────┬─────────┘
                                  │
             ┌────────────────────┼────────────────────┐
             │                    │                    │
             ▼                    ▼                    ▼
      ┌─────────────┐      ┌─────────────┐      ┌─────────────┐
      │ PROCUREMENT │      │  INVENTORY  │      │   PHARMACY  │
      └──────┬──────┘      └──────┬──────┘      └──────┬──────┘
             │                    │                    │
             ▼                    ▼                    ▼
      Purchase Orders       Stock Movements       Dispensing
      Goods Receiving       Batches               Medication
      Suppliers              Locations             Consumption
                             Transfers
                             Counts
                             Adjustments
             │                    │                    │
             └────────────────────┼────────────────────┘
                                  ▼
                         ┌─────────────────┐
                         │     FINANCE     │
                         │ Billing / AP /  │
                         │ Cost Accounting │
                         └─────────────────┘
1. Start with the right domain model

I'd divide inventory into these domains:

A. Item Master

The definition of what the hospital can stock.

Item
├── Identity
├── Category
├── Subcategory
├── SKU
├── Generic name
├── Brand name
├── Manufacturer
├── Unit of measure
├── Pack size
├── Conversion rules
├── Tax
├── Reorder settings
├── Storage requirements
├── Batch tracking?
├── Expiry tracking?
├── Serial tracking?
├── Controlled item?
└── Active/inactive

Don't make drugs a completely different inventory system from gloves, reagents, or surgical supplies.

They should all be inventory items, with configurable attributes.

2. Locations are first-class objects

This is critical.

Don't create:

pharmacy_stock
ward_stock
lab_stock
theatre_stock

as separate tables.

Create a generic:

InventoryLocation

Then:

Hospital
│
├── Main Store
│   ├── Pharmaceutical Store
│   ├── Consumables Store
│   └── General Store
│
├── Pharmacy
│
├── Laboratory
│
├── Theatre
│
├── Emergency
│
├── Ward 1
├── Ward 2
└── Ward 3

Every location can have inventory.

That makes expansion to multiple branches much easier.

3. Separate stock from stock movements

I'd have:

InventoryBalance

for the current state:

item_id
location_id
batch_id
quantity
reserved_quantity
available_quantity

And:

StockMovement

for the history:

id
item_id
batch_id
source_location_id
destination_location_id
quantity
movement_type
reference_type
reference_id
unit_cost
performed_by
approved_by
created_at

For example:

StockMovement
──────────────────────────────────
CEFTRIAXONE 1G
Batch: CEF-2027-001

Source:      Main Store
Destination: Pharmacy
Quantity:    100
Type:        TRANSFER

Reference:   TRF-000421
Performed by: John
Approved by:  Mary
Date:         10 Aug 2026

This becomes your inventory ledger.

4. Never allow arbitrary quantity changes

This is one of the most important rules I'd put into the system.

Avoid:

inventory.quantity = 500

Instead, every change should have a business reason:

PURCHASE_RECEIPT
TRANSFER
ISSUE
DISPENSE
RETURN
ADJUSTMENT
DAMAGE
EXPIRY
LOSS
STOCK_COUNT
PATIENT_CONSUMPTION
SUPPLIER_RETURN

So if 100 units disappear, you can answer:

Who removed them?

When?

From which location?

Which batch?

Why?

Which document caused it?

That becomes extremely valuable for hospital audit and fraud prevention.

5. Build the workflows before the UI

I'd define these workflows first.

Procurement
Stock Need
   ↓
Purchase Requisition
   ↓
Approval
   ↓
Purchase Order
   ↓
Supplier
   ↓
Goods Receipt
   ↓
Batch / Expiry Capture
   ↓
Quality Check
   ↓
Inventory
Internal request
Ward
 ↓
Stock Request
 ↓
Approval
 ↓
Picking
 ↓
Issue
 ↓
Ward Receives
Transfer
Main Store
 ↓
Transfer Request
 ↓
Approval
 ↓
Dispatch
 ↓
Receive
 ↓
Complete
Stock count
Create Count
 ↓
Snapshot Expected Stock
 ↓
Physical Count
 ↓
Variance
 ↓
Supervisor Review
 ↓
Adjustment
Pharmacy
Prescription
 ↓
Pharmacy
 ↓
Dispensing
 ↓
Batch Selection / FEFO
 ↓
Stock Deduction
 ↓
Patient Charge
6. Batch management should be built in from day one

For hospital inventory, I'd make this configurable per item:

batch_tracking = true
expiry_tracking = true
serial_tracking = false

For example:

Ceftriaxone
├── Batch CEF001
│   ├── Expiry: Jan 2027
│   └── Qty: 300
│
├── Batch CEF002
│   ├── Expiry: Jun 2027
│   └── Qty: 500
│
└── Batch CEF003
    ├── Expiry: Dec 2027
    └── Qty: 200

The system should preferentially issue the batch that expires first.

That's FEFO — First Expiry, First Out.

7. Don't forget unit conversions

This causes problems in real inventory systems.

For example:

1 carton
   = 20 boxes

1 box
   = 100 gloves

So:

1 carton = 2,000 gloves

But procurement may purchase:

10 cartons

while the ward requests:

500 gloves

Your inventory engine needs to understand the conversion.

I'd therefore have:

Item
 └── UnitOfMeasure
      ├── Base Unit
      ├── Purchase Unit
      ├── Issue Unit
      └── Conversion Factor
8. Add reservations

This is another feature I'd build early.

Suppose the hospital has:

Ceftriaxone = 500

But 100 has already been allocated to approved requests.

You shouldn't show:

Available = 500

You should show:

On Hand:     500
Reserved:    100
Available:   400

This prevents two departments from consuming the same stock.

9. Design permissions around actions

Don't just have:

Inventory Manager
Inventory Staff

Instead:

inventory.item.view
inventory.item.create
inventory.item.edit

inventory.stock.view
inventory.stock.adjust

inventory.request.create
inventory.request.approve

inventory.transfer.create
inventory.transfer.approve
inventory.transfer.dispatch
inventory.transfer.receive

inventory.count.create
inventory.count.perform
inventory.count.approve

inventory.purchase.receive
inventory.return.create
inventory.return.approve

Then roles become collections of permissions.

For example:

Store Officer
 ├── View stock
 ├── Create requests
 ├── Issue stock
 ├── Receive stock
 └── Perform stock counts

Store Manager
 ├── All Store Officer permissions
 ├── Approve adjustments
 ├── Approve transfers
 └── Approve stock counts
10. Audit everything important

I'd create a generic audit system across the ERP:

AuditLog
────────────────────────────
User
Action
Entity
Entity ID
Old Value
New Value
IP Address
Timestamp
Reason

Especially for:

Stock adjustments
Deleted/voided transactions
Price changes
Batch changes
Expiry changes
Purchase orders
Approvals
Dispensing
Returns
Financial transactions

And ideally, don't actually delete transactional records.

Use:

VOIDED
CANCELLED
REVERSED

instead.

11. Your inventory module should eventually expose APIs

I'd structure the backend around domain services rather than letting every module directly manipulate inventory.

For example:

InventoryService
├── receiveStock()
├── issueStock()
├── transferStock()
├── reserveStock()
├── releaseReservation()
├── adjustStock()
├── consumeStock()
├── returnStock()
├── countStock()
└── getAvailableStock()

Then Pharmacy calls:

inventory.consumeStock(...)

Procurement calls:

inventory.receiveStock(...)

Ward calls:

inventory.requestStock(...)

This prevents each module from inventing its own inventory logic.

12. The key entities

If I were starting your database today, my initial inventory schema would look roughly like:

items
item_categories
item_units
item_unit_conversions
manufacturers
suppliers

inventory_locations
inventory_location_types

inventory_batches
inventory_serial_numbers

inventory_balances
inventory_reservations
inventory_movements

stock_requests
stock_request_lines

stock_transfers
stock_transfer_lines

stock_counts
stock_count_lines
stock_adjustments

purchase_requisitions
purchase_requisition_lines

purchase_orders
purchase_order_lines

goods_receipts
goods_receipt_lines

supplier_returns
supplier_return_lines

inventory_audit_logs

Then later:

reorder_rules
stock_alerts
inventory_valuations
cycle_counts
barcode_labels
warehouse_bins
controlled_drug_register
13. One thing I'd change from a typical ERP

I'd make Inventory Transaction a unified concept.

Instead of having completely independent logic for:

Receipt
Issue
Transfer
Adjustment
Return
Consumption

they should all ultimately produce inventory movements.

Conceptually:

                 ┌──────────────┐
                 │   BUSINESS   │
                 │  TRANSACTION │
                 └───────┬──────┘
                         │
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
       Purchase        Pharmacy        Ward
       Receipt        Dispensing       Issue
          │              │              │
          └──────────────┼──────────────┘
                         ▼
                ┌─────────────────┐
                │ STOCK MOVEMENT  │
                │    LEDGER       │
                └────────┬────────┘
                         ▼
                ┌─────────────────┐
                │ STOCK BALANCE   │
                └─────────────────┘

That architecture will save you a lot of pain later.

If this were my project

I'd build the inventory module in this order:

Phase 1 — Foundation

Item master
Categories
Units/conversions
Locations
Batches/expiry
Inventory balances
Stock movement ledger
Permissions/audit

Phase 2 — Operations
9. Stock receiving
10. Stock issuing
11. Transfers
12. Reservations
13. Stock requests
14. Stock counts
15. Adjustments

Phase 3 — Procurement
16. Suppliers
17. Purchase requisitions
18. Purchase orders
19. Goods receiving
20. Supplier returns
21. Reorder management

Phase 4 — Hospital integration
22. Pharmacy
23. Laboratory
24. Theatre
25. Wards
26. Patient consumption
27. Billing
28. Finance

Phase 5 — Intelligence
29. Expiry alerts
30. FEFO
31. Demand forecasting
32. Consumption analytics
33. Supplier analytics
34. Inventory valuation
35. Multi-branch consolidation

That gives you a solid foundation without trying to build the entire hospital ERP at once