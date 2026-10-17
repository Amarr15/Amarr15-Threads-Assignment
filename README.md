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
+-- README.md
+-- submission/
    +-- assignment/
    |   +-- README.md
    |   +-- 01-InvoiceExport/      (Async/await order export project and answers)
    |   +-- 02-TableBooking/       (Thread-safe table booking project and answers)
    |   +-- 03-LockingResearch/    (Monitor, lock compilation, and comparison answers)
    |   +-- 04-MenuViews/          (Interlocked view counter project and answers)
    |   +-- 05-ViewsAndEmails/     (Concurrent collections project and answers)
    |   +-- 06-OrderProcessing/    (Order processing phases, outputs, screenshots, and README)
    +-- linkedin/
        +-- posts.md               (Public LinkedIn post links)
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
