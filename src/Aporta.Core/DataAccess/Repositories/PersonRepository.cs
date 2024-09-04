using Aporta.Shared.Models;

namespace Aporta.Core.DataAccess.Repositories;

public class PersonRepository : BaseRepository<Person>
{
    public PersonRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;
    }
        
    protected override IDataAccess DataAccess { get; }
        
    protected override string SqlSelect => @"select person.id, 
                                                    person.first_name as firstName, 
                                                    person.last_name as lastName,
                                                    person.enabled
                                                    from person";
        
    protected override string SqlInsert => @"insert into person
                                                (first_name, last_name, enabled) values 
                                                (@firstName, @lastName, @enabled)";

    // ReSharper disable once UnassignedGetOnlyAutoProperty
    protected override string SqlUpdate { get; }

    protected override string SqlDelete => @"delete from person where id = @id";
    
    protected override string SqlRowCount => @"select count(*) from person";
        
    protected override object InsertParameters(Person person)
    {
        return new
        {
            firstName = person.FirstName,
            lastName = person.LastName,
            enabled = person.Enabled
        };
    }

    protected override object UpdateParameters(Person record)
    {
        throw new System.NotImplementedException();
    }

    protected override void InsertId(Person person, int id)
    {
        person.Id = id;
    }
}