# 🚀 Guia Passo-a-Passo: Subir o ShieldBlock pro GitHub

## PASSO 1: Instalar Git

Baixe e instale: **https://git-scm.com/download/win**

Durante a instalação, deixe TUDO padrão (next → next → install).

Teste no CMD:
```cmd
git --version
```
Deve mostrar `git version 2.x.x`.

## PASSO 2: Criar o repositório vazio no GitHub

1. Abra: **https://github.com/new**
2. **Owner:** `peritobrunoferrari`
3. **Repository name:** `ShieldBlock`
4. **Description:** `USB Write Blocker Forense Auditável com Cadeia de Custódia Criptográfica e ICP-Brasil`
5. Marque **Public**
6. ⚠️ **NÃO** marque "Add a README file"
7. ⚠️ **NÃO** adicione .gitignore nem license (já temos localmente)
8. Clique **Create repository**

## PASSO 3: Rodar o script automático

Na pasta `ShieldBlock-github/`, dê duplo-clique em:

```
SUBIR_PARA_GITHUB.bat
```

O script faz:
- ✅ `git init` + configuração do usuário
- ✅ Commit inicial com toda a estrutura
- ✅ Tag `v6.5`
- ✅ `git remote add origin ...`
- ✅ `git push -u origin main`
- ✅ `git push origin v6.5`

Na primeira vez vai abrir o **GitHub Credential Manager** no browser pedindo login.
Faça login com `peritobrunoferrari` e autorize.

## PASSO 4: Criar a Release v6.5

1. Abra: **https://github.com/peritobrunoferrari/ShieldBlock/releases/new**
2. **Choose a tag:** selecione `v6.5` (já existe)
3. **Release title:** `ShieldBlock v6.5 - Integração ICP-Brasil`
4. **Description:** cole esse texto:

```markdown
## 🎉 Primeira versão pública!

ShieldBlock é um bloqueador de escrita USB forense com auditoria criptográfica
e assinatura digital ICP-Brasil (e-CPF/e-CNPJ A1/A3).

### ⭐ Destaques da v6.5
- **ICP-Brasil completo**: PKCS#7 CAdES-BES + carimbo de tempo RFC 3161 (ACT-ITI)
- **Auto-log em tempo real** com SHA-256 encadeado
- **70+ ferramentas forenses detectadas** (FTK Imager, Autopsy, X-Ways, EnCase...)
- **Fingerprint imutável** da máquina (hash .exe + Machine GUID + SID + BIOS)
- **Interface dark** profissional com banner de Cadeia de Custódia

### 📥 Instalação

Baixe **ShieldBlock-v6.5-win.zip** abaixo, extraia e execute `ShieldBlock.exe` como Administrador.

Veja [CHANGELOG.md](https://github.com/peritobrunoferrari/ShieldBlock/blob/main/CHANGELOG.md) completo.

### 📋 Requisitos
- Windows 10 ou 11
- .NET Framework 4.7.2 (já vem no Windows 10+)
- Privilégios de Administrador

### 👤 Autor
Bruno Enrico Moro Ferrari - Perito Criminal, Polícia Científica de SP
Em parceria com Shield Perícias Digitais - contato@shieldpericias.com.br

### ⚖️ Licença
GPL v3 - para uso comercial em soluções proprietárias, entre em contato.
```

5. **Compile o app**: rode `COMPILAR.bat` → gera `bin\Release\ShieldBlock.exe`
6. **Zipe a pasta Release**: botão direito em `bin\Release\` → "Enviar para > Pasta compactada" → renomeie para `ShieldBlock-v6.5-win.zip`
7. **Arraste o .zip** pra área "Attach binaries by dropping them here or selecting them"
8. Marque **"Set as the latest release"**
9. Clique **Publish release** 🚀

## PASSO 5: Adicionar screenshots (opcional, mas recomendado)

Depois:
1. Tire screenshots do app:
   - Splash com créditos
   - Dashboard com cards verde/vermelho
   - Aba LOGS com audit trail
   - Aba HISTÓRICO
   - Banner CADEIA DE CUSTÓDIA
2. Salve em `docs/screenshots/`:
   - `splash.png`
   - `dashboard.png`
   - `logs.png`
   - `history.png`
   - `banner.png`
3. No CMD:
```cmd
cd C:\caminho\pra\ShieldBlock
git add docs/screenshots/
git commit -m "docs: add screenshots"
git push
```

Pronto, screenshots aparecem no README automaticamente! 🎨

## 🐛 Troubleshooting

### "git não é reconhecido como comando"
Reinstale o Git marcando "Add Git to PATH" na instalação.

### "Permission denied (publickey)" ao fazer push
Use HTTPS em vez de SSH (o script já faz isso).
Se pedir senha, use um **Personal Access Token**:
1. https://github.com/settings/tokens
2. Generate new token (classic) → marque `repo` → create
3. Copie o token (começa com `ghp_...`)
4. No prompt do git push, cole o token no lugar da senha

### "Repository not found"
Confira que criou o repo com nome EXATO: `ShieldBlock` (case-sensitive).

### Erro no push de arquivos grandes
Se o `.exe` compilado estiver > 100 MB (improvável), use GitHub Releases pra anexar em vez de commitar.

## ✅ Após publicar

Seu projeto estará em:
**https://github.com/peritobrunoferrari/ShieldBlock**

Compartilhe com colegas de perícia! 🛡️
