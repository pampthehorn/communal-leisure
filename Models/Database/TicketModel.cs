using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace website.Models.Database
{
    [TableName("Ticket")]
    [PrimaryKey("Id", AutoIncrement = true)]
    public class TicketModel
    {

        [PrimaryKeyColumn(AutoIncrement = true)]
        public int Id { get; set; }
        public Guid EventNodeId { get; set; }
        public Guid TicketId { get; set; }
        public int Quantity { get; set; }
        public string Type { get; set; }
        public string EventName { get; set; }
        public int Cost { get; set; }
        public int OrderId { get; set; }

        [Column("TicketCodes")]
        public string TicketCodes { get; set; } = string.Empty;
    }
}
