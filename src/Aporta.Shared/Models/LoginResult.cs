using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aporta.Shared.Models
{
    public class LoginResult
    {
        public bool Success {  get; set; } 
        public User User { get; set; }
    }
}
