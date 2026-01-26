using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace website.Models.Database
{
    [TableName("Orders")]
    [PrimaryKey("Id", AutoIncrement = true)]
    public class OrderModel
    {
        [PrimaryKeyColumn(AutoIncrement = true)]
        public int Id { get; set; }

        public long TotalAmount { get; set; }

        public string CustomerName { get; set; }

        public string CustomerEmail { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = string.Empty;

        public string StripeSessionId { get; set; } = string.Empty;

        public string StripeCustomerId { get; set; } = string.Empty;
    }
}
