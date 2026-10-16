# Simulation Academy — .NET Diploma (Cycle 1)
## Async, Threads & Thread Safety Assignment

**Student Name:** Ammar Yasser  
**Group / ID:** G2  
**Track:** Backend .NET  
**Branch:** `assignment/1-8`

---

## Repository Structure

```
.
+-- README.md                      (Student information, structure, notes & assumptions)
+-- 01-InvoiceExport/
|   +-- InvoiceExport/             (Fixed async/await order export project)
|   +-- ANSWERS.md                 (Original behavior, problems found, changes made)
+-- 02-TableBooking/
|   +-- TableBooking/              (Fixed thread-safe table booking project using lock)
|   +-- ANSWERS.md                 (Problem explanation, lock solution, actual output)
+-- 03-LockingResearch/
|   +-- ANSWERS.md                 (Answers to Q1-Q5 on Monitor, lock compilation, and comparison)
+-- 04-MenuViews/
|   +-- MenuViews/                 (Fixed view counter using Interlocked.Increment)
|   +-- ANSWERS.md                 (Race condition analysis, atomic update fix)
+-- 05-ViewsAndEmails/
|   +-- ViewsAndEmails/            (ConcurrentDictionary & ConcurrentQueue implementation)
|   +-- ANSWERS.md                 (Concurrency failures, thread-safe collections vs business rules)
+-- 06-OrderProcessing/
|   +-- OrderProcessing/           (Async Order Processing System targeting net10.0: Phases 1 to 4)
|   +-- sample-output/             (phase1/ ... phase4/ - final JSON files)
|   +-- screenshots/               (Console execution screenshots)
|   +-- README.md                  (Comprehensive architecture design, benchmark table, and answers)
+-- LinkedIn/
    +-- linked.md                  (Public LinkedIn post references for required topics)
```

---

## Notes & Assumptions

1. **Target Framework:**
   - The mini-project targets `.NET 10` (`<TargetFramework>net10.0</TargetFramework>`) per technical requirements.
   - Leverages modern .NET 10 features like `Task.WhenEach` for live task completion streaming.

2. **Concurrency & Thread Safety:**
   - In Parts 01–05 and Mini Project Phases 1–3, tasks are started and awaited one-by-one without group APIs (`Task.WhenAll`/`WhenAny`), complying strictly with Part 00 rules. Phase 4 lifts this constraint to scale up.
   - No calls to `.Result` or `.Wait()` exist anywhere in production paths; the single required demonstration is cleanly isolated in `BlockingWaitDemo.cs`.
   - File storage integrity is guaranteed through atomic `.tmp` file swapping and per-file `SemaphoreSlim` mutual exclusion.

3. **External Services & Latency:**
   - Database operations, payments, shipping APIs, and SMTP are simulated with asynchronous delays (`Task.Delay`) and thread-safe logging.
   - Failures (timeouts, declined payments, database write aborts, and SMTP retries) are controlled through constructor injection.
