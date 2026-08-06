using System.IO;

namespace Direction.NFSe.Danfe
{
    public interface IDanfeService
    {
        DanfeResult Generate(NFSeSchema nfse, DanfeEnvironment environment, DanfeStatus status = DanfeStatus.Autorizada);
        DanfeResult Generate(Stream xmlStream, DanfeEnvironment environment, DanfeStatus status = DanfeStatus.Autorizada);
        DanfeResult Generate(string xml, DanfeEnvironment environment, DanfeStatus status = DanfeStatus.Autorizada);

        [System.Obsolete("Use Generate(NFSeSchema, DanfeEnvironment, DanfeStatus).")]
        DanfeResult Generate(NFSeSchema nfse, DanfeEnvironment environment, bool isCancelled);

        [System.Obsolete("Use Generate(Stream, DanfeEnvironment, DanfeStatus).")]
        DanfeResult Generate(Stream xmlStream, DanfeEnvironment environment, bool isCancelled);

        [System.Obsolete("Use Generate(string, DanfeEnvironment, DanfeStatus).")]
        DanfeResult Generate(string xml, DanfeEnvironment environment, bool isCancelled);
    }
}
