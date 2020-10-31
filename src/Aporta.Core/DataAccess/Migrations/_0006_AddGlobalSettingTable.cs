using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
	public class _0006_AddGlobalSettingTable : IMigration
	{
		public int Version => 6;

		public string Name => "Add Global Setting table";

		public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
		{
			await connection.ExecuteAsync(
				@"create table global_setting
					(
					    name  text not null
					        constraint global_setting_pk
					            primary key,
					    value text not null
					);

					create unique index global_setting_name_uindex
					    on global_setting (name);",
				transaction: transaction);
		}
	}
}