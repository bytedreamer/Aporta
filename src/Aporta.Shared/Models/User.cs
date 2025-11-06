using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aporta.Shared.Models
{
    public class User
    {
        public int Id { get; set; }

        public int PersonId { get; set; }

        public string Password { get; set; }

        public Person Person { get; set; }
    }
}
