using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
	public class _0005_AddDoorTable : IMigration
	{
		public int Version => 5;

		public string Name => "Add Door table";

		public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
		{
			await connection.ExecuteAsync(
				@"create table door
					(
						id 					integer 	not null
							constraint door_pk
								primary key autoincrement,
						in_access_endpoint_id	integer 
							constraint in_access_endpoint_id_fk
								references endpoint
									on update cascade on delete cascade,
						out_access_endpoint_id	integer
							constraint out_access_endpoint_id_fk
								references endpoint
									on update cascade on delete cascade,
						door_contact_endpoint_id	integer 
							constraint door_contact_endpoint_id_fk
								references endpoint
									on update cascade on delete cascade,
						request_to_exit_endpoint_id	integer
							constraint request_to_exit_endpoint_id_fk
								references endpoint
									on update cascade on delete cascade,
						door_strike_endpoint_id		integer
							constraint door_strike_endpoint_id_fk
								references endpoint
									on update cascade on delete cascade,
					    name			text				not null,
						strike_timer	integer	default 3 	not null	
					);

				create unique index door_id_uindex
					on door (id);",
				transaction: transaction);
		}
	}
}