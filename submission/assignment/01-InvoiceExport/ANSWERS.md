# Part 01 — Invoice Export

**Student Name:** Ammar Yasser  
**Group / ID:** G2  

## R1 — Original Behavior

Ran the original project using `dotnet run --project InvoiceExport`.

The application successfully exported five invoices: 1001, 1002, 1003, 1004, and 1005.

**Original execution time:** 5.9 seconds.

## R2 — Problems Found

1. `Thread.Sleep` blocks the current thread while waiting.
2. Synchronous file operations block the executing thread during file I/O.
3. Processing every order sequentially limits overlap between independent I/O operations and can increase total execution time when the number of orders grows.

## R3 — Changes Made

1. Converted `GetPendingOrders` to `GetPendingOrdersAsync` and replaced `Thread.Sleep` with `await Task.Delay`.
2. Converted `GetOrderById` to `GetOrderByIdAsync` and replaced `Thread.Sleep` with `await Task.Delay`.
3. Converted customer profile reading to `ReadProfileAsync`, using `File.ReadAllTextAsync`.
4. Converted invoice writing to `WriteInvoiceAsync`, using `File.WriteAllTextAsync`.
5. Extracted the processing of a single order into `ExportOrderAsync`.
6. Started the order-processing tasks before awaiting them individually, without using `Task.WhenAll`.

The business flow remains unchanged: load order → read customer profile → write invoice. The invoice content and the five invoice IDs remain unchanged.

## R4 — Console Output and Results

### Before Changes

```text
Starting invoice export...
Customer files: [original project's Data directory]
Invoice output: [original project's invoices directory]

Wrote [path]\invoice-1001.txt
Wrote [path]\invoice-1002.txt
Wrote [path]\invoice-1003.txt
Wrote [path]\invoice-1004.txt
Wrote [path]\invoice-1005.txt
Export finished in 5.9 s
```

### After Changes

```text
Starting invoice export...
Customer files: [updated project's Data directory]
Invoice output: [updated project's invoices directory]

Wrote [path]\invoice-1001.txt
Wrote [path]\invoice-1002.txt
Wrote [path]\invoice-1003.txt
Wrote [path]\invoice-1004.txt
Wrote [path]\invoice-1005.txt
Export finished in 6.0 s
```

**Result:** Both runs successfully produced the same five invoices with unchanged content. The measured execution time did not improve in this run (5.9 seconds before and 6.0 seconds after). Async I/O avoids blocking a thread during waits, but does not guarantee a lower elapsed time in every run.
