using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitInstaller.Infrastructure
{
    public class BaseMessage
    {
        public string Id { get; set; }

        public string Message { get; set; }
        //public DateTime Created { get; set; }
    }
}
