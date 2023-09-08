using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
	public class _0010_AddLastEventToCredentialTable : IMigration
	{
		public int Version => 10;

		public string Name => "All Last Event to Credenial table";

		public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
		{
			await connection.ExecuteAsync(
				@"create table credential_dg_tmp
						(
						    id         integer not null
						        constraint credential_pk
						            primary key autoincrement,
						    number     text    not null,
						    last_event integer
						);

						insert into credential_dg_tmp(id, number)
						select id, number
						from credential;

						drop table credential;

						alter table credential_dg_tmp
						    rename to credential;

						create unique index credential_id_uindex
						    on credential (id);

						create unique index credential_number_uindex
						    on credential (number);
						",
				transaction: transaction);
		}
	}
}