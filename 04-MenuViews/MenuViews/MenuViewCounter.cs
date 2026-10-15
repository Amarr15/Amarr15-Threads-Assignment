namespace MenuViews;
using System.Threading;

public sealed class MenuViewCounter
{
    public int Views;

    public void RegisterView()
    {
        Interlocked.Increment(ref Views);
    }
}
