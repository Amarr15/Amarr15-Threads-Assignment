using System.Threading.Tasks;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class CouponRepository
{
    private readonly JsonFileStore _store;

    public CouponRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<List<Coupon>> GetAllAsync(
        CancellationToken ct)
    {
        Log.Info("CouponRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("CouponRepository -> Executing query to get all coupons...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        return await _store.ReadAsync<Coupon>(
            DataFiles.Coupons,
            ct);
    }

    public async Task<Coupon?> GetByCodeAsync(
        string code,
        CancellationToken ct)
    {
        Log.Info($"CouponRepository -> Opening connection to get coupon '{code}'...");
        await Task.Delay(200, ct);

        Log.Info($"CouponRepository -> Executing query for coupon '{code}'...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Coupon> coupons =
            await _store.ReadAsync<Coupon>(
                DataFiles.Coupons,
                ct);

        return coupons.Find(coupon =>
            coupon.Code.Equals(
                code,
                StringComparison.OrdinalIgnoreCase));
    }
}