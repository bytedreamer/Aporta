using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
	public class _0003_AddOutputTable : IMigration
	{
		public int Version => 3;

		public string Name => "Add output table";

		public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
		{
			await connection.ExecuteAsync(
				@"create table output
					(
						id 			integer 			not null
							constraint output_pk
								primary key autoincrement,
						endpoint_id	integer 			not null
							constraint output_endpoint_id_fk
								references endpoint
									on update cascade on delete cascade,
					    name		text				not null,
						pulse_timer	integer	default 5 	not null	
					);

				create unique index output_id_uindex
					on output (id);",
				transaction: transaction);
		}
	}
}