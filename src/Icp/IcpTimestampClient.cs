using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ShieldBlock.Icp
{
    /// <summary>
    /// Cliente RFC 3161 simplificado para Autoridade de Carimbo de Tempo
    /// do ICP-Brasil (ACT-ITI: act.iti.gov.br) ou similar.
    ///
    /// Implementacao manual ASN.1 - sem dependencia externa.
    /// Constroi TimeStampReq, faz POST application/timestamp-query,
    /// recebe TimeStampResp e retorna a TimeStampToken bruta.
    /// </summary>
    public static class IcpTimestampClient
    {
        // Endpoints publicos conhecidos de ACT brasileira
        public const string ITI_ACT_URL = "https://act.iti.gov.br/";

        public class TimestampResult
        {
            public bool Success;
            public byte[] TokenBytes;        // TimeStampToken (PKCS#7 com data)
            public DateTime? GeneratedAt;    // Quando o ACT carimbou
            public string TsaName;
            public string Error;
        }

        /// <summary>
        /// Solicita carimbo de tempo para um hash SHA-256 dos dados.
        /// </summary>
        public static TimestampResult RequestTimestamp(byte[] dataToTimestamp,
                                                       string tsaUrl)
        {
            var result = new TimestampResult();
            try
            {
                // 1. Calcula SHA-256 dos dados
                byte[] hash;
                using (var sha = SHA256.Create())
                    hash = sha.ComputeHash(dataToTimestamp);

                // 2. Constroi TimeStampReq (RFC 3161)
                byte[] request = BuildTimestampRequest(hash);

                // 3. POST HTTP
                ServicePointManager.SecurityProtocol =
                    (SecurityProtocolType)0xC00 | // TLS 1.2 + 1.3
                    SecurityProtocolType.Tls12;

                var req = (HttpWebRequest)WebRequest.Create(tsaUrl);
                req.Method = "POST";
                req.ContentType = "application/timestamp-query";
                req.Accept = "application/timestamp-reply";
                req.Timeout = 30000;
                req.ContentLength = request.Length;
                req.UserAgent = "ShieldBlock/6.5 (Bruno Ferrari)";

                using (var s = req.GetRequestStream())
                    s.Write(request, 0, request.Length);

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var stream = resp.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    byte[] respBytes = ms.ToArray();

                    // O response inteiro eh a TimeStampResp ASN.1.
                    // Extrair o TimeStampToken (Status: granted seguido de PKCS#7).
                    byte[] token = ExtractTokenFromResponse(respBytes);
                    if (token == null)
                    {
                        result.Error = "Resposta sem token (status nao granted?)";
                        return result;
                    }

                    result.Success = true;
                    result.TokenBytes = token;
                    result.GeneratedAt = DateTime.UtcNow; // melhor parse seria
                                                          // extrair do TSTInfo
                    result.TsaName = tsaUrl;
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            return result;
        }

        // ============= ASN.1 BUILDERS =============

        private static byte[] BuildTimestampRequest(byte[] sha256Hash)
        {
            // TimeStampReq ::= SEQUENCE {
            //   version           INTEGER (1),
            //   messageImprint    MessageImprint,
            //   reqPolicy         OBJECT IDENTIFIER OPTIONAL,
            //   nonce             INTEGER OPTIONAL,
            //   certReq           BOOLEAN DEFAULT FALSE,
            //   extensions        [0] IMPLICIT Extensions OPTIONAL
            // }
            //
            // MessageImprint ::= SEQUENCE {
            //   hashAlgorithm     AlgorithmIdentifier,
            //   hashedMessage     OCTET STRING
            // }

            // SHA-256 OID: 2.16.840.1.101.3.4.2.1
            byte[] sha256Oid = new byte[]
            {
                0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01
            };
            byte[] nullParam = new byte[] { 0x05, 0x00 };

            // AlgorithmIdentifier = SEQUENCE { OID, NULL }
            byte[] algId = WrapSequence(Concat(sha256Oid, nullParam));

            // hashedMessage OCTET STRING
            byte[] hashOctet = WrapTag(0x04, sha256Hash);

            // MessageImprint = SEQUENCE { algId, octet }
            byte[] msgImprint = WrapSequence(Concat(algId, hashOctet));

            // version = INTEGER 1
            byte[] version = new byte[] { 0x02, 0x01, 0x01 };

            // nonce: 8 bytes random
            var rng = RandomNumberGenerator.Create();
            byte[] nonceBytes = new byte[8];
            rng.GetBytes(nonceBytes);
            // garantir positivo (bit alto = 0)
            nonceBytes[0] &= 0x7F;
            byte[] nonce = WrapTag(0x02, nonceBytes);

            // certReq = BOOLEAN TRUE
            byte[] certReq = new byte[] { 0x01, 0x01, 0xFF };

            // SEQUENCE final
            return WrapSequence(Concat(version, msgImprint, nonce, certReq));
        }

        private static byte[] ExtractTokenFromResponse(byte[] respBytes)
        {
            // TimeStampResp ::= SEQUENCE {
            //   status            PKIStatusInfo,
            //   timeStampToken    TimeStampToken OPTIONAL
            // }
            // PKIStatusInfo: status INTEGER (0=granted, 1=grantedWithMods, ...)
            //
            // Estrategia simplificada: localizar o segundo SEQUENCE de nivel
            // alto que comeca apos o PKIStatusInfo. Quando bem-sucedido, o
            // ContentInfo do TimeStampToken comeca com 30 82 xx xx 06 09
            // 2A 86 48 86 F7 0D 01 07 02 (OID signedData)

            // Procurar pelo OID signedData (1.2.840.113549.1.7.2)
            byte[] signedDataOid = new byte[]
            {
                0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x07, 0x02
            };
            int idx = FindBytes(respBytes, signedDataOid);
            if (idx < 0) return null;

            // Voltar ate achar a tag SEQUENCE (0x30) que contem este OID
            // O ContentInfo SEQUENCE comeca alguns bytes antes
            // Backtrack para encontrar inicio do SEQUENCE
            for (int back = idx - 1; back >= 0; back--)
            {
                if (respBytes[back] == 0x30)
                {
                    // Tenta interpretar como SEQUENCE valido
                    int lenBytes;
                    int totalLen = TryReadLength(respBytes, back + 1, out lenBytes);
                    if (totalLen > 0)
                    {
                        int seqEnd = back + 1 + lenBytes + totalLen;
                        if (seqEnd <= respBytes.Length && back + lenBytes < idx)
                        {
                            // Achou! Retorna do back ate seqEnd
                            byte[] token = new byte[seqEnd - back];
                            Array.Copy(respBytes, back, token, 0, token.Length);
                            return token;
                        }
                    }
                }
            }
            return null;
        }

        private static int TryReadLength(byte[] data, int offset, out int lengthBytesUsed)
        {
            lengthBytesUsed = 0;
            if (offset >= data.Length) return -1;
            byte first = data[offset];
            if (first < 0x80)
            {
                lengthBytesUsed = 1;
                return first;
            }
            int n = first & 0x7F;
            if (n == 0 || n > 4 || offset + n >= data.Length) return -1;
            lengthBytesUsed = 1 + n;
            int len = 0;
            for (int i = 0; i < n; i++)
                len = (len << 8) | data[offset + 1 + i];
            return len;
        }

        // ============= ASN.1 HELPERS =============

        private static byte[] WrapSequence(byte[] content)
        {
            return WrapTag(0x30, content);
        }

        private static byte[] WrapTag(byte tag, byte[] content)
        {
            byte[] len = EncodeLength(content.Length);
            byte[] result = new byte[1 + len.Length + content.Length];
            result[0] = tag;
            Array.Copy(len, 0, result, 1, len.Length);
            Array.Copy(content, 0, result, 1 + len.Length, content.Length);
            return result;
        }

        private static byte[] EncodeLength(int length)
        {
            if (length < 0x80) return new byte[] { (byte)length };
            if (length <= 0xFF) return new byte[] { 0x81, (byte)length };
            if (length <= 0xFFFF)
                return new byte[] { 0x82, (byte)(length >> 8), (byte)(length & 0xFF) };
            return new byte[]
            {
                0x83,
                (byte)((length >> 16) & 0xFF),
                (byte)((length >> 8) & 0xFF),
                (byte)(length & 0xFF)
            };
        }

        private static byte[] Concat(params byte[][] arrays)
        {
            int total = 0;
            foreach (var a in arrays) total += a.Length;
            byte[] result = new byte[total];
            int pos = 0;
            foreach (var a in arrays)
            {
                Array.Copy(a, 0, result, pos, a.Length);
                pos += a.Length;
            }
            return result;
        }

        private static int FindBytes(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                if (match) return i;
            }
            return -1;
        }
    }
}
