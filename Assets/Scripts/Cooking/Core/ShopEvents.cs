namespace Cook.Core
{
    // SaleReady 仅表示确认了待售成品，尚未运行顾客服务或售卖判定。
    public enum ShopPhase { Production, SaleSelection, SaleReady }

    public readonly struct InventoryChanged { }

    public readonly struct ShopPhaseChanged
    {
        public ShopPhaseChanged(ShopPhase phase) { Phase = phase; }
        public ShopPhase Phase { get; }
    }
}
