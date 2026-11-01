@echo off
REM =============================================================
REM  ShieldBlock - Script de Upload para GitHub
REM  Automatiza: git init + commits + tag + push
REM =============================================================

cd /d %~dp0
title ShieldBlock - Upload GitHub
color 0B
echo.
echo =============================================================
echo   SHIELDBLOCK - UPLOAD PARA GITHUB
echo =============================================================
echo.

REM Verifica Git instalado
where git >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERRO] Git nao esta instalado.
    echo.
    echo Baixe e instale:
    echo    https://git-scm.com/download/win
    echo.
    echo Durante a instalacao, deixe as opcoes padrao.
    pause
    exit /b 1
)
echo [OK] Git instalado:
git --version
echo.

REM Configura identidade (local - so neste repo)
echo [...] Configurando identidade do commit...
git config --local user.name  "Bruno Enrico Moro Ferrari"
git config --local user.email "peritodigitalexpert@gmail.com"
echo [OK] Identidade configurada

REM init - sempre remove .git vazia e cria do zero
if exist ".git\HEAD" (
    echo [INFO] Repositorio Git ja inicializado corretamente
) else (
    echo [...] Limpando .git parcial (se existir)...
    rmdir /s /q .git 2>nul
    echo [...] Inicializando repositorio Git...
    git init -b main
    echo [OK] Repositorio inicializado
)

echo.
echo [...] Adicionando arquivos...
git add .gitignore LICENSE AUTHORS.md CONTRIBUTING.md README.md CHANGELOG.md
git add src/ Resources/ Config/ COMPILAR.bat ShieldBlock.csproj ShieldBlock.sln app.manifest
git add docs/ Logs/.gitkeep
git add SUBIR_PARA_GITHUB.bat PUSH_INSTRUCTIONS.md 2>nul

echo.
echo [...] Fazendo commit inicial...
git commit -m "feat: ShieldBlock v6.5 - initial public release" -m "USB Write Blocker Forense Auditavel com Cadeia de Custodia Criptografica e Assinatura Digital ICP-Brasil." -m "- 36 .cs files, ~7100 LOC, zero NuGet dependency" -m "- .NET Framework 4.7.2 / C# 5" -m "- ICP-Brasil integration (e-CPF/e-CNPJ, A1/A3)" -m "- Audit trail SHA-256 chained + auto-log + session registry" -m "- 70+ forensic tools detection" -m "- Dark Catppuccin theme + Shield Pericias branding"

if %errorLevel% neq 0 (
    echo [INFO] Possivelmente nada novo pra commitar - prosseguindo
)

echo.
echo [...] Criando tag v6.5...
git tag -a v6.5 -m "Release v6.5 - Initial public release with ICP-Brasil"

echo.
echo =============================================================
echo   PROXIMO PASSO: criar o repo no GitHub (manual)
echo =============================================================
echo.
echo 1. Abra:  https://github.com/new
echo 2. Owner: peritobrunoferrari
echo 3. Nome:  ShieldBlock
echo 4. Descricao: USB Write Blocker Forense Auditavel com ICP-Brasil
echo 5. Marque PUBLIC
echo 6. NAO marque "Add README/gitignore/license" (ja temos)
echo 7. Clique "Create repository"
echo.
echo Depois volta aqui e aperte qualquer tecla.
echo.
pause

echo.
echo [...] Adicionando remote origin...
git remote remove origin 2>nul
git remote add origin https://github.com/peritobrunoferrari/ShieldBlock.git
echo [OK] Remote adicionado

echo.
echo =============================================================
echo   AUTENTICACAO
echo =============================================================
echo.
echo No proximo passo, o Git vai pedir login do GitHub.
echo A primeira vez, abrira um browser com GitHub Credential Manager.
echo Faca login com usuario: peritobrunoferrari
echo.
pause

echo.
echo [...] Fazendo push do main...
git push -u origin main

echo.
echo [...] Fazendo push da tag v6.5...
git push origin v6.5

echo.
echo =============================================================
echo   [OK] UPLOAD CONCLUIDO!
echo =============================================================
echo.
echo Repositorio: https://github.com/peritobrunoferrari/ShieldBlock
echo.
echo PROXIMO PASSO - Criar a Release:
echo.
echo 1. Abra: https://github.com/peritobrunoferrari/ShieldBlock/releases/new
echo 2. "Choose a tag": v6.5
echo 3. Release title: ShieldBlock v6.5 - ICP-Brasil Integration
echo 4. Cole o conteudo de CHANGELOG.md secao [6.5]
echo 5. Compile o app (COMPILAR.bat) e zipe a pasta bin\Release
echo 6. Arraste o .zip pra area "Attach binaries"
echo 7. Marque "Set as latest release"
echo 8. Publish release
echo.
pause
