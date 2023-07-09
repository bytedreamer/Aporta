using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
	public class _0009_AddEventTable : IMigration
	{
		public int Version => 9;

		public string Name => "Add Event table";

		public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
		{
			await connection.ExecuteAsync(
				@"create table event
                    (
                        id integer 				not null
                            constraint event_pk
                                primary key autoincrement,
						endpoint_id	integer 	not null
							constraint endpoint_id_fk
								references endpoint,
                        timestamp datetime 		not null,
                        event_type integer 		not null,
                        data text 				not null
                    );

                    create unique index event_id_uindex
                        on event (id);

					create unique index credential_number_uindex
						on credential (number);
					",
				transaction: transaction);
		}
	}
}