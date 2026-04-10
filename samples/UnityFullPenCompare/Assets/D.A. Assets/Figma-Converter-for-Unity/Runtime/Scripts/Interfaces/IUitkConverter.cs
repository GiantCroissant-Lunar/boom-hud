using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DA_Assets.FCU
{
    public interface IUitkConverter
    {
        void Init(FigmaConverterUnity monoBeh);
        Task Convert(FObject virtualPage, List<FObject> currPage, CancellationToken token);
    }
}
