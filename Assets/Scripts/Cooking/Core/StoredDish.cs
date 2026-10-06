using System;

namespace Cook.Core
{
    /// <summary>一道实际成品；同菜谱的多道成品也有独立身份和制作记录。</summary>
    public sealed class StoredDish
    {
        internal StoredDish(ProductionResult result)
        {
            Id = Guid.NewGuid().ToString("N");
            Result = result;
        }

        public string Id { get; }
        public ProductionResult Result { get; }
    }
}
