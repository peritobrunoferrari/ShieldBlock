# Changelog

Todas as mudanças notáveis do ShieldBlock. Formato baseado em [Keep a Changelog](https://keepachangelog.com/).

## [6.5] — 2026-05-30

### Adicionado
- **Integração ICP-Brasil completa** (`src/Icp/`)
  - Suporte a e-CPF e e-CNPJ, A1 (arquivo .pfx) e A3 (smartcard/token)
  - `IcpBrasilCertificate.cs` — wrapper X509 que extrai OIDs ICP (`2.16.76.1.3.1` PF, `2.16.76.1.3.3` CNPJ)
  - `IcpBrasilSigner.cs` — assinatura PKCS#7/CAdES-BES via `System.Security.Cryptography.Pkcs.SignedCms`
  - `IcpTimestampClient.cs` — cliente RFC 3161 manual pra ACT-ITI (sem dependência externa)
  - `IcpBrasilDialog.cs` — UI de seleção de certificado (lista do Windows Cert Store + carregar .pfx externo)
  - Botão "ICP-BRASIL" no header + badge "ICP ATIVO"
  - Ao fim da sessão, gera `.p7s` e `.p7s.tst` ao lado do `.log`

### Consertado
- **AutoMount agora funciona**: adicionado `mountvol.exe /N /E` em vez de só Registry
- **Botão "LIMPAR CACHE DE DRIVES"** (`mountvol /R`) — Windows esquece drives conhecidos

## [6.4] — 2026-05-30

### Adicionado
- **UsbWatcher com snapshot diff**: logs detalhados de plug/unplug com letra, label, FS, tamanho, modelo e serial do disco físico
- **ForensicToolWatcher**: detecta launch de 70+ ferramentas (FTK Imager, Autopsy, X-Ways, EnCase, Magnet AXIOM, Cellebrite UFED, OSForensics, Volatility, Wireshark, etc) + anti-forenses (CCleaner, BleachBit, Eraser, VeraCrypt)
- WMI `Win32_ProcessStartTrace` + fallback via snapshot periódico

## [6.3] — 2026-05-29

### Adicionado
- **Splash screen** com créditos antes do app principal
- **Logo real do Shield** (PNG do SVG oficial) no splash e header
- **Botão "SOBRE"** reabre splash
- **Banner CADEIA DE CUSTÓDIA**: integridade + timer + hash atual sempre visíveis
- **PDF rico**: boxes coloridos por seção, tabelas com bordas, header/footer em cada página
- **Error handler** robusto com crash dump

### Consertado
- Botões da aba HISTÓRICO cortados (FlowLayoutPanel com WrapContents)

## [6.2] — 2026-05-28

### Adicionado
- **MachineFingerprint** imutável: Hash SHA-256 do próprio .exe + Windows SID + Machine GUID + BIOS Serial
- **SessionRegistry** (`SESSIONS_INDEX.log`) — índice global append-only de todas as sessões, encadeado por hash
- **UsbWatcher** (WMI `Win32_VolumeChangeEvent`)
- **RegistryTamperGuard** — detecta alterações externas nas chaves WriteBlock
- **FileSystemMonitor** (`FileSystemWatcher` em drives não-system)
- **ProcessAuditor** (snapshot top 10 processos)
- **HeartbeatService** (30s)
- **Nova aba HISTÓRICO** com verificação de integridade
- **Filtro no audit trail vivo**

## [6.1] — 2026-05-27

### Adicionado
- **Cards verde/vermelho preenchidos** (WriteBlock + AutoMount)
- **PeritoEditDialog** — edita dados do perito direto no app
- **AutoLogger** — grava cada evento IMEDIATAMENTE em disco (`session_<id>_<ts>.log`)
- **LogOnlyExporter** (TXT + JSON) — export só do audit trail
- **Botão "Abrir pasta de logs"**

## [6.0] — 2026-05-27

### Primeira versão refatorada
- Arquitetura modular: 17 arquivos .cs separados por responsabilidade
- **Dark theme Catppuccin Mocha**
- **4 exportadores**: PDF (puro C#), HTML interativo, JSON, XML DFRWS
- **Audit trail blockchain-leve** (SHA-256 encadeado)
- **StatusCards customizados** com cantos arredondados
- **Fingerprint do perito** carregado de `Config/perito.json`
- Compatível .NET Framework 4.7.2 / C# 5
- Sem dependências externas

## [5.3] e anteriores

- v5.3: Análise de partições via WMI
- v5.0: Dark theme básico + 3 abas
- v4.0: Export TXT/CSV
- v3.0: Write Block básico via Registry
