using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCanteen.Services.Lifetimes
{
    public interface ITransientService { Guid InstanceId { get; } }
    public interface IScopedService { Guid InstanceId { get; } }
    public interface ISingletonService { Guid InstanceId { get; } }

}
