using System.Collections.Generic;
using System.IO;
using System.Text;
using ShieldBlock.Core;
using ShieldBlock.Utils;

namespace ShieldBlock.Export
{
    /// <summary>
    /// Exportador JSON estruturado — sem dependência externa.
    /// Compatível com Autopsy, X-Ways e ferramentas que aceitam JSON forense.
    /// </summary>
    public class JsonExporter
    {
        public string Export(string path,
                             LogSession session,
                             List<DriveRecord> drives,
                             List<DiskRecord> disks,
                             List<PartitionRecord> partitions)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"tool\": \"ShieldBlock v6.0\",\n");
            sb.Append("  \"format_version\": \"1.0\",\n");
            sb.Append("  \"generated_at\": ").Append(JsonStr(Iso8601.FormatNow())).Append(",\n");

            // SESSÃO
            sb.Append("  \"session\": {\n");
            sb.Append("    \"id\": ").Append(JsonStr(session.SessionId)).Append(",\n");
            sb.Append("    \"started_at\": ").Append(JsonStr(Iso8601.Format(session.StartedAt))).Append(",\n");
            sb.Append("    \"operator_user\": ").Append(JsonStr(session.OperatorUser)).Append(",\n");
            sb.Append("    \"machine_name\": ").Append(JsonStr(session.MachineName)).Append(",\n");
            sb.Append("    \"event_count\": ").Append(session.EventCount).Append(",\n");
            sb.Append("    \"final_hash\": ").Append(JsonStr(session.FinalHash)).Append(",\n");
            sb.Append("    \"integrity_ok\": ").Append(session.VerifyIntegrity() ? "true" : "false").Append("\n");
            sb.Append("  },\n");

            // PERITO
            sb.Append("  \"perito\": {\n");
            sb.Append("    \"nome\": ").Append(JsonStr(session.Perito.Nome)).Append(",\n");
            sb.Append("    \"cpf\": ").Append(JsonStr(session.Perito.CPF)).Append(",\n");
            sb.Append("    \"registro\": ").Append(JsonStr(session.Perito.Registro)).Append(",\n");
            sb.Append("    \"orgao\": ").Append(JsonStr(session.Perito.Orgao)).Append(",\n");
            sb.Append("    \"cargo\": ").Append(JsonStr(session.Perito.Cargo)).Append(",\n");
            sb.Append("    \"email\": ").Append(JsonStr(session.Perito.Email)).Append("\n");
            sb.Append("  },\n");

            // DRIVES
            sb.Append("  \"drives\": [\n");
            for (int i = 0; i < drives.Count; i++)
            {
                var d = drives[i];
                sb.Append("    {");
                sb.Append("\"letter\": ").Append(JsonStr(d.Letter)).Append(", ");
                sb.Append("\"label\": ").Append(JsonStr(d.Label)).Append(", ");
                sb.Append("\"file_system\": ").Append(JsonStr(d.FileSystem)).Append(", ");
                sb.Append("\"drive_type\": ").Append(JsonStr(d.DriveType)).Append(", ");
                sb.Append("\"total_bytes\": ").Append(d.TotalBytes).Append(", ");
                sb.Append("\"free_bytes\": ").Append(d.FreeBytes).Append(", ");
                sb.Append("\"ready\": ").Append(d.IsReady ? "true" : "false");
                sb.Append("}").Append(i < drives.Count - 1 ? "," : "").Append("\n");
            }
            sb.Append("  ],\n");

            // DISKS
            sb.Append("  \"physical_disks\": [\n");
            for (int i = 0; i < disks.Count; i++)
            {
                var d = disks[i];
                sb.Append("    {");
                sb.Append("\"device_id\": ").Append(JsonStr(d.DeviceID)).Append(", ");
                sb.Append("\"model\": ").Append(JsonStr(d.Model)).Append(", ");
                sb.Append("\"interface\": ").Append(JsonStr(d.InterfaceType)).Append(", ");
                sb.Append("\"media_type\": ").Append(JsonStr(d.MediaType)).Append(", ");
                sb.Append("\"serial\": ").Append(JsonStr(d.SerialNumber)).Append(", ");
                sb.Append("\"size_bytes\": ").Append(d.SizeBytes).Append(", ");
                sb.Append("\"partitions\": ").Append(d.Partitions);
                sb.Append("}").Append(i < disks.Count - 1 ? "," : "").Append("\n");
            }
            sb.Append("  ],\n");

            // PARTITIONS
            sb.Append("  \"partitions\": [\n");
            for (int i = 0; i < partitions.Count; i++)
            {
                var p = partitions[i];
                sb.Append("    {");
                sb.Append("\"disk_index\": ").Append(p.DiskIndex).Append(", ");
                sb.Append("\"partition_index\": ").Append(p.Index).Append(", ");
                sb.Append("\"starting_offset\": ").Append(p.StartingOffset).Append(", ");
                sb.Append("\"size_bytes\": ").Append(p.SizeBytes).Append(", ");
                sb.Append("\"block_size\": ").Append(p.BlockSize).Append(", ");
                sb.Append("\"number_of_blocks\": ").Append(p.NumberOfBlocks).Append(", ");
                sb.Append("\"logical_disk\": ").Append(JsonStr(p.LogicalDisk)).Append(", ");
                sb.Append("\"file_system\": ").Append(JsonStr(p.FileSystem)).Append(", ");
                sb.Append("\"volume_serial\": ").Append(JsonStr(p.VolumeSerial)).Append(", ");
                sb.Append("\"type\": ").Append(JsonStr(p.Type)).Append(", ");
                sb.Append("\"bootable\": ").Append(p.Bootable ? "true" : "false");
                sb.Append("}").Append(i < partitions.Count - 1 ? "," : "").Append("\n");
            }
            sb.Append("  ],\n");

            // AUDIT TRAIL
            sb.Append("  \"audit_trail\": [\n");
            for (int i = 0; i < session.Events.Count; i++)
            {
                var ev = session.Events[i];
                sb.Append("    {");
                sb.Append("\"seq\": ").Append(ev.Sequence).Append(", ");
                sb.Append("\"timestamp\": ").Append(JsonStr(Iso8601.Format(ev.Timestamp))).Append(", ");
                sb.Append("\"category\": ").Append(JsonStr(ev.Category)).Append(", ");
                sb.Append("\"description\": ").Append(JsonStr(ev.Description)).Append(", ");
                sb.Append("\"prev_hash\": ").Append(JsonStr(ev.PrevHash)).Append(", ");
                sb.Append("\"event_hash\": ").Append(JsonStr(ev.EventHash));
                sb.Append("}").Append(i < session.Events.Count - 1 ? "," : "").Append("\n");
            }
            sb.Append("  ]\n");

            sb.Append("}\n");

            string content = sb.ToString();
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return HashUtility.Sha256OfFile(path);
        }

        private static string JsonStr(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
