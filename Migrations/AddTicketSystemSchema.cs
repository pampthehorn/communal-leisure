using System.Data;
using Umbraco.Cms.Infrastructure.Migrations;
using website.Models.Database;

namespace website.Migrations;

public class AddTicketSystemSchema : MigrationBase
{
    public AddTicketSystemSchema(IMigrationContext context) : base(context)
    {
    }

    protected override void Migrate()
    {
        if (!TableExists("Orders"))
        {
            Create.Table<OrderModel>().Do();
        }

        if (!TableExists("Ticket"))
        {
            Create.Table<TicketModel>().Do();
        }

        const string constraintName = "FK_Ticket_Orders";

        var sqlToCheckConstraint = @$"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
            WHERE CONSTRAINT_TYPE = 'FOREIGN KEY'
            AND CONSTRAINT_NAME = '{constraintName}'";

        var constraintExists = Context.Database.ExecuteScalar<int>(sqlToCheckConstraint) > 0;

        if (!constraintExists)
        {
            Create.ForeignKey(constraintName)
                .FromTable("Ticket").ForeignColumn("OrderId")
                .ToTable("Orders").PrimaryColumn("Id")
                .OnDelete(Rule.Cascade)
                .Do();
        }
    }
}