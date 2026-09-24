using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCanteen.Services.Lifetimes
{
    public class TransientService : ITransientService
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
    }
}
