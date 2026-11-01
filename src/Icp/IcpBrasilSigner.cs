using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ShieldBlock.Icp
{
    /// <summary>
    /// Assinatura digital ICP-Brasil via PKCS#7/CMS (CAdES-BES).
    /// Suporta e-CPF e e-CNPJ, A1 (arquivo .pfx) e A3 (smartcard/token).
    ///
    /// Para A3 o Windows pede o PIN automaticamente quando ComputeSignature()
    /// acessa a chave privada. Para A1 a chave ja esta na memoria (importada
    /// do .pfx com a senha).
    /// </summary>
    public class IcpBrasilSigner
    {
        /// <summary>
        /// Lista TODOS os certificados disponiveis no Windows Cert Store
        /// (CurrentUser + LocalMachine, ambos com chave privada).
        /// Para A3, o smartcard precisa estar conectado.
        /// </summary>
        public static List<IcpBrasilCertificate> ListAvailableCertificates()
        {
            var result = new List<IcpBrasilCertificate>();
            var seenThumbprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var loc in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
            {
                X509Store store = null;
                try
                {
                    store = new X509Store(StoreName.My, loc);
                    store.Open(OpenFlags.ReadOnly);
                    foreach (X509Certificate2 c in store.Certificates)
                    {
                        try
                        {
                            if (!c.HasPrivateKey) continue;
                            if (seenThumbprints.Contains(c.Thumbprint)) continue;
                            seenThumbprints.Add(c.Thumbprint);
                            result.Add(new IcpBrasilCertificate(c));
                        }
                        catch { }
                    }
                }
                catch { /* store pode falhar em alguns SOs */ }
                finally
                {
                    try { if (store != null) store.Close(); } catch { }
                }
            }
            return result;
        }

        /// <summary>
        /// Carrega cert de arquivo .pfx/.p12 (A1) com a senha do usuario.
        /// </summary>
        public static IcpBrasilCertificate LoadFromPfx(string path, SecureString password)
        {
            byte[] data = File.ReadAllBytes(path);
            var cert = new X509Certificate2(data, password,
                X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
            return new IcpBrasilCertificate(cert);
        }

        /// <summary>
        /// Recupera o cert pelo thumbprint (apos o user ter selecionado).
        /// Procura em CurrentUser e LocalMachine.
        /// </summary>
        public static IcpBrasilCertificate GetByThumbprint(string thumbprint)
        {
            if (string.IsNullOrEmpty(thumbprint)) return null;
            string tp = thumbprint.Replace(" ", "").ToUpperInvariant();

            foreach (var loc in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
            {
                X509Store store = null;
                try
                {
                    store = new X509Store(StoreName.My, loc);
                    store.Open(OpenFlags.ReadOnly);
                    foreach (X509Certificate2 c in store.Certificates)
                    {
                        try
                        {
                            if (string.Equals(c.Thumbprint, tp, StringComparison.OrdinalIgnoreCase)
                                && c.HasPrivateKey)
                                return new IcpBrasilCertificate(c);
                        }
                        catch { }
                    }
                }
                catch { }
                finally
                {
                    try { if (store != null) store.Close(); } catch { }
                }
            }
            return null;
        }

        /// <summary>
        /// Assina os bytes usando PKCS#7/CMS (CAdES-BES detached).
        /// Retorna bytes do .p7s para salvar em arquivo.
        ///
        /// Para A3 isso DISPARA o popup do Windows pedindo o PIN do smartcard.
        /// Para A1 usa a chave em memoria sem intervencao do usuario.
        /// </summary>
        public static byte[] SignDetached(byte[] dataToSign, IcpBrasilCertificate cert)
        {
            if (cert == null || cert.X509 == null)
                throw new ArgumentException("Certificado nulo");
            if (!cert.HasPrivateKey)
                throw new InvalidOperationException(
                    "Certificado sem chave privada - nao pode assinar");

            var contentInfo = new ContentInfo(dataToSign);
            var signedCms = new SignedCms(contentInfo, detached: true);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, cert.X509);
            signer.DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"); // SHA-256
            signer.IncludeOption = X509IncludeOption.WholeChain;

            // ComputeSignature acessa a chave privada
            // (para A3, Windows mostra dialog do PIN aqui)
            signedCms.ComputeSignature(signer, silent: false);
            return signedCms.Encode();
        }

        /// <summary>
        /// Assina uma string SHA-256 (FinalHash da sessao).
        /// Conveniente: converte para bytes UTF-8 antes.
        /// </summary>
        public static byte[] SignHashString(string finalHashHex, IcpBrasilCertificate cert)
        {
            return SignDetached(Encoding.UTF8.GetBytes(finalHashHex), cert);
        }

        /// <summary>
        /// Valida uma assinatura .p7s contra os dados originais.
        /// Retorna lista de problemas (vazia = OK).
        /// </summary>
        public static List<string> Verify(byte[] originalData, byte[] signatureP7s)
        {
            var problems = new List<string>();
            try
            {
                var contentInfo = new ContentInfo(originalData);
                var signedCms = new SignedCms(contentInfo, detached: true);
                signedCms.Decode(signatureP7s);

                try
                {
                    signedCms.CheckSignature(verifySignatureOnly: false);
                }
                catch (CryptographicException ex)
                {
                    problems.Add("Assinatura invalida: " + ex.Message);
                }

                // Verificar validade dos certs
                foreach (var signer in signedCms.SignerInfos)
                {
                    var c = signer.Certificate;
                    if (c == null)
                    {
                        problems.Add("Signatario sem certificado");
                        continue;
                    }
                    var now = DateTime.Now;
                    if (now < c.NotBefore)
                        problems.Add("Cert ainda nao valido: " + c.Subject);
                    if (now > c.NotAfter)
                        problems.Add("Cert expirado em " + c.NotAfter + ": " + c.Subject);
                }

                if (problems.Count == 0)
                    problems.Add("OK - assinatura validada com sucesso ("
                        + signedCms.SignerInfos.Count + " signatario(s))");
            }
            catch (Exception ex)
            {
                problems.Add("Erro ao validar: " + ex.Message);
            }
            return problems;
        }

        /// <summary>
        /// Extrai informacoes legiveis de um arquivo .p7s.
        /// </summary>
        public static List<IcpBrasilCertificate> ExtractSigners(byte[] signatureP7s)
        {
            var list = new List<IcpBrasilCertificate>();
            try
            {
                var dummyContent = new ContentInfo(new byte[] { 0 });
                var signedCms = new SignedCms(dummyContent, detached: true);
                signedCms.Decode(signatureP7s);
                foreach (var signer in signedCms.SignerInfos)
                {
                    if (signer.Certificate != null)
                        list.Add(new IcpBrasilCertificate(signer.Certificate));
                }
            }
            catch { }
            return list;
        }
    }
}
