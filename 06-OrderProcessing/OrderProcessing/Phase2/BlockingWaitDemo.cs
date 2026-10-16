using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Logging;

namespace OrderProcessing.Phase2;

/// <summary>
/// Small separate demo method demonstrating the difference and danger of calling
/// .Result or .Wait() on an unfinished task instead of awaiting it.
/// This is the ONLY place in the project where .Result / .Wait() is allowed per assignment rules.
/// </summary>
public static class BlockingWaitDemo
{
    public static void RunDemo()
    {
        Console.WriteLine("\n===== Blocking Wait (.Result / .Wait()) Demo =====");

        // 1. Simulating an asynchronous task
        Func<Task<string>> computeAsync = async () =>
        {
            await Task.Delay(200);
            return "Data loaded successfully";
        };

        // 2. Synchronous blocking with .Result
        var sw = Stopwatch.StartNew();
        int callingThreadBefore = Environment.CurrentManagedThreadId;

        // Calling .Result on an unfinished task blocks the current thread synchronously
        Task<string> task = computeAsync();
        string result = task.Result; // Synchronous blocking!
        sw.Stop();

        int callingThreadAfter = Environment.CurrentManagedThreadId;

        Console.WriteLine($"[BlockingWaitDemo] Result retrieved: '{result}' in {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"[BlockingWaitDemo] Calling Thread before: {callingThreadBefore}, after: {callingThreadAfter} (Thread was frozen/blocked waiting)");

        // 3. Demonstrating exception wrapping (AggregateException)
        Func<Task> failingAsync = async () =>
        {
            await Task.Delay(100);
            throw new InvalidOperationException("Demo inner failure");
        };

        try
        {
            Task failingTask = failingAsync();
            failingTask.Wait(); // Throws AggregateException wrapping the real exception!
        }
        catch (AggregateException ae)
        {
            Console.WriteLine($"[BlockingWaitDemo] Caught AggregateException: {ae.GetType().Name} wrapping {ae.InnerException?.GetType().Name}: {ae.InnerException?.Message}");
        }

        Console.WriteLine("==================================================\n");
    }
}
