using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
	public class _0004_AddInputTable : IMigration
	{
		public int Version => 4;

		public string Name => "Add input table";

		public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
		{
			await connection.ExecuteAsync(
				@"create table input
					(
						id 			integer 			not null
							constraint input_pk
								primary key autoincrement,
						endpoint_id	integer 			not null
							constraint input_endpoint_id_fk
								references endpoint
									on update cascade on delete cascade,
					    name		text				not null
					);

				create unique index input_id_uindex
					on input (id);",
				transaction: transaction);
		}
	}
}