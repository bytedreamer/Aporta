using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
    internal class _0011_AddUserTable : IMigration
    {
        public int Version => 11;

        public string Name => "Add User table";

        public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
        {
            await connection.ExecuteAsync(
                @"create table user
				(
					id integer not null
						constraint user_pk
							primary key autoincrement,
					person_id	integer not null
						constraint user_person_id_fk
							references person
								on update cascade on delete cascade,
					password text
				);

				create unique index user_id_uindex
					on user (id);

				insert into person (first_name, last_name, enabled) values ('admin', 'admin', 1);

				insert into user (person_id, password) select id, 'aporta123' from person where first_name = 'admin';

				", transaction: transaction);
        }
    }
}

