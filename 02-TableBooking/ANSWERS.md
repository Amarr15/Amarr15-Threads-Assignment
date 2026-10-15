# Part 02 - Table Booking

**Student Name:** Ammar Yasser

**Assignment:** Async, Threads & Thread Safety

## Problem

Multiple customer threads attempt to book a limited number of tables concurrently. Without synchronization, multiple threads can read the same available table count and cause a race condition.

## Solution

I used a private lock object and placed the availability check and booking updates inside a `lock` statement. This ensures that only one thread executes the critical section at a time.

## Actual Output

```text
Customer-01 booked a table. Left=4
Customer-02 booked a table. Left=3
Customer-03 booked a table. Left=2
Customer-04 booked a table. Left=1
Customer-05 booked a table. Left=0

Booked=5, TablesLeft=0
```

The customer order may vary between runs.

## Assumptions

- The booking system starts with five available tables.
- Each successful booking reserves exactly one table.
- Once no tables remain, additional booking attempts are ignored.
- The lock protects the availability check and both counter updates.
