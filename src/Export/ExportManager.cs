using System.Collections.Generic;
using ShieldBlock.Core;

namespace ShieldBlock.Export
{
    /// <summary>
    /// Fachada para os exportadores. Recebe estado completo e gera o arquivo
    /// no formato escolhido, retornando o hash SHA-256 do resultado.
    /// </summary>
    public class ExportManager
    {
        public enum Format { PDF, HTML, JSON, XML_DFRWS }

        public string Export(Format format,
                             string outputPath,
                             LogSession session,
                             List<DriveRecord> drives,
                             List<DiskRecord> disks,
                             List<PartitionRecord> partitions)
        {
            switch (format)
            {
                case Format.PDF:
                    return new PdfExporter().Export(outputPath, session, drives, disks, partitions);
                case Format.HTML:
                    return new HtmlExporter().Export(outputPath, session, drives, disks, partitions);
                case Format.JSON:
                    return new JsonExporter().Export(outputPath, session, drives, disks, partitions);
                case Format.XML_DFRWS:
                    return new XmlDfrwsExporter().Export(outputPath, session, drives, disks, partitions);
                default:
                    return null;
            }
        }

        public static string DefaultExtension(Format f)
        {
            switch (f)
            {
                case Format.PDF:       return ".pdf";
                case Format.HTML:      return ".html";
                case Format.JSON:      return ".json";
                case Format.XML_DFRWS: return ".xml";
                default: return ".dat";
            }
        }

        public static string Label(Format f)
        {
            switch (f)
            {
                case Format.PDF:       return "PDF (Laudo)";
                case Format.HTML:      return "HTML (Interativo)";
                case Format.JSON:      return "JSON (Estruturado)";
                case Format.XML_DFRWS: return "XML (DFRWS)";
                default: return f.ToString();
            }
        }
    }
}
