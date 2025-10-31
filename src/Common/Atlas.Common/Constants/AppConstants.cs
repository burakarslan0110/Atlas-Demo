namespace Atlas.Common.Constants;

public static class AppConstants
{
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string User = "User";
    }

    public static class OrderStatus
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
    }

    public static class PaymentStatus
    {
        public const string Pending = "Pending";
        public const string Success = "Success";
        public const string Failed = "Failed";
    }

    public static class PaymentMethod
    {
        public const string CreditCard = "credit_card";
        public const string BankTransfer = "bank_transfer";
        public const string Cash = "cash";
    }

    public static class CacheKeys
    {
        public const string ProductList = "products:list:{0}";
        public const string ProductDetail = "products:detail:{0}";
        public const string CategoryTree = "categories:tree";
        public const string Cart = "cart:{0}";
    }

    public static class CacheDuration
    {
        public const int ProductList = 600;
        public const int ProductDetail = 3600;
        public const int CategoryTree = 1800;
    }

    public static class EventTypes
    {
        public const string ProductCreated = "product.created";
        public const string ProductUpdated = "product.updated";
        public const string ProductDeleted = "product.deleted";
        public const string ProductStockChanged = "product.stock.changed";
        public const string OrderCreated = "order.created";
        public const string OrderCancelled = "order.cancelled";
    }

    public static class Exchanges
    {
        public const string ProductEvents = "product.events";
        public const string OrderEvents = "order.events";
    }

    public static class Queues
    {
        public const string ProductSearchIndexer = "product.search.indexer";
        public const string OrderNotification = "order.notification";
    }
}
