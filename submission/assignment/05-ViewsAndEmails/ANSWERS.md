# Assignment 05 — Answers

Student name: Ammar Yasser  
Group / ID: G2

## Failures you observed

In the original implementation, the view count was lower than expected because multiple threads updated the same dictionary entry concurrently. In my test, the expected count was 50,000, but the actual count was 46,306.

The original code also accessed a regular Queue from multiple threads without synchronization. This could cause race conditions when workers checked the queue and removed jobs. Although all 20 email jobs were sent in my initial run, the implementation was not thread-safe and could fail under different scheduling conditions.

## What you changed

I replaced `Dictionary<string, int>` with `ConcurrentDictionary<string, int>` and used `AddOrUpdate` to update view counts safely.

I replaced `Queue<string>` with `ConcurrentQueue<string>` and used `TryDequeue` so multiple email workers can safely retrieve jobs. I kept a lock around adding results to `SentEmails` because `List<string>` is not thread-safe. The workers stop when the producers have finished and the queue is empty.

After the changes, the actual view count was 50,000, and all 20 email jobs were sent without duplicates.

**Actual output after the fix:**

```text
Views expected=50000, actual=50000
Email jobs placed=20, unique sent=20, total sends=20
```

## Collection safety vs business-rule safety

A thread-safe collection protects its own operations, but it does not automatically make a sequence of business operations atomic. For example, checking a queue and then removing an item using separate operations can be unsafe. `ConcurrentQueue.TryDequeue` combines the check and removal safely.

Likewise, `ConcurrentDictionary` protects concurrent dictionary operations, but the application must still choose the correct atomic update operation, such as `AddOrUpdate`, to preserve the view-counting rule.