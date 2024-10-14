using Aporta.Shared.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aporta.Core.DataAccess.Repositories
{
    public class UserRepository : BaseRepository<User>
    {

        public UserRepository(IDataAccess dataAccess)
        {
            DataAccess = dataAccess;
        }

        protected override IDataAccess DataAccess { get; }

        protected override string SqlSelect => @"select user.id, 
                                                    user.person_id,
                                                    user.password
                                                    from user";
        //insert into user (person_id, password)
        protected override string SqlInsert => @"insert into user
                                                (person_id, password) values 
                                                (@person_id, @password)";

        // ReSharper disable once UnassignedGetOnlyAutoProperty
        protected override string SqlUpdate { get; }

        protected override string SqlDelete => @"delete from user where id = @id";

        protected override string SqlRowCount => @"select count(*) from user";

        public async Task<User> GetForPassword(string password)
        {
            using var connection = DataAccess.CreateDbConnection();
            connection.Open();

            return await connection.QueryFirstAsync<User>(SqlSelect +
                                                            @"  where user.password = @password",
                new { password });
        }

        protected override object InsertParameters(User user)
        {
            return new
            {
                id = user.Id,
                person_id = user.Person.Id,
                password = user.Password
            };
        }

        protected override object UpdateParameters(User record)
        {
            throw new System.NotImplementedException();
        }

        protected override void InsertId(User user, int id)
        {
            user.Id = id;
        }

    }
}
