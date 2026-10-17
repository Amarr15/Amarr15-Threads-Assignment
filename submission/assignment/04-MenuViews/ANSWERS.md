# \# Assignment 04 — Answers

# 

# Student name: Ammar Yasser  

# Group / ID: G2

# 

# \## Why Expected and Actual disagree

# 

# The original code uses `Views++`, which is not an atomic operation. It involves reading the current value, incrementing it, and writing the result. When multiple threads perform these steps at the same time, some updates can be lost because of a race condition. As a result, the actual count may be lower than the expected count of 400,000.

# 

# \## What you changed

# 

# I replaced `Views++` with `Interlocked.Increment(ref Views)`. The `Interlocked.Increment` method performs the increment as an atomic operation, preventing concurrent threads from losing updates to the shared counter.

# 

# \*\*Actual output after the fix:\*\*

# 

# ```text

# Expected: 400000

# Actual:   400000

# ```

# 

# \## When would you protect the update differently

# 

# For a simple counter increment, `Interlocked.Increment` is efficient and appropriate. I would use `lock` when the critical section contains multiple related operations that must be executed together as one synchronized section, or when more complex shared state needs protection.

