using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
	public class _0008_AddPersonTable : IMigration
	{
		public int Version => 8;

		public string Name => "Add Person table";

		public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
		{
			await connection.ExecuteAsync(
				@"create table person
				(
					id integer not null
						constraint person_pk
							primary key autoincrement,
					first_name text,
					last_name text,
					enabled integer default 0 not null
				);

				create unique index person_id_uindex
					on person (id);

				create table credential_assignment
				(
					person_id integer not null
						references person,
					credential_id integer not null
						constraint credential_assignment_pk
							primary key
						references credential,
					enabled integer default 0 not null
				);

				create unique index credential_assignment_credential_id_uindex
					on credential_assignment (credential_id);",
				transaction: transaction);
		}
	}
}