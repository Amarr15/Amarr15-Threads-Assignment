
# Assignment 03 - Answers

Student name: Ammar Yasser
Group / ID: G2

## 1. What is System.Threading.Monitor used for?

`System.Threading.Monitor` is used to synchronize access to shared objects and protect critical sections. It allows only one thread at a time to hold a lock on a specific object, preventing multiple threads from modifying shared data simultaneously.

## 2. What does the C# lock statement compile to?

For a normal reference-type lock object, the C# `lock` statement is translated into `Monitor.Enter` and `Monitor.Exit` calls wrapped in a `try/finally` structure. This ensures that the lock is released even if an exception occurs inside the critical section.

## 3. List at least two differences between using lock and calling Monitor APIs directly.

1. **Simplicity and safety:** `lock` is shorter and automatically releases the lock. When using `Monitor.Enter` and `Monitor.Exit` directly, the developer must use `try/finally` to avoid leaving the lock held after an exception.
2. **Additional control:** `Monitor` provides methods such as `TryEnter`, `Wait`, and `Pulse`, which allow timed lock attempts and communication between threads. A basic `lock` statement does not provide these operations directly.
3. **Flexibility:** Direct `Monitor` calls allow more advanced synchronization patterns, but they require more careful handling and can introduce bugs if used incorrectly.

## 4. When might a developer use Monitor.TryEnter, Monitor.Pulse, or Monitor.Wait instead of a plain lock?

- `Monitor.TryEnter` is useful when a thread should try to acquire a lock without waiting indefinitely, or wait only for a specified timeout.
- `Monitor.Wait` is useful when a thread needs to wait for a condition to change. It temporarily releases the lock while waiting and reacquires it before returning.
- `Monitor.Pulse` is useful when a thread changes shared state and needs to notify one waiting thread that it may continue. The notified thread must reacquire the lock before continuing.

These methods are useful in coordination scenarios such as producer-consumer patterns. The condition should be checked again after waiting because notification does not guarantee that the condition is still true.

## 5. Write the same critical section twice: once with lock, once with Monitor.Enter / Monitor.Exit.

Both examples below protect the same operation: incrementing a shared counter.

### Using lock

```csharp
private readonly object _sync = new object();
private int _counter = 0;

public void Increment()
{
    lock (_sync)
    {
        _counter++;
    }
}
```

### Using Monitor.Enter / Monitor.Exit

private readonly object _sync = new object();
private int _counter = 0;

public void Increment()
{
    bool lockTaken = false;

    try
    {
        Monitor.Enter(_sync, ref lockTaken);
        _counter++;
    }
    finally
    {
        if (lockTaken)
        {
            Monitor.Exit(_sync);
        }
    }
}

Both versions ensure that only one thread at a time can increment `_counter` while holding the same lock object. The `lockTaken` variable helps ensure that `Monitor.Exit` is called only if the lock was successfully acquired.

## Links

- [Microsoft Learn - Monitor Class](https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor)
- [Microsoft Learn - C# lock statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock)
