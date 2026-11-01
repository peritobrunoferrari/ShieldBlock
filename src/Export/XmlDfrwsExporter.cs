using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using ShieldBlock.Core;
using ShieldBlock.Utils;

namespace ShieldBlock.Export
{
    /// <summary>
    /// Exportador XML compatível com padrão DFRWS (Digital Forensic Research Workshop).
    /// Estrutura genérica baseada em CASE/UCO para interoperabilidade.
    /// </summary>
    public class XmlDfrwsExporter
    {
        public string Export(string path,
                             LogSession session,
                             List<DriveRecord> drives,
                             List<DiskRecord> disks,
                             List<PartitionRecord> partitions)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                Encoding = new UTF8Encoding(false),
                NewLineChars = "\n"
            };

            using (var w = XmlWriter.Create(path, settings))
            {
                w.WriteStartDocument();
                w.WriteStartElement("CaseEvidence");
                w.WriteAttributeString("xmlns", "https://shieldblock.local/schema/v6");
                w.WriteAttributeString("tool", "ShieldBlock v6.0");
                w.WriteAttributeString("standard", "DFRWS-compatible");
                w.WriteAttributeString("generated", Iso8601.FormatNow());

                // SESSION
                w.WriteStartElement("Session");
                w.WriteElementString("Id", session.SessionId);
                w.WriteElementString("StartedAt", Iso8601.Format(session.StartedAt));
                w.WriteElementString("Operator", session.OperatorUser);
                w.WriteElementString("Machine", session.MachineName);
                w.WriteElementString("EventCount", session.EventCount.ToString());
                w.WriteElementString("FinalHashSha256", session.FinalHash);
                w.WriteElementString("IntegrityOk",
                    session.VerifyIntegrity() ? "true" : "false");
                w.WriteEndElement();

                // PERITO
                w.WriteStartElement("Examiner");
                w.WriteElementString("Name", session.Perito.Nome);
                w.WriteElementString("CPF", session.Perito.CPF);
                w.WriteElementString("Registration", session.Perito.Registro);
                w.WriteElementString("Organization", session.Perito.Orgao);
                w.WriteElementString("Role", session.Perito.Cargo);
                w.WriteElementString("Email", session.Perito.Email ?? "");
                w.WriteEndElement();

                // DRIVES LÓGICOS
                w.WriteStartElement("LogicalDrives");
                foreach (var d in drives)
                {
                    w.WriteStartElement("Drive");
                    w.WriteAttributeString("letter", d.Letter ?? "");
                    w.WriteElementString("Label", d.Label ?? "");
                    w.WriteElementString("FileSystem", d.FileSystem ?? "");
                    w.WriteElementString("DriveType", d.DriveType ?? "");
                    w.WriteElementString("TotalBytes", d.TotalBytes.ToString());
                    w.WriteElementString("FreeBytes", d.FreeBytes.ToString());
                    w.WriteElementString("Ready", d.IsReady ? "true" : "false");
                    w.WriteEndElement();
                }
                w.WriteEndElement();

                // DISCOS FÍSICOS
                w.WriteStartElement("PhysicalDisks");
                foreach (var d in disks)
                {
                    w.WriteStartElement("Disk");
                    w.WriteAttributeString("deviceId", d.DeviceID ?? "");
                    w.WriteElementString("Model", d.Model ?? "");
                    w.WriteElementString("Interface", d.InterfaceType ?? "");
                    w.WriteElementString("MediaType", d.MediaType ?? "");
                    w.WriteElementString("SerialNumber", d.SerialNumber ?? "");
                    w.WriteElementString("SizeBytes", d.SizeBytes.ToString());
                    w.WriteElementString("Partitions", d.Partitions.ToString());
                    w.WriteEndElement();
                }
                w.WriteEndElement();

                // PARTIÇÕES
                w.WriteStartElement("Partitions");
                foreach (var p in partitions)
                {
                    w.WriteStartElement("Partition");
                    w.WriteAttributeString("diskIndex", p.DiskIndex.ToString());
                    w.WriteAttributeString("index", p.Index.ToString());
                    w.WriteElementString("StartingOffset", p.StartingOffset.ToString());
                    w.WriteElementString("SizeBytes", p.SizeBytes.ToString());
                    w.WriteElementString("BlockSize", p.BlockSize.ToString());
                    w.WriteElementString("NumberOfBlocks", p.NumberOfBlocks.ToString());
                    w.WriteElementString("LogicalDisk", p.LogicalDisk ?? "");
                    w.WriteElementString("FileSystem", p.FileSystem ?? "");
                    w.WriteElementString("VolumeSerial", p.VolumeSerial ?? "");
                    w.WriteElementString("Type", p.Type ?? "");
                    w.WriteElementString("Bootable", p.Bootable ? "true" : "false");
                    w.WriteEndElement();
                }
                w.WriteEndElement();

                // AUDIT TRAIL
                w.WriteStartElement("AuditTrail");
                foreach (var ev in session.Events)
                {
                    w.WriteStartElement("Event");
                    w.WriteAttributeString("seq", ev.Sequence.ToString());
                    w.WriteElementString("Timestamp", Iso8601.Format(ev.Timestamp));
                    w.WriteElementString("Category", ev.Category ?? "");
                    w.WriteElementString("Description", ev.Description ?? "");
                    w.WriteElementString("PrevHash", ev.PrevHash ?? "");
                    w.WriteElementString("EventHash", ev.EventHash ?? "");
                    w.WriteEndElement();
                }
                w.WriteEndElement();

                w.WriteEndElement(); // CaseEvidence
                w.WriteEndDocument();
            }

            return HashUtility.Sha256OfFile(path);
        }
    }
}
