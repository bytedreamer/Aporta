using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
	public class _0007_AddCredentialTable : IMigration
	{
		public int Version => 7;

		public string Name => "Add Credential table";

		public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
		{
			await connection.ExecuteAsync(
				@"create table credential
					(
						id integer not null
							constraint credential_pk
								primary key autoincrement,
						number text not null,
						enroll_date datetime not null
					);

					create unique index credential_id_uindex
						on credential (id);",
				transaction: transaction);
		}
	}
}