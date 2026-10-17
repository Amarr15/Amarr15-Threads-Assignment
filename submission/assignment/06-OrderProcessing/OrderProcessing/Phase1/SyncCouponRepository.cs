using System;
using System.Collections.Generic;
using System.Threading;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class SyncCouponRepository
{
    private readonly JsonFileStore _store;

    public SyncCouponRepository(JsonFileStore store)
    {
        _store = store;
    }

    public List<Coupon> GetAll()
    {
        Log.Info("SyncCouponRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info("SyncCouponRepository -> Executing query to get all coupons...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        return _store.Read<Coupon>(DataFiles.Coupons);
    }

    public Coupon? GetByCode(string code)
    {
        Log.Info($"SyncCouponRepository -> Opening connection for coupon '{code}'...");
        Thread.Sleep(200);

        Log.Info($"SyncCouponRepository -> Executing query for coupon '{code}'...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        List<Coupon> coupons =
            _store.Read<Coupon>(DataFiles.Coupons);

        return coupons.Find(coupon =>
            coupon.Code.Equals(
                code,
                StringComparison.OrdinalIgnoreCase));
    }
}